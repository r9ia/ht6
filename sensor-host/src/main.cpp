#include <smartspectra/messages/metrics.h>
#include <smartspectra/smartspectra.h>
#include <smartspectra/smartspectra_config.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <csignal>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <mutex>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <thread>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
using SocketHandle = SOCKET;
using SocketLength = int;
constexpr SocketHandle kInvalidSocket = INVALID_SOCKET;
#else
#include <arpa/inet.h>
#include <netdb.h>
#include <sys/socket.h>
#include <unistd.h>
using SocketHandle = int;
using SocketLength = socklen_t;
constexpr SocketHandle kInvalidSocket = -1;
#endif

namespace spectra = presage::smartspectra;

namespace {

std::atomic_bool g_stop_requested{false};

void HandleSignal(int) {
    g_stop_requested.store(true);
}

int64_t NowEpochUs() {
    return std::chrono::duration_cast<std::chrono::microseconds>(
               std::chrono::system_clock::now().time_since_epoch())
        .count();
}

double ConfidencePercent(double value) {
    return std::clamp(value, 0.0, 100.0);
}

struct Options {
    std::string host = "127.0.0.1";
    uint16_t port = 7776;
    int camera_index = 0;
    int width = 1280;
    int height = 720;
    int fps = 30;
    std::string replay_path;
    bool dry_run = false;
};

void PrintUsage() {
    std::cout
        << "Usage: dread-sensor-host [options]\n"
        << "  --host HOST       QNX/replay receiver host (default 127.0.0.1)\n"
        << "  --port PORT       VitalsSample UDP port (default 7776)\n"
        << "  --camera INDEX    Camera index (default 0)\n"
        << "  --replay FILE     Send synthetic/recorded JSONL without opening camera\n"
        << "  --dry-run         Validate configuration without camera or network\n"
        << "  --help            Show this message\n";
}

Options ParseOptions(int argc, char** argv) {
    Options options;
    for (int i = 1; i < argc; ++i) {
        const std::string arg = argv[i];
        auto next = [&]() -> std::string {
            if (++i >= argc) throw std::invalid_argument("Missing value after " + arg);
            return argv[i];
        };

        if (arg == "--host") {
            options.host = next();
        } else if (arg == "--port") {
            const int value = std::stoi(next());
            if (value < 1 || value > 65535) throw std::invalid_argument("Port must be 1..65535");
            options.port = static_cast<uint16_t>(value);
        } else if (arg == "--camera") {
            options.camera_index = std::stoi(next());
            if (options.camera_index < 0) throw std::invalid_argument("Camera index cannot be negative");
        } else if (arg == "--replay") {
            options.replay_path = next();
        } else if (arg == "--dry-run") {
            options.dry_run = true;
        } else if (arg == "--help" || arg == "-h") {
            PrintUsage();
            std::exit(0);
        } else {
            throw std::invalid_argument("Unknown option: " + arg);
        }
    }
    return options;
}

class UdpSender {
public:
    UdpSender(const std::string& host, uint16_t port) {
#ifdef _WIN32
        WSADATA data{};
        if (WSAStartup(MAKEWORD(2, 2), &data) != 0) {
            throw std::runtime_error("WSAStartup failed");
        }
        winsock_started_ = true;
#endif
        addrinfo hints{};
        hints.ai_family = AF_UNSPEC;
        hints.ai_socktype = SOCK_DGRAM;
        hints.ai_protocol = IPPROTO_UDP;
        addrinfo* results = nullptr;
        const std::string service = std::to_string(port);
        const int status = getaddrinfo(host.c_str(), service.c_str(), &hints, &results);
        if (status != 0 || results == nullptr) {
            throw std::runtime_error("Could not resolve UDP destination");
        }

        for (const addrinfo* candidate = results; candidate != nullptr; candidate = candidate->ai_next) {
            socket_ = socket(candidate->ai_family, candidate->ai_socktype, candidate->ai_protocol);
            if (socket_ == kInvalidSocket) continue;
            destination_length_ = static_cast<SocketLength>(candidate->ai_addrlen);
            if (candidate->ai_addrlen <= sizeof(destination_)) {
                std::memcpy(&destination_, candidate->ai_addr, candidate->ai_addrlen);
                break;
            }
            CloseSocket();
        }
        freeaddrinfo(results);
        if (socket_ == kInvalidSocket) throw std::runtime_error("Could not create UDP socket");
    }

