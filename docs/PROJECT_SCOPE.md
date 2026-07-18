# Project Scope: Dread Director

## Product statement

Dread Director is a reusable C++17 biometric pacing library for horror games. It accepts normalized player-state inputs and returns game-facing pacing decisions. A Unity horror scene is a reference implementation, not the primary product.

## Success criterion

During a live demo, a judge can observe a visible causal chain:

```text
player expression / pulse changes
→ validated VitalsSample
→ QNX Director updates PlayerState
→ Director selects pacing response
→ Unity game reacts at a matching intensity
```

The debug HUD must show the input readiness/signal quality and state values so the adaptation is observable rather than asserted.

## Must-have MVP

### SDK and QNX decision node

- Portable C++17 `director-core` with no Unity and no SmartSpectra headers.
- `VitalsSample`, `PlayerState`, readiness flags, baseline calibration, arousal, sustained stress, and composure trend.
- `Director` API: `ingest`, `shouldEscalate`, `nextEventIntensity`, `pickTarget`, callbacks for escalation/panic/recovery.
- Designer-owned JSON profile for thresholds and pacing.
- QNX executable receives local vitals messages over UDP and emits Director messages over UDP.
- Use OpenCV's **DNN module** with a tiny ONNX model for local Director inference if the QNX OpenCV port includes `opencv2/dnn.hpp`; preserve a deterministic heuristic fallback.

### Sensor node

- SmartSpectra C++ runs only on a documented supported Linux host.
- Webcam input requests only the minimum useful metrics: pulse, face expression, HRV, and validation status.
- Adapter converts callback payloads to `VitalsSample` and sends packets over the isolated local network.
- Sensor validation gates all state integration: no face, poor framing, too dark/bright, or low frame rate must be surfaced in the HUD.

### Unity reference demo

- Unity 6 URP, one small night-watch/security-room scene.
- A 60-second diegetic calibration sequence: player is still, webcam can see their face, baseline pulse and HRV windows warm up.
- Two escalating scare effects: light/ambient manipulation and a single apparition + sting.
- A recovery beat after panic; no constant punishment.
- Keyboard-driven fake Director mode so game development and rehearsals do not depend on hardware.
- UDP receiver that replaces fake events with QNX events without altering game-effect code.
- Debug HUD: arousal, sustained stress, composure, pulse/HRV readiness, validation status, latest Director action.

### Demo resilience

- Record and replay a valid `VitalsSample` stream.
- Have the replay mode ready if booth lighting, Wi-Fi, camera, or SmartSpectra fails.
- Bring a webcam, USB extension, and a small face-facing LED light. Seat the player 50–80 cm from camera and keep the scene's real-world demo area adequately lit.

## Unifold stablecoin integration

This is a committed sponsor-track feature, implemented **after the local state → QNX Director → Unity effect loop works**. The track requires an end-to-end use of the Unifold SDK for stablecoin deposits or payments; an on-chain-reward game is an accepted example, but a cosmetic “claim” that does not use Unifold is not sufficient.

### Required demo flow

1. Unity creates an opaque game session/achievement ID after the player begins or completes the Night Watch loop. It contains no biometrics or identity.
2. Unity opens or calls a local/browser-based **TypeScript Unifold bridge**. The bridge owns the actual Unifold SDK integration because the documented SDK languages are TypeScript, React Native, Swift, and Kotlin—not Unity C#.
3. The bridge presents one clear sponsor-supported sandbox stablecoin deposit/payment flow, including amount/network selection only where the Unifold docs require it.
4. The bridge returns `completed`, `pending`, `cancelled`, or `failed` status to Unity. Unity displays a polished success/error state and may unlock the related game reward/achievement only after the documented flow succeeds.
5. The team demonstrates the real sandbox integration live and records a fallback video only as a contingency; mock-only checkout screens do not qualify.
6. The exact SDK API, payment method, network, and reward semantics must follow the password-protected Unifold docs and supplied sandbox/API keys. Do not invent an SDK interface, custom settlement logic, or an unsupported Solana dependency.

### Privacy and security boundary

