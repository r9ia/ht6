# Dread Director

**A biometric AI Director SDK for adaptive horror games.** This Unity 6 URP repository contains the *Dread Director: Night Watch* reference vertical slice for Hack the 6ix 2026.

> This is a game-adaptation prototype, not a medical device. It makes no diagnosis, treatment, blood-pressure, or surveillance claims.

## Current Unity demo

The demo is generated reproducibly by `Assets/Editor/DreadDirectorSceneBootstrap.cs`; no manual GameObject wiring is required.

1. In Unity, use **Dread Director → Build Night Watch Scene**.
2. Open `Assets/Scenes/DreadDirectorNightWatch.unity` if Unity does not open it automatically.
3. Enter Play mode. Move with `WASD`, look with the mouse, and press `Esc` to release/re-lock the cursor.
4. Use `1` for low escalation, `2` for high escalation, `3` for panic/back-off, and `4` for recovery/re-arm.
5. QNX-compatible high-level JSON can be sent to UDP port `7777`; fake input and UDP share `DirectorGameBridge`.

The generated scene discovers and uses the approved Backrooms environment imported from `origin/georgia`; the primitive room remains a guaranteed fallback. The apparition uses the imported animated CC0 Demon by Quaternius, with the procedural creature retained as a fallback. Both are selected and wired by the bootstrap without Inspector setup.

## Honest deployment topology

```text
Linux sensor host
  Webcam + Presage SmartSpectra
  → normalized VitalsSample over local UDP
QNX Raspberry Pi
  portable C++17 Director + optional local OpenCV DNN
  → high-level Director events over local UDP
Unity laptop
  UDP receiver → DirectorGameBridge → scene effects and HUD
```

SmartSpectra is documented for Linux/macOS/Windows, **not QNX**. It stays on the Linux sensor host. The portable QNX decision core must not include Unity, SmartSpectra, OpenCV, socket, or QNX headers.

Unity receives high-level messages only:

```json
{ "version": 1, "type": "state", "arousal": 0.42, "sustainedStress": 0.10, "composure": -0.08 }
{ "version": 1, "type": "escalate", "intensity": 0.76 }
{ "version": 1, "type": "panic", "intensity": 1.0 }
{ "version": 1, "type": "recovery", "intensity": 0.0 }
```

## Optional narration bridge

`narration-bridge/` is a local TypeScript presentation service with an offline line bank and opt-in Gemini/ElevenLabs enrichment. It accepts only allowlisted event labels, never biometrics or identity.

```powershell
Copy-Item .env.example .env
# Add newly rotated keys locally; never reuse keys exposed in chat.
npm --prefix narration-bridge install
npm --prefix narration-bridge run build
npm --prefix narration-bridge start
```

Cloud narration is disabled unless `ENABLE_CLOUD_NARRATION=true`. The game works when the bridge or Internet is unavailable.

## Documentation

- [`AGENTS.md`](AGENTS.md) — implementation boundaries and contributor rules
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — Linux/QNX/Unity topology and protocols
- [`docs/PROJECT_SCOPE.md`](docs/PROJECT_SCOPE.md) — MVP priorities and exclusions
- [`docs/UNIFOLD_INTEGRATION.md`](docs/UNIFOLD_INTEGRATION.md) — sponsor integration contract
- [`docs/THIRD_PARTY_ASSETS.md`](docs/THIRD_PARTY_ASSETS.md) — optional environment/monster asset workflow

## Security and privacy

Never commit API keys, `.env`, wallet secrets, signed transactions, recordings, raw video/audio, or participant biometrics. Gemini, ElevenLabs, and Unifold remain outside the QNX real-time loop and receive no biometric-derived data.
