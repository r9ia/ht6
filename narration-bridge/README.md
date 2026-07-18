# Narration Bridge

Local presentation-only service for Dread Director. The game sends one allowlisted event label; it never sends biometrics, identity, webcam frames, audio, project code, or Director scores.

## Setup

```powershell
Copy-Item ..\.env.example ..\.env
# Add newly rotated keys to the ignored root .env only.
npm install
npm test
npm start
```

Cloud narration is disabled by default. With `ENABLE_CLOUD_NARRATION=false`, `POST /v1/narration` immediately returns a curated line selected from a per-event pool without repeating the previous response. When explicitly enabled, Gemini uses the SmartSpectra-derived Backrooms persona to produce one 10-to-15-word line; the bridge also enforces the 15-word limit locally. ElevenLabs may create a short temporary MP3 using `ELEVENLABS_MODEL_ID` (default `eleven_multilingual_v2`); if Gemini is unavailable, ElevenLabs can still voice the curated line. Unity plays returned audio from a spatial voice anchor on the monster. Any timeout/provider failure falls back to an offline subtitle and never blocks gameplay.

Microphone conversation is a separate explicit feature. Set both `ENABLE_CLOUD_NARRATION=true` and `ENABLE_MICROPHONE_CONVERSATION=true`, start the bridge, then press `V` in Play mode. Unity requests microphone permission, captures five seconds at 16 kHz, keeps the WAV only in memory, and sends it to the loopback-only `/v1/conversation` endpoint. The bridge forwards that audio to Gemini and returns optional ElevenLabs audio through the same spatial monster source. No input WAV is written to disk, but the raw clip does leave the machine for Gemini when the player presses `V`.

## API

```http
GET /health
POST /v1/narration
Content-Type: application/json

{"event":"escalation_low"}
```

Allowed events: `calibration_complete`, `escalation_low`, `escalation_high`, `panic_backoff`, `recovery`. The service binds to `127.0.0.1` by default and rejects other labels and oversized bodies.

### Opt-in microphone conversation

```http
POST /v1/conversation
Content-Type: audio/wav

<PCM16 WAV, maximum 512000 bytes>
```

This endpoint returns `403` unless `ENABLE_MICROPHONE_CONVERSATION=true`, accepts no text fields or identity metadata, and never persists the input WAV.