    ~UdpSender() {
        CloseSocket();
#ifdef _WIN32
        if (winsock_started_) WSACleanup();
#endif
    }

    UdpSender(const UdpSender&) = delete;
    UdpSender& operator=(const UdpSender&) = delete;

    void Send(const std::string& payload) const {
        const int sent = sendto(socket_, payload.data(), static_cast<int>(payload.size()), 0,
                                reinterpret_cast<const sockaddr*>(&destination_), destination_length_);
        if (sent < 0 || static_cast<size_t>(sent) != payload.size()) {
            throw std::runtime_error("UDP send failed");
        }
    }

private:
    void CloseSocket() {
        if (socket_ == kInvalidSocket) return;
#ifdef _WIN32
        closesocket(socket_);
#else
        close(socket_);
#endif
        socket_ = kInvalidSocket;
    }

    SocketHandle socket_ = kInvalidSocket;
    sockaddr_storage destination_{};
    SocketLength destination_length_ = 0;
#ifdef _WIN32
    bool winsock_started_ = false;
#endif
};

struct VitalsSnapshot {
    int64_t timestamp_us = 0;
    bool signal_valid = false;
    int validation_code = -1;
    double pulse_bpm = 0.0;
    double pulse_confidence_pct = 0.0;
    double fear_confidence_pct = 0.0;
    double baevsky = 0.0;
    double hrv_confidence_pct = 0.0;
    double breathing_bpm = 0.0;
    double breathing_confidence_pct = 0.0;
    bool talking = false;
};

std::string Serialize(const VitalsSnapshot& value) {
    std::ostringstream json;
    json << std::fixed << std::setprecision(3)
         << "{\"version\":1,\"type\":\"vitals\",\"timestampUs\":" << value.timestamp_us
         << ",\"signalValid\":" << (value.signal_valid ? "true" : "false")
         << ",\"validationCode\":" << value.validation_code
         << ",\"pulseBpm\":" << value.pulse_bpm
         << ",\"pulseConfidence\":" << ConfidencePercent(value.pulse_confidence_pct)
         << ",\"fear\":" << ConfidencePercent(value.fear_confidence_pct)
         << ",\"baevsky\":" << value.baevsky
         << ",\"hrvConfidence\":" << ConfidencePercent(value.hrv_confidence_pct)
         << ",\"breathingBpm\":" << value.breathing_bpm
         << ",\"breathingConfidence\":"
         << (value.talking ? 0.0 : ConfidencePercent(value.breathing_confidence_pct))
         << ",\"talking\":" << (value.talking ? "true" : "false") << "}";
    return json.str();
}

int RunReplay(const Options& options) {
    std::ifstream input(options.replay_path);
    if (!input) throw std::runtime_error("Could not open replay file: " + options.replay_path);
    UdpSender sender(options.host, options.port);
    std::string line;
    size_t count = 0;
    while (!g_stop_requested.load() && std::getline(input, line)) {
        if (line.empty() || line.front() == '#') continue;
        sender.Send(line);
        ++count;
        std::this_thread::sleep_for(std::chrono::milliseconds(200));
    }
    std::cout << "Replayed " << count << " VitalsSample packets.\n";
    return 0;
}

std::string ResolveApiKey() {
    if (const char* value = std::getenv("SMARTSPECTRA_API_KEY")) return value;
    return {};
}

int RunLive(const Options& options) {
    const std::string api_key = ResolveApiKey();
    if (api_key.empty()) {
        throw std::runtime_error("SMARTSPECTRA_API_KEY is not set");
    }

    UdpSender sender(options.host, options.port);
    spectra::SmartSpectraConfig config;
    config.api_key = api_key;
    config.requested_metrics = spectra::SmartSpectraConfig::BreathingMetrics();
    config.AddMetrics(spectra::SmartSpectraConfig::CardioMetrics());
    config.AddMetrics(spectra::SmartSpectraConfig::FaceMetrics());

    spectra::SmartSpectra sdk(config);
    VitalsSnapshot state;
    std::mutex state_mutex;
    std::mutex send_mutex;
    auto last_send = std::chrono::steady_clock::time_point::min();

    auto emit = [&](int64_t timestamp_us, bool force) {
        VitalsSnapshot snapshot;
        {
            std::lock_guard<std::mutex> lock(state_mutex);
            state.timestamp_us = timestamp_us > 0 ? timestamp_us : NowEpochUs();
            snapshot = state;
        }
        std::lock_guard<std::mutex> lock(send_mutex);
        const auto now = std::chrono::steady_clock::now();
        if (!force && last_send != std::chrono::steady_clock::time_point::min() &&
            now - last_send < std::chrono::milliseconds(200)) {
            return;
        }
        try {
            const std::string payload = Serialize(snapshot);
            sender.Send(payload);
            std::cout << payload << '\n';
            last_send = now;
        } catch (const std::exception& error) {
            std::cerr << "[sensor-host] " << error.what() << '\n';
        }
    };

    sdk.SetOnMetrics([&](const spectra::Metrics& metrics, int64_t timestamp_us) {
        {
            std::lock_guard<std::mutex> lock(state_mutex);
            if (metrics.has_cardio()) {
                const auto& cardio = metrics.cardio();
                if (cardio.pulse_rate_size() > 0) {
                    const auto& pulse = cardio.pulse_rate(cardio.pulse_rate_size() - 1);
                    state.pulse_bpm = pulse.value();
                    state.pulse_confidence_pct = pulse.confidence();
                }
                if (cardio.hrv_size() > 0) {
                    const auto& hrv = cardio.hrv(cardio.hrv_size() - 1);
                    state.baevsky = hrv.baevsky();
                    state.hrv_confidence_pct = hrv.confidence();
                }
            }
            if (metrics.has_breathing() && metrics.breathing().rate_size() > 0) {
                const auto& breathing = metrics.breathing().rate(metrics.breathing().rate_size() - 1);
                state.breathing_bpm = breathing.value();
                state.breathing_confidence_pct = breathing.confidence();
            }
            if (metrics.has_face()) {
                const auto& face = metrics.face();
                if (face.talking_size() > 0) {
                    state.talking = face.talking(face.talking_size() - 1).detected();
                }
                if (face.expression_size() > 0) {
                    const auto& expression = face.expression(face.expression_size() - 1);
                    double fear = 0.0;
                    for (const auto& score : expression.scores()) {
                        if (score.type() == spectra::ExpressionType::FEAR) fear = score.confidence();
                    }
                    state.fear_confidence_pct = fear;
                }
            }
        }
        emit(timestamp_us, false);
    });

    sdk.SetOnValidationStatusChanged([&](const spectra::ValidationStatus& status, int64_t timestamp_us) {
        {
            std::lock_guard<std::mutex> lock(state_mutex);
            state.validation_code = static_cast<int>(status.code);
            state.signal_valid = status.code == spectra::ValidationCode::kOk;
        }
        if (!status.hint.empty()) std::cerr << "[validation] " << status.hint << '\n';
        emit(timestamp_us, true);
    });

    sdk.SetOnError([](const spectra::SmartSpectraError& error) {
        std::cerr << "[SmartSpectra error] " << error.message << '\n';
    });

    const auto source_error = sdk.UseCamera(options.camera_index)
                                  .SetResolution(options.width, options.height)
                                  .SetFps(options.fps)
                                  .Build();
    if (!source_error.ok()) throw std::runtime_error("Camera setup failed: " + source_error.message);
    if (const auto error = sdk.Start(); !error.ok()) {
        throw std::runtime_error("SmartSpectra start failed: " + error.message);
    }

    std::cout << "SmartSpectra sensor host running; Ctrl+C to stop.\n";
    while (!g_stop_requested.load()) {
        std::this_thread::sleep_for(std::chrono::milliseconds(200));
    }
    if (const auto error = sdk.Stop(); !error.ok()) {
        std::cerr << "SmartSpectra stop failed: " << error.message << '\n';
        return 1;
    }
    return 0;
}

}  // namespace

int main(int argc, char** argv) {
    std::signal(SIGINT, HandleSignal);
    try {
        const Options options = ParseOptions(argc, argv);
        if (options.dry_run) {
            std::cout << "Configuration valid: UDP " << options.host << ':' << options.port
                      << ", camera " << options.camera_index << ". No devices opened.\n";
            return 0;
        }
        if (!options.replay_path.empty()) return RunReplay(options);
        return RunLive(options);
    } catch (const std::exception& error) {
        std::cerr << "dread-sensor-host: " << error.what() << '\n';
        return 1;
    }
}
