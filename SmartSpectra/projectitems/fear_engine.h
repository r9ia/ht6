// fear_engine.h
// Converts raw SmartSpectra signals (pulse, breathing, blink, expression)
// into discrete "fear events" with dialogue lines, cooldowns, and a rolling
// 0-100 fear score meant to eventually feed a game.
//
// This class only reasons about numbers you hand it via Update*() calls; it
// does not touch the SDK directly, so it's easy to unit-test or swap inputs.

#pragma once

#include <algorithm>
#include <chrono>
#include <cstdio>
#include <deque>
#include <optional>
#include <random>
#include <string>
#include <string_view>
#include <vector>

#include "dialogue.h"

namespace fear {

using Clock = std::chrono::steady_clock;

enum class EventType {
    kHeartRateSpike,
    kBreathingElevated,
    kBlinkingRapid,
    kExpressionFear,
    kExpressionSurprise,
};

struct FearEvent {
    EventType type;
    std::string_view label;   // machine-friendly tag, e.g. "heart_rate"
    std::string_view message; // the dialogue line
    float intensity;          // 0..1, roughly "how far past threshold"
};

class FearEngine {
public:
    // How long a signal must stay elevated above baseline before we trust it.
    static constexpr double kHeartRateDeltaThreshold = 14.0;   // BPM above baseline
    static constexpr double kBreathingDeltaThreshold = 6.0;    // breaths/min above baseline
    static constexpr int kBlinkBurstThreshold = 5;              // blinks within window
    static constexpr double kBlinkWindowSeconds = 15.0;
    static constexpr float kExpressionConfidenceThreshold = 35.0f; // percent, per data-types.md

    static constexpr double kBaselineCalibrationSeconds = 20.0;
    static constexpr double kEventCooldownSeconds = 8.0;

    FearEngine() : rng_(std::random_device{}()) {}

    bool IsCalibrating() const {
        return Elapsed(start_time_) < kBaselineCalibrationSeconds;
    }

    double CalibrationSecondsRemaining() const {
        return std::max(0.0, kBaselineCalibrationSeconds - Elapsed(start_time_));
    }

    // Feed the latest pulse rate (BPM). Call each time SetOnMetrics reports a
    // new cardio sample.
    void UpdateHeartRate(double bpm) {
        last_heart_rate_ = bpm;
        if (IsCalibrating()) {
            heart_rate_baseline_samples_.push_back(bpm);
            return;
        }
        EnsureBaselineComputed();
        if (!heart_rate_baseline_) return;

        double delta = bpm - *heart_rate_baseline_;
        if (delta >= kHeartRateDeltaThreshold) {
            TryFire(EventType::kHeartRateSpike, "heart_rate",
                    dialogue::kHeartRateSpike,
                    static_cast<float>(std::min(1.0, delta / (kHeartRateDeltaThreshold * 2.5))));
        }
    }

    // Feed the latest breathing rate (breaths/min).
    void UpdateBreathingRate(double bpm) {
        last_breathing_rate_ = bpm;
        if (IsCalibrating()) {
            breathing_baseline_samples_.push_back(bpm);
            return;
        }
        EnsureBaselineComputed();
        if (!breathing_baseline_) return;

        double delta = bpm - *breathing_baseline_;
        if (delta >= kBreathingDeltaThreshold) {
            TryFire(EventType::kBreathingElevated, "breathing",
                    dialogue::kBreathingElevated,
                    static_cast<float>(std::min(1.0, delta / (kBreathingDeltaThreshold * 2.5))));
        }
    }

    // Call once per detected blink *onset* (rising edge), with the current
    // timestamp in seconds since engine construction (use NowSeconds()).
    void RegisterBlink(double now_seconds) {
        blink_times_.push_back(now_seconds);
        while (!blink_times_.empty() &&
               now_seconds - blink_times_.front() > kBlinkWindowSeconds) {
            blink_times_.pop_front();
        }
        last_blink_rate_per_min_ =
            static_cast<int>(blink_times_.size() * (60.0 / kBlinkWindowSeconds));

        if (static_cast<int>(blink_times_.size()) >= kBlinkBurstThreshold) {
            TryFire(EventType::kBlinkingRapid, "blinking", dialogue::kBlinkingRapid,
                    static_cast<float>(std::min(
                        1.0, static_cast<double>(blink_times_.size()) / (kBlinkBurstThreshold * 2.0))));
        }
    }

