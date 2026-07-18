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

Cloud narration is disabled by default. With `ENABLE_CLOUD_NARRATION=false`, `POST /v1/narration` immediately returns a curated offline line. When explicitly enabled, Gemini may replace the text and ElevenLabs may create a short temporary MP3. Any timeout/provider failure falls back to the curated line.

## API

```http
GET /health
POST /v1/narration
Content-Type: application/json

{"event":"escalation_low"}
```

Allowed events: `calibration_complete`, `escalation_low`, `escalation_high`, `panic_backoff`, `recovery`. The service binds to `127.0.0.1` by default and rejects other labels and oversized bodies.
