// fear_detector.cpp
//
// Reads heart rate, breathing rate, blink events, and facial expression from
// a webcam via the Presage SmartSpectra SDK, turns sustained spikes into
// "fear events", prints them to the console (in a format that's easy to
// pipe into a game later), and shows a live camera + HUD window.
//
// Usage:
//   fear_detector.exe YOUR_API_KEY
//   (or set SMARTSPECTRA_API_KEY env var and omit the argument)
//
// Controls: 'q' or ESC in the video window quits. Ctrl+C also works.

#include <smartspectra/messages/metrics.h>
#include <smartspectra/smartspectra.h>
#include <smartspectra/smartspectra_config.h>

#include <opencv2/highgui.hpp>
#include <opencv2/imgproc.hpp>

#include <atomic>
#include <chrono>
#include <csignal>
#include <cstdlib>
#include <iostream>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>

#include "fear_engine.h"

namespace spectra = presage::smartspectra;

namespace {

volatile std::sig_atomic_t g_stop_requested = 0;
void HandleSignal(int) { g_stop_requested = 1; }

std::string ResolveApiKey(int argc, char** argv) {
    if (argc > 1) return argv[1];
    if (const char* key = std::getenv("SMARTSPECTRA_API_KEY")) return key;
    return {};
}

// Everything the render loop needs, written to by SDK callbacks (which fire
// on their own threads) and read by main() on the main thread.
struct SharedState {
    std::mutex mutex;
    cv::Mat latest_frame;
    std::string validation_hint;
    bool talking = false;
    std::string last_console_line = "Calibrating...";
};

// Maps the SDK's ExpressionType enum to the plain-text label FearEngine and
// the HUD expect. See cpp/docs/data-types.md in the SDK repo for the source
// of truth on these enumerator names.
std::string ExpressionTypeName(spectra::ExpressionType type) {
    switch (type) {
        case spectra::ExpressionType::ANGRY: return "ANGRY";
        case spectra::ExpressionType::CONTEMPT: return "CONTEMPT";
        case spectra::ExpressionType::DISGUST: return "DISGUST";
        case spectra::ExpressionType::FEAR: return "FEAR";
        case spectra::ExpressionType::HAPPY: return "HAPPY";
        case spectra::ExpressionType::NEUTRAL: return "NEUTRAL";
        case spectra::ExpressionType::SAD: return "SAD";
        case spectra::ExpressionType::SURPRISE: return "SURPRISE";
        default: return "UNSPECIFIED";
    }
}

void PrintFearEvent(const fear::FearEvent& ev) {
    // Deliberately simple/greppable so this can later feed a game process
    // (e.g. read stdout line-by-line, or swap this for a socket/file write).
    std::cout << "[FEAR EVENT] type=" << ev.label
              << " intensity=" << ev.intensity
              << " msg=\"" << ev.message << "\"\n";
}

void DrawHud(cv::Mat& canvas, int panel_x, const fear::FearEngine& engine,
             const SharedState& state, int fear_score) {
    auto put = [&](const std::string& text, int y, cv::Scalar color = {220, 220, 220},
                    double scale = 0.55) {
        cv::putText(canvas, text, {panel_x, y}, cv::FONT_HERSHEY_SIMPLEX, scale, color, 1,
                    cv::LINE_AA);
    };

    int y = 30;
    put("FEAR DETECTOR", y, {0, 255, 255}, 0.7);
    y += 35;

    if (engine.IsCalibrating()) {
        std::ostringstream oss;
        oss << "Calibrating baseline... " << static_cast<int>(engine.CalibrationSecondsRemaining())
            << "s left";
        put(oss.str(), y, {0, 200, 255});
        y += 30;
    }

    if (auto hr = engine.HeartRate()) {
        std::ostringstream oss;
        oss << "Heart rate: " << static_cast<int>(*hr) << " BPM";
        if (auto base = engine.HeartRateBaseline()) oss << "  (baseline " << static_cast<int>(*base) << ")";
        put(oss.str(), y);
        y += 26;
    } else {
        put("Heart rate: --", y);
        y += 26;
    }

    if (auto br = engine.BreathingRate()) {
        std::ostringstream oss;
        oss << "Breathing: " << static_cast<int>(*br) << " br/min";
        if (auto base = engine.BreathingBaseline()) oss << "  (baseline " << static_cast<int>(*base) << ")";
        put(oss.str(), y);
        y += 26;
    } else {
        put("Breathing: --", y);
        y += 26;
    }

    {
        std::ostringstream oss;
        oss << "Blink rate: " << engine.BlinkRatePerMin() << " /min";
        put(oss.str(), y);
        y += 26;
    }

    {
        std::ostringstream oss;
        oss << "Talking: " << (state.talking ? "yes" : "no");
        put(oss.str(), y);
        y += 26;
    }

    {
        std::ostringstream oss;
        oss << "Expression: " << engine.ExpressionLabel() << " ("
            << static_cast<int>(engine.ExpressionConfidence()) << "%)";
        cv::Scalar color = (engine.ExpressionLabel() == "FEAR" || engine.ExpressionLabel() == "SURPRISE")
                                ? cv::Scalar(60, 60, 255)
                                : cv::Scalar(220, 220, 220);
        put(oss.str(), y, color);
        y += 34;
    }

    // Fear meter bar.
    put("Fear meter:", y);
    y += 12;
    int bar_w = 240, bar_h = 22;
    cv::rectangle(canvas, {panel_x, y}, {panel_x + bar_w, y + bar_h}, {90, 90, 90}, 1);
    int fill_w = static_cast<int>(bar_w * (fear_score / 100.0));
    cv::Scalar fill_color(0, 255 - static_cast<int>(fear_score * 2.0 > 255 ? 255 : fear_score * 2.0),
                           std::min(255, fear_score * 3));
    cv::rectangle(canvas, {panel_x + 1, y + 1}, {panel_x + std::max(1, fill_w), y + bar_h - 1}, fill_color,
                  cv::FILLED);
    y += bar_h + 30;

    if (!state.validation_hint.empty()) {
        put("Status: " + state.validation_hint, y, {0, 165, 255});
        y += 26;
    }

    y += 10;
    put("Last event:", y, {150, 150, 150});
    y += 24;
    // Wrap the last console line across the panel width crudely.
    const int wrap_chars = 34;
    std::string msg = state.last_console_line;
    for (size_t start = 0; start < msg.size(); start += wrap_chars) {
        put(msg.substr(start, wrap_chars), y, {255, 255, 255}, 0.5);
        y += 22;
    }

    put("[q / ESC to quit]", canvas.rows - 15, {120, 120, 120}, 0.45);
}

}  // namespace

