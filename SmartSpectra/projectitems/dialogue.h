// dialogue.h
// Pools of "creepy narrator" lines, grouped by the biometric trigger that
// fires them. Pure data -- edit/extend freely, no logic lives here.
//
// Each pool is picked from at random (without repeating the immediately
// previous line) by FearEngine, see fear_engine.h.

#pragma once

#include <array>
#include <string_view>

namespace dialogue {

// Triggered when heart rate rises well above the player's calibrated baseline.
inline constexpr std::array<std::string_view, 8> kHeartRateSpike = {
    "Your heart's giving you away.",
    "Something in here can hear that pulse.",
    "That's not exercise. That's fear.",
    "Slow down. It only finds what's racing.",
    "I can feel it beating from here.",
    "Every heartbeat is a light in the dark. Yours is very bright right now.",
    "It knows exactly how scared you are. So do I.",
    "Keep that up and your heart will bring it to you.",
};

// Triggered when the player's breathing rate spikes / turns ragged.
inline constexpr std::array<std::string_view, 6> kBreathingElevated = {
    "Breathe quieter. It's listening for that too.",
    "Your breath is louder than your footsteps now.",
    "Short breaths. Shallow breaths. It always starts that way.",
    "Hold it if you can. Just for a moment. It helps. It doesn't.",
    "That ragged breathing carries further than you'd like.",
    "In. Out. In. Out. You're forgetting how.",
};

// Triggered on elevated blink rate (nervous/rapid blinking).
inline constexpr std::array<std::string_view, 6> kBlinkingRapid = {
    "Careful. Something changes every time you blink.",
    "Blink less. You might not like what moves in the gap.",
    "You're blinking like there's something you don't want to see.",
    "Every blink is half a second it gets closer.",
    "Your eyes keep closing. It only needs one of those moments.",
    "Stop blinking so much. It's starting to notice the rhythm.",
};

// Triggered when the facial-expression model's top class is FEAR.
inline constexpr std::array<std::string_view, 6> kExpressionFear = {
    "There it is. That's the face it was waiting for.",
    "You can't hide that expression from a camera.",
    "That's fear. Real fear. Good. It prefers that.",
    "Your face just told on you.",
    "I've seen that look before. It doesn't end well for the ones who make it.",
    "Something about your expression just changed the room.",
};

// Triggered when the facial-expression model's top class is SURPRISE.
inline constexpr std::array<std::string_view, 5> kExpressionSurprise = {
    "Didn't expect that, did you?",
    "That reaction was worth recording.",
    "Surprise is just fear that hasn't decided what to do yet.",
    "There -- did you see it too, or just feel it?",
    "Your face just asked a question your mouth didn't.",
};

// Ambient / calibration lines -- shown while establishing baseline, not tied
// to a specific spike. Optional flavor text, safe to ignore.
inline constexpr std::array<std::string_view, 4> kCalibrating = {
    "Hold still. I'm learning what calm looks like on you.",
    "This part is quiet. Enjoy it.",
    "I need a baseline. Try not to give me one early.",
    "Measuring your ordinary. It won't last.",
};

}  // namespace dialogue