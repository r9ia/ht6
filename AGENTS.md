# Agent and Contributor Guide

## Mission

Build **Dread Director**, a reusable biometric AI Director SDK for horror games plus a deliberately small Unity reference demo. The SDK, not the game content, is the primary deliverable.

Read [`README.md`](README.md), [`docs/PROJECT_SCOPE.md`](docs/PROJECT_SCOPE.md), and [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) before implementing or expanding scope.

## Non-negotiable facts

1. SmartSpectra's documented C++ support does **not** include QNX. Keep SmartSpectra code in `sensor-host/` on a supported Linux host unless Presage provides an official QNX build.
2. The QNX Pi runs the Director decision node and local OpenCV DNN inference, not unsupported vendor binaries.
3. `director-core/` must compile as portable C++17 and must not include Unity, SmartSpectra, OpenCV, sockets, or QNX headers.
4. HRV confidence is zero until its 60-second window fills. Breathing confidence is zero until 30 seconds and is unreliable under talking/motion. Do not treat either as an immediate scare trigger.
5. SmartSpectra confidence values are documented as percentages (0–100), not normalized floats.
6. Preserve a replay/fake input path at every layer. Live hardware is never the only demo path.
7. No health, diagnosis, treatment, blood-pressure, or surveillance claims.

## Source-of-truth dependencies

- SmartSpectra API/payload truth: official C++ API and data-type documentation; do not invent callback signatures.
- QNX dependency truth: exact package/build listing from `oss.qnx.com`; verify OpenCV DNN availability with a compile test.
- Unity target: Unity 6 URP. This repository root is the Unity demo (`Assets/`, `Packages/`, `ProjectSettings/`); keep portable and host-specific code outside `Assets/`.

## Repository layout to maintain

```text
configs/        Designer-facing JSON profiles and schema/examples
director-core/  Pure C++17 core and deterministic unit tests
sensor-host/    Linux-only SmartSpectra adapter and capture/replay tools
qnx-host/       QNX executable, UDP transport, OpenCV DNN adapter
Assets/         Unity 6 URP reference game (this repository root)
docs/           Architecture, scope, integration/protocol notes
recordings/     Ignored local capture files; no real participant data in Git
```

## Implementation order

1. Define versioned `VitalsSample` and Director-event wire contracts.
2. Implement and unit-test pure `director-core` with recorded/synthetic sample sequences.
3. Build Unity one-room demo with fake keyboard events and HUD.
4. Add QNX UDP receiver/emitter, then validate with replay data.
5. Add Linux SmartSpectra adapter only after the standalone path works.
6. Add OpenCV DNN model only after a heuristic fallback works and the QNX port proves `opencv2/dnn.hpp`.
7. Add narration/microphone/MPU6050 only if the end-to-end MVP is stable.

## Coding conventions

### C++

- C++17 only unless a target-specific directory explicitly requires otherwise.
- Namespace: `dread`.
- Favor explicit units in identifiers: `timestamp_us`, `pulse_bpm`, `confidence_pct`.
- Keep data structures serializable and deterministic.
- No hidden global state in the Director.
- Validate all external packet fields; clamp game-state values to documented ranges.
- Tests must cover cold start, invalid signal, HRV readiness, cooldown, recovery, and dropped/reordered UDP-event handling.

### Unity C#

- Namespace: `DreadDirector`.
- Unity should consume only high-level Director messages—not SmartSpectra payloads.
- Keep gameplay effects behind `DirectorGameBridge`; keyboard fake events and UDP events must share that bridge.
- Make scene construction reproducible with an Editor/bootstrap script rather than manual-only wiring.
- Do not commit generated Unity `Library/`, `Temp/`, `Logs/`, or build output.

## Security, privacy, and secrets

- Never commit API keys, `.env` files, credentials, wallet seed phrases, private keys, signed transactions, participant recordings, raw video, raw audio, or face landmarks.
- Never send biometrics, biometric-derived scores, or player identity to Unifold, a chain, or any reward service; the TypeScript bridge receives only an opt-in gameplay achievement/opaque claim ID.
- Do not send project code, biometrics, or secrets to a third-party service without explicit approval.
- Any cloud narration must have an offline fallback and must not enter the real-time QNX loop.

## Change discipline

- Keep changes narrow and match the MVP scope.
- Before adding a package, verify platform support and pin an exact version.
- Do not initialize credentials, make production cloud changes, create commits, or push branches without explicit user authorization.
- Run targeted tests/builds after edits. Report what ran and what could not be validated.