int main(int argc, char** argv) {
    std::signal(SIGINT, HandleSignal);

    const std::string api_key = ResolveApiKey(argc, argv);
    if (api_key.empty()) {
        std::cerr << "Usage: fear_detector.exe YOUR_API_KEY\n"
                  << "or set SMARTSPECTRA_API_KEY=YOUR_API_KEY\n";
        return 1;
    }

    spectra::SmartSpectraConfig config;
    config.api_key = api_key;
    config.requested_metrics = spectra::SmartSpectraConfig::BreathingMetrics();
    config.AddMetrics(spectra::SmartSpectraConfig::CardioMetrics());
    config.AddMetrics(spectra::SmartSpectraConfig::FaceMetrics());

    spectra::SmartSpectra sdk(config);

    fear::FearEngine engine;
    SharedState state;
    std::mutex console_mutex;

    // Edge-detection state for blink/talk DetectionStatus streams.
    bool prev_blink_detected = false;

    sdk.SetOnMetrics([&](const spectra::Metrics& metrics, int64_t) {
        if (metrics.has_cardio() && metrics.cardio().pulse_rate_size() > 0) {
            double bpm = metrics.cardio().pulse_rate(metrics.cardio().pulse_rate_size() - 1).value();
            engine.UpdateHeartRate(bpm);
        }

        if (metrics.has_breathing() && metrics.breathing().rate_size() > 0) {
            double bpm = metrics.breathing().rate(metrics.breathing().rate_size() - 1).value();
            engine.UpdateBreathingRate(bpm);
        }

        if (metrics.has_face()) {
            const auto& face = metrics.face();

            // blinking()/talking() are DetectionStatus streams; each call to
            // SetOnMetrics carries the samples produced since the previous
            // call, so we scan all of them (usually 0 or 1) for a rising
            // edge rather than just looking at the latest entry.
            for (const auto& status : face.blinking()) {
                if (status.detected() && !prev_blink_detected) {
                    engine.RegisterBlink(fear::FearEngine::NowSeconds());
                }
                prev_blink_detected = status.detected();
            }

            if (face.talking_size() > 0) {
                std::lock_guard<std::mutex> lock(state.mutex);
                state.talking = face.talking(face.talking_size() - 1).detected();
            }

            if (face.expression_size() > 0) {
                const auto& latest_expression = face.expression(face.expression_size() - 1);
                float best_confidence = -1.0f;
                spectra::ExpressionType best_type = spectra::ExpressionType::NEUTRAL;
                for (const auto& score : latest_expression.scores()) {
                    if (score.confidence() > best_confidence) {
                        best_confidence = score.confidence();
                        best_type = score.type();
                    }
                }
                if (best_confidence >= 0.0f) {
                    engine.UpdateExpression(ExpressionTypeName(best_type), best_confidence);
                }
            }
        }

        if (auto ev = engine.PopPendingEvent()) {
            std::lock_guard<std::mutex> lock(console_mutex);
            PrintFearEvent(*ev);
            std::lock_guard<std::mutex> state_lock(state.mutex);
            state.last_console_line = std::string(ev->message);
        }
    });

    sdk.SetOnValidationStatusChanged([&](const spectra::ValidationStatus& status, int64_t) {
        std::lock_guard<std::mutex> lock(state.mutex);
        state.validation_hint = (status.code == spectra::ValidationCode::kOk) ? "" : status.hint;
    });

    sdk.SetOnError([&](const spectra::SmartSpectraError& error) {
        std::lock_guard<std::mutex> lock(console_mutex);
        std::cerr << "[SDK ERROR] " << error.message << "\n";
    });

    // Camera preview: SDK hands us raw RGB frames, we convert to BGR for
    // OpenCV display (this conversion matches Presage's own full_example).
    sdk.SetOnVideoOutput([&](const spectra::FrameBuffer& fb, int64_t) {
        cv::Mat rgb(fb.height, fb.width, CV_8UC3, const_cast<uint8_t*>(fb.data), fb.stride_bytes);
        cv::Mat bgr;
        cv::cvtColor(rgb, bgr, cv::COLOR_RGB2BGR);
        std::lock_guard<std::mutex> lock(state.mutex);
        state.latest_frame = bgr.clone();
    });

    const auto source_error = sdk.UseCamera().SetResolution(1280, 720).SetFps(30).Build();
    if (!source_error.ok()) {
        std::cerr << "Failed to create camera source: " << source_error.message << "\n";
        return 1;
    }

    if (const auto err = sdk.Start(); !err.ok()) {
        std::cerr << "Failed to start: " << err.message << "\n";
        return 1;
    }

    std::cout << "Fear detector running. Press 'q' or ESC in the video window to quit.\n";

    const int panel_width = 300;
    cv::Mat canvas;

    while (!g_stop_requested) {
        engine.Tick();

        cv::Mat frame;
        {
            std::lock_guard<std::mutex> lock(state.mutex);
            frame = state.latest_frame.clone();
        }

        if (frame.empty()) {
            std::this_thread::sleep_for(std::chrono::milliseconds(15));
        } else {
            canvas = cv::Mat::zeros(frame.rows, frame.cols + panel_width, CV_8UC3);
            frame.copyTo(canvas(cv::Rect(0, 0, frame.cols, frame.rows)));
            {
                std::lock_guard<std::mutex> lock(state.mutex);
                DrawHud(canvas, frame.cols + 15, engine, state, engine.FearScore());
            }
            cv::imshow("Fear Detector", canvas);
        }

        int key = cv::waitKey(1) & 0xFF;
        if (key == 'q' || key == 27) break;
    }

    if (const auto err = sdk.Stop(); !err.ok()) {
        std::cerr << "Stop failed: " << err.message << "\n";
    }
    cv::destroyAllWindows();
    std::cout << "Done.\n";
    return 0;
}