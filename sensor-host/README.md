# SmartSpectra sensor host

C++17 adapter for the official Presage SmartSpectra SDK. It opens the camera only in live mode, normalizes documented callback fields into the versioned `VitalsSample` JSON contract, and sends that JSON over local UDP (default `127.0.0.1:7776`). It never stores frames, landmarks, or biometric history.

## Local SDK

The SDK is intentionally not committed. Set these in the process environment:

```powershell
$env:SMARTSPECTRA_SDK_PATH = "$PWD\.local\smartspectra-sdk\windows-x64"
$env:SMARTSPECTRA_API_KEY = "<local secret>"
```

The official Windows v3.2.1 SDK requires Visual Studio 2022 C++ tools. Configure and build:

```powershell
cmake -S sensor-host -B .local/smartspectra-sdk/build/sensor-host -A x64
cmake --build .local/smartspectra-sdk/build/sensor-host --config Release
```

Put the SDK runtime on `PATH` before running:

```powershell
$env:PATH = "$env:SMARTSPECTRA_SDK_PATH\bin;$env:PATH"
.local\smartspectra-sdk\build\sensor-host\Release\dread-sensor-host.exe --dry-run
```

`--dry-run` opens no camera and sends no packets. Live mode requires visible participant consent:

```powershell
.local\smartspectra-sdk\build\sensor-host\Release\dread-sensor-host.exe --host 127.0.0.1 --port 7776
```

The adapter requests breathing, cardio, and face bundles. Confidence remains in SmartSpectra's documented `0..100` percentage units. Validation status controls `signalValid`; HRV readiness follows SDK confidence; breathing confidence is emitted as zero while talking.

## Replay fallback

Replay requires no camera or API call. The executable still links against the local SDK, but it sends each non-comment JSONL line at 5 Hz:

```powershell
.local\smartspectra-sdk\build\sensor-host\Release\dread-sensor-host.exe --replay sensor-host/fixtures/synthetic-vitals.jsonl
```

The destination is the future QNX Director input, not Unity's high-level event port. Unity continues to consume only `state`, `escalate`, `panic`, and `recovery` messages on UDP 7777.