- Unifold receives only the minimum payment/deposit context and an opaque gameplay session/claim ID if needed. **Never send pulse, HRV, facial expression, audio, raw video, names, wallet secrets, or a biometric-derived score to Unifold or any chain.**
- Payment/reward eligibility is based on an explicit gameplay milestone, not on “who was least scared,” a medical inference, or any health-like score.
- The QNX node never connects to Unifold, a wallet, blockchain RPC, or the Internet. It only emits local gameplay state/events.
- Unity never embeds Unifold API secrets. The TypeScript bridge follows Unifold’s documented client/server and key-handling model.
- Any wallet interaction, deposit, or payment requires explicit player action and must default to the supplied sandbox/test environment unless the Unifold team authorizes otherwise.
- A cancelled or failed payment must not interrupt core gameplay; it only affects the optional sponsor integration/reward state.

## Should-have

- ElevenLabs narration driven by Director state, using a small curated line bank and a non-blocking fallback clip.
- Microphone loudness as an **optional** local gameplay input (scream/noise meter), never a reason to store voice recordings.
- MPU6050 weapon/controller movement as an optional combat or interaction input.
- A small two-player/replay demonstration for `pickTarget`.
- Measured QNX timing/jitter visualization for the sensor-to-decision loop.

## Stretch only

- LLM-generated narration (Gemini or equivalent) behind an explicit network flag, with prewritten offline fallback lines.
- Eye/landmark-driven monster spawn targeting.
- A richer monster/level, combat system, procedural environments, or backrooms content.

## Explicitly out of scope for the hackathon MVP

- Medical measurement claims, health advice, diagnosis, treatment, or blood-pressure estimation.
- Claiming SmartSpectra is running on QNX without an official supported build.
- Cloud dependency in the QNX Director decision loop.
- Storing raw camera frames, face landmarks, audio, biometric history, API keys, wallet secrets, or player identities in Git.
- Making gameplay inaccessible or coercive: provide a pause/exit control, avoid forcing loud noise, and use a clearly visible consent/calibration start screen.
- Deploying a custom Solana program, custodying funds, issuing a real-value financial product, or creating payments/rewards outside Unifold's documented flow.

## Input and integration prioritization

| Input / integration | Role | Demo priority | Notes |
| --- | --- | --- | --- |
| Facial fear/expression | Fast arousal signal | Must-have | No warm-up; use when face validation is good. |
| Pulse rate | Fast arousal signal | Must-have | Compare against player baseline, not an absolute target. |
| HRV / Baevsky | Slow sustained-stress signal | Must-have after warm-up | Confidence is zero until the 60-second window completes. |
| Breathing | Optional confirmation | Do not trigger on it | Motion/talking/flinching makes it unreliable. |
| Unifold stablecoin payment/deposit | Opt-in game achievement/payment companion | Sponsor-track feature | Actual TypeScript SDK flow; keep biometrics and QNX out. |
| Microphone loudness | Optional game mechanic | Should-have | Use a local RMS meter; do not retain recordings. |
| MPU6050 weapon tracking | Optional input | Should-have | Do not make it required for the first playable loop. |
| LLM narrator | Presentation layer | Stretch | Must never block or decide the real-time loop. |

## Team focus proposal

| Workstream | Primary owner | First deliverable |
| --- | --- | --- |
| SmartSpectra sensor adapter + state data | Karan | Live or replayed `VitalsSample` UDP stream |
| QNX Director + OpenCV DNN | Karan | QNX receives samples and emits Director decisions |
| Unity scene + controls | Georgia / Komali | One-room playable fake-Director demo |
| Unifold TypeScript bridge | Karan | Sandbox payment/deposit result returned to Unity from the actual SDK |
| Audio / narration / microphone | Aditi / Komali | Curated sting and optional narration fallback |
| Sensor/hardware integration | Team | Camera, LED, Pi network, replay fallback |

Ownership is a starting point only; prioritize an end-to-end working path over component completeness.

## Demo script

1. Explain: “This is a reusable AI Director, not a one-off game mechanic.”
2. Player accepts the demo and begins calibration.
3. HUD shows face/pulse readiness; HRV appears once warmed.
4. A subtle event happens; the game observes arousal/trend.
5. When the player settles, the Director schedules an intensity-scaled scare.
6. If panic is detected, the game backs off; after recovery, it re-arms.
7. Show the QNX node / timing HUD and state that OpenCV DNN inference is local.
8. On session completion, show the optional Unifold sandbox payment/deposit companion and its success/cancel/error UI; explain that it receives only an opaque achievement ID, never biometric data.
9. Mention replay fallback only if needed; never fake a “live” run.