    // Feed the dominant expression label ("FEAR", "SURPRISE", ...) and its
    // confidence in percent [0, 100].
    void UpdateExpression(std::string_view dominant_label, float confidence_percent) {
        last_expression_label_ = std::string(dominant_label);
        last_expression_confidence_ = confidence_percent;
        if (confidence_percent < kExpressionConfidenceThreshold) return;

        if (dominant_label == "FEAR") {
            TryFire(EventType::kExpressionFear, "expression_fear", dialogue::kExpressionFear,
                    confidence_percent / 100.0f);
        } else if (dominant_label == "SURPRISE") {
            TryFire(EventType::kExpressionSurprise, "expression_surprise",
                    dialogue::kExpressionSurprise, confidence_percent / 100.0f);
        }
    }

    // Pops the most recent event since the last call, if any. Intended to be
    // drained once per frame by the console/HUD layer.
    std::optional<FearEvent> PopPendingEvent() {
        if (!pending_) return std::nullopt;
        auto ev = *pending_;
        pending_.reset();
        return ev;
    }

    // Rolling 0-100 "fear score" for a HUD meter. Purely presentational --
    // decays over time, jumps on events.
    int FearScore() const { return fear_score_; }

    // --- Read-only telemetry for the HUD panel ---
    std::optional<double> HeartRate() const { return last_heart_rate_; }
    std::optional<double> BreathingRate() const { return last_breathing_rate_; }
    std::optional<double> HeartRateBaseline() const { return heart_rate_baseline_; }
    std::optional<double> BreathingBaseline() const { return breathing_baseline_; }
    int BlinkRatePerMin() const { return last_blink_rate_per_min_; }
    const std::string& ExpressionLabel() const { return last_expression_label_; }
    float ExpressionConfidence() const { return last_expression_confidence_; }

    static double NowSeconds() {
        static const auto epoch = Clock::now();
        return std::chrono::duration<double>(Clock::now() - epoch).count();
    }

    // Call once per rendered frame so the fear score can decay smoothly even
    // when no new event has fired.
    void Tick() {
        double now = Elapsed(start_time_);
        double dt = now - last_tick_seconds_;
        last_tick_seconds_ = now;
        if (dt <= 0) return;
        fear_score_ = std::max(0, fear_score_ - static_cast<int>(dt * 4.0)); // decay ~4/sec
    }

private:
    template <size_t N>
    void TryFire(EventType type, std::string_view label,
                 const std::array<std::string_view, N>& pool, float intensity) {
        double now = Elapsed(start_time_);
        double& last_fired = cooldowns_[static_cast<size_t>(type)];
        if (now - last_fired < kEventCooldownSeconds) return;
        last_fired = now;

        std::string_view line = PickLine(type, pool);
        pending_ = FearEvent{type, label, line, intensity};
        fear_score_ = std::min(100, fear_score_ + static_cast<int>(20 + intensity * 40));
    }

    template <size_t N>
    std::string_view PickLine(EventType type, const std::array<std::string_view, N>& pool) {
        size_t idx = static_cast<size_t>(type);
        std::uniform_int_distribution<size_t> dist(0, N - 1);
        size_t choice;
        do {
            choice = dist(rng_);
        } while (N > 1 && choice == last_line_index_[idx]);
        last_line_index_[idx] = choice;
        return pool[choice];
    }

    void EnsureBaselineComputed() {
        if (!heart_rate_baseline_ && !heart_rate_baseline_samples_.empty()) {
            heart_rate_baseline_ = Median(heart_rate_baseline_samples_);
        }
        if (!breathing_baseline_ && !breathing_baseline_samples_.empty()) {
            breathing_baseline_ = Median(breathing_baseline_samples_);
        }
    }

    static double Median(std::vector<double> v) {
        std::sort(v.begin(), v.end());
        return v[v.size() / 2];
    }

    static double Elapsed(Clock::time_point since) {
        return std::chrono::duration<double>(Clock::now() - since).count();
    }

    Clock::time_point start_time_ = Clock::now();
    double last_tick_seconds_ = 0.0;

    std::vector<double> heart_rate_baseline_samples_;
    std::vector<double> breathing_baseline_samples_;
    std::optional<double> heart_rate_baseline_;
    std::optional<double> breathing_baseline_;

    std::optional<double> last_heart_rate_;
    std::optional<double> last_breathing_rate_;
    int last_blink_rate_per_min_ = 0;
    std::string last_expression_label_ = "NEUTRAL";
    float last_expression_confidence_ = 0.0f;

    std::deque<double> blink_times_;

    std::array<double, 5> cooldowns_ = {-1000, -1000, -1000, -1000, -1000};
    std::array<size_t, 5> last_line_index_ = {SIZE_MAX, SIZE_MAX, SIZE_MAX, SIZE_MAX, SIZE_MAX};
    std::optional<FearEvent> pending_;
    int fear_score_ = 0;

    std::mt19937 rng_;
};

}  // namespace fear