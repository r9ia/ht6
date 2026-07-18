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

The generated scene uses Georgia's authored `Assets/Asset/BackroomsLikeAsset/Rooms.unity` world from `origin/georgia`, preserving its room tiles, props, colliders, lighting, and volume while replacing Georgia's player with the Dread Director controller and systems. The primitive security room and previously approved Backrooms prefab remain code-only fallbacks when that source scene is unavailable. The apparition uses the imported animated CC0 Demon by Quaternius, with the procedural creature retained as a fallback. Everything is selected and wired by the bootstrap without Inspector setup.

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

`narration-bridge/` is a local TypeScript presentation service with rotating, non-repeating response pools adapted from the `smartspectra` branch and opt-in Gemini/ElevenLabs enrichment. Normal narration accepts only allowlisted gameplay event labels, never biometrics or identity. An independently gated push-to-talk mode can accept a five-second in-memory WAV from Unity when the player presses `V`; enabling it explicitly sends that raw clip to Gemini and returns optional ElevenLabs audio through the spatial monster voice. Input audio is not written to disk, and subtitles remain the offline fallback.

```powershell
Copy-Item .env.example .env
# Add newly rotated keys locally; never reuse keys exposed in chat.
npm --prefix narration-bridge install
npm --prefix narration-bridge run build
npm --prefix narration-bridge start
```

Cloud narration is disabled unless `ENABLE_CLOUD_NARRATION=true`. The game works when the bridge or Internet is unavailable.

## Optional reward bridge

`unifold-bridge/` is a local, loopback-only TypeScript service that powers the **Night Watch Contract**: surviving longer and staying calmer (less scared) earns a sandbox stablecoin (USDC) bounty via the [Unifold](https://unifold.io) SDK. It is the only module that touches Unifold. Unity measures survival and composure locally and sends only an opaque `{version, claimId, tier}` claim — never biometrics, Director scores, identity, or a wallet. QNX is not involved.

```powershell
Copy-Item .env.example .env
# Leave the Unifold values blank to run in mock mode (no keys, funds, or network).
npm --prefix unifold-bridge install
npm --prefix unifold-bridge run build
npm --prefix unifold-bridge start
```

Rewards are mock/simulated unless `ENABLE_UNIFOLD_REWARDS=true` and a secret key, treasury account, and recipient address are configured. Live payouts use Unifold treasury outbound transfers (default Base USDC). The game works when the bridge or Internet is unavailable. See [`unifold-bridge/README.md`](unifold-bridge/README.md).

## Documentation

- [`AGENTS.md`](AGENTS.md) — implementation boundaries and contributor rules
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — Linux/QNX/Unity topology and protocols
- [`docs/PROJECT_SCOPE.md`](docs/PROJECT_SCOPE.md) — MVP priorities and exclusions
- [`docs/UNIFOLD_INTEGRATION.md`](docs/UNIFOLD_INTEGRATION.md) — sponsor integration contract
- [`docs/THIRD_PARTY_ASSETS.md`](docs/THIRD_PARTY_ASSETS.md) — optional environment/monster asset workflow

## Security and privacy

Never commit API keys, `.env`, wallet secrets, signed transactions, recordings, raw video/audio, or participant biometrics. Gemini, ElevenLabs, and Unifold remain outside the QNX real-time loop and receive no biometric-derived data.
