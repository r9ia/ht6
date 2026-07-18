# Architecture

## Deployment topology

```text
┌──────────────────────── Linux sensor host ────────────────────────┐
│ Webcam → SmartSpectra C++ SDK → SmartSpectra adapter              │
│           OnMetrics / Validation callbacks → normalized sample     │
└───────────────────────────────┬───────────────────────────────────┘
                                │ local UDP only; flat versioned payload
                                ▼
┌──────────────────── QNX Raspberry Pi ────────────────────────────┐
│ UDP receiver → director-core (C++17) → OpenCV DNN / heuristic     │
│                PlayerState            Director events              │
└───────────────────────────────┬───────────────────────────────────┘
                                │ local UDP JSON (or compact binary later)
                                ▼
┌──────────────────── Unity reference demo ─────────────────────────┐
│ DirectorUdpReceiver → DirectorGameBridge → lights/audio/threat/HUD│
└───────────────────────────────────────────────────────────────────┘
```

## Why this is split

The documented SmartSpectra C++ SDK targets Linux, macOS, and Windows—not QNX. Its vendor adapter must therefore stay outside the portable core and QNX target. The split is an explicit architecture boundary, not a workaround to conceal a compatibility issue.

The QNX process owns the Director's real-time decision loop and local model inference. It should remain functional with replayed or simulated samples even if the Linux sensor host fails.

## Layers

### 1. State layer (`director-core`)

Consumes only a project-defined `VitalsSample` POD. Its output is `PlayerState`:

```cpp
struct PlayerState {
  float arousal;          // 0..1, fast: face fear + pulse above baseline
  float sustainedStress;  // 0..1, slow: Baevsky/HRV when ready
  float composure;        // negative recovering, positive escalating
  Readiness readiness;    // pulse / expression / HRV / breathing / signal validity
  float baselinePulse;
  int64_t lastUpdateUs;
};
```

Key rules:

- `signalValid == false` means do not integrate that sample.
- Face expression and pulse are usable during cold start.
- HRV is unready until SmartSpectra confidence becomes positive after its full 60-second window.
- Breathing is confirmation only. Never use it as the primary trigger because jump-scare motion and talking can invalidate it.
- Every smoothing, threshold, and time window must be configured or documented.

### 2. Decision layer (`director-core`)

Game-facing API target:

```cpp
namespace dread {
class Director {
public:
  const PlayerState& ingest(const VitalsSample& sample);
  bool shouldEscalate() const;
  float nextEventIntensity() const;  // 0..1
  int pickTarget(const std::vector<PlayerState>& players) const;

  DirectorEvents events; // onEscalate, onPlayerBroke, onRecoveryDetected
};
}
```

It should create relief after high stress and avoid back-to-back events. The next scare is normally scheduled when the player has begun to settle, not while they are already overwhelmed.

### 3. Integration layer

| Component | Owns | Must not own |
| --- | --- | --- |
| `sensor-host` | SmartSpectra headers/callbacks, camera startup, packet serialization | Director pacing policy or Unity logic |
| `qnx-host` | packet receiver, QNX scheduling, OpenCV DNN invocation, event packet emission | vendor SDK headers or Unity assets |
| `unity-demo` | visuals, audio, scene pacing effects, HUD, test fake events | raw biometric algorithms |

## Transport contract

Start with JSON UDP because it is inspectable in a hackathon. Include a protocol version and timestamps. Upgrade to a packed binary form only if actual profiling says it is necessary.

### Sensor → QNX

```json
{
  "version": 1,
  "type": "vitals",
  "timestampUs": 0,
  "signalValid": true,
  "validationCode": 0,
  "pulseBpm": 72.4,
  "pulseConfidence": 87.0,
  "fear": 12.0,
  "baevsky": 0.0,
  "hrvConfidence": 0.0,
  "breathingBpm": 0.0,
  "breathingConfidence": 0.0,
  "talking": false
}
```

Confidence values follow SmartSpectra's documented **0–100 percentage** convention. Do not silently interpret them as 0–1.

### QNX → Unity

```json
{ "version": 1, "type": "state", "arousal": 0.42, "sustainedStress": 0.10, "composure": -0.08, "readiness": "pulse,face" }
{ "version": 1, "type": "escalate", "intensity": 0.76 }
{ "version": 1, "type": "panic", "intensity": 1.0 }
{ "version": 1, "type": "recovery", "intensity": 0.0 }
```

Messages must be idempotent or carry an event ID before the final demo. UDP may drop or reorder packets; state packets may be sent frequently, but scare events require deduplication.

## QNX and OpenCV DNN

Verify the exact `oss.qnx.com` package/build contains OpenCV DNN before claiming it. A valid compile-time proof is availability of:

```cpp
#include <opencv2/dnn.hpp>
```

The local model should be deliberately tiny—e.g., an ONNX MLP mapping `arousal`, `sustainedStress`, `composure`, and readiness values to escalation probability/intensity. A deterministic heuristic must remain the fallback so unavailable model assets never break the demo.

## Non-blocking external services

ElevenLabs, Gemini, and any network service live at the Unity/presentation edge. They may enrich narration but may not block `Director::ingest`, sensor processing, or QNX decision timing. The game must have pre-recorded or text-only fallback behavior.

## Unifold stablecoin integration boundary

The Unifold sponsor feature is a post-session/start-session TypeScript/web integration, not a biometric, Unity-C# SDK, or QNX capability:

```text
QNX Director → Unity game session/achievement
                        │ opaque ID only; no biometric payload
                        ▼
         TypeScript/web Unifold bridge → documented sandbox deposit/payment flow
                        │
                        ▼
              completion / pending / cancelled / failure status → Unity UI
```

- The supplied Unifold SDK must run in a documented supported language; for this Unity demo, use a TypeScript companion/web bridge. Do not claim that C# directly uses the Unifold SDK.
- The bridge invokes only the password-protected Unifold documentation’s approved SDK flow. Its API, supported chains, payment methods, network, and callback shape remain unimplemented until the sponsor supplies docs, API keys, and sandbox access.
- `game_session_id` / `achievement_id` is opaque and non-biometric. It must contain no vitals, expressions, raw sensor data, participant name, wallet secret, or biometric-derived score.
- The QNX node has no wallet, private key, Unifold SDK, RPC endpoint, or Internet dependency. It is never a payment or blockchain client.
- Unity opens the bridge or receives its local result; it does not hold Unifold secrets. Follow Unifold’s prescribed client/server key model.
- Payment/deposit authorization must be explicit and player initiated. Cancellation, unavailable wallet, and API/network failures must produce an understandable Unity state without changing the Director loop or blocking the demo.
- Do not claim Solana support unless it appears in the supplied Unifold sandbox/docs. Use Unifold’s multi-chain handling rather than bespoke wallet, chain, or settlement logic.

## Privacy and demo safety

- Do not record raw video or audio by default.
- Keep biometric transport on the local demo network.
- Store only consented, anonymous, short-lived replay fixture data if it is needed for reliability.
- Never make medical claims from pulse, HRV, facial expression, or breathing data.
- Keep a visible opt-out/exit route and do not require participants to scream or disclose personal data.
- Never commit wallet seed phrases, private keys, signed transactions, Unifold credentials, or participant identifiers.
