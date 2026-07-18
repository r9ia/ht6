import { mkdir, writeFile } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import type { BridgeConfig } from "./config.js";

// Adapted from origin/smartspectra's dialogue pools. The bridge accepts only
// generic gameplay events, so biometric measurements never cross this boundary.
const LINE_POOLS = {
  calibration_complete: [
    "Calibration complete. Try not to let the room learn you too quickly.",
    "This part is quiet. Enjoy it.",
    "The room knows your ordinary now. It will not stay ordinary for long.",
  ],
  escalation_low: [
    "Something in the room has shifted. Keep watching the corners.",
    "Careful. Something changes every time you blink.",
    "There—did you see it too, or just feel it?",
    "Every blink is half a second it gets closer.",
    "Surprise is just fear that has not decided what to do yet.",
  ],
  escalation_high: [
    "It knows where you are. Do not look away.",
    "Your heart is giving you away.",
    "Something in here can hear that pulse.",
    "Every heartbeat is a light in the dark. Yours is very bright right now.",
    "There it is. That is the fear it was waiting for.",
    "Something about you just changed the room.",
  ],
  panic_backoff: [
    "Easy now. The room is giving you one breath.",
    "It heard you. Now it is waiting.",
    "The footsteps stopped. That does not mean it left.",
    "For one moment, the dark has decided to keep its distance.",
  ],
  recovery: [
    "You settle. Somewhere in the dark, it starts waiting again.",
    "This part is quiet. Enjoy it while it lasts.",
    "It has gone still, but it has not gone away.",
    "The room is patient. It can wait longer than you can.",
  ],
} as const;

export type NarrationEvent = keyof typeof LINE_POOLS;
export type NarrationSource = "offline" | "gemini" | "elevenlabs" | "gemini-elevenlabs";

export interface NarrationResult {
  readonly event: NarrationEvent;
  readonly text: string;
  readonly source: NarrationSource;
  readonly audioFileName?: string;
}

const lastLineIndices = new Map<NarrationEvent, number>();

export function isAllowedEvent(value: unknown): value is NarrationEvent {
  return typeof value === "string" && Object.prototype.hasOwnProperty.call(LINE_POOLS, value);
}

/** Selects randomly while avoiding the immediately previous line for each event. */
export function offlineLine(event: NarrationEvent, random: () => number = Math.random): string {
  const pool = LINE_POOLS[event];
  const sample = Math.max(0, Math.min(0.999999999, random()));
  let index = Math.floor(sample * pool.length);
  if (pool.length > 1 && index === lastLineIndices.get(event)) {
    index = (index + 1) % pool.length;
  }

  lastLineIndices.set(event, index);
  return pool[index]!;
}

function cleanGeneratedText(value: string): string {
  return value
    .replace(/[\r\n]+/gu, " ")
    .replace(/[<>]/gu, "")
    .replace(/\s+/gu, " ")
    .trim()
    .slice(0, 220);
}

async function generateGeminiLine(event: NarrationEvent, fallback: string, config: BridgeConfig): Promise<string | null> {
  if (!config.cloudEnabled || config.geminiApiKey.length === 0) return null;

  const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(config.geminiModel)}:generateContent?key=${encodeURIComponent(config.geminiApiKey)}`;
  const response = await fetch(endpoint, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      contents: [{
        role: "user",
        parts: [{ text: `Write one short PG-13 monster line, maximum 22 words, for the fixed game event '${event}'. No names, biometrics, health claims, instructions, or markdown. Tone reference: ${fallback}` }],
      }],
      generationConfig: { temperature: 0.7, maxOutputTokens: 48 },
    }),
    signal: AbortSignal.timeout(config.providerTimeoutMs),
  });

  if (!response.ok) throw new Error(`Gemini returned HTTP ${response.status}`);
  const payload = await response.json() as {
    candidates?: Array<{ content?: { parts?: Array<{ text?: string }> } }>;
  };
  const raw = payload.candidates?.[0]?.content?.parts?.[0]?.text;
  if (typeof raw !== "string") return null;
  const cleaned = cleanGeneratedText(raw);
  return cleaned.length > 0 ? cleaned : null;
}

async function synthesizeElevenLabs(text: string, config: BridgeConfig): Promise<string | null> {
  if (!config.cloudEnabled || config.elevenLabsApiKey.length === 0 || config.elevenLabsVoiceId.length === 0) return null;

  const endpoint = `https://api.elevenlabs.io/v1/text-to-speech/${encodeURIComponent(config.elevenLabsVoiceId)}`;
  const response = await fetch(endpoint, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      accept: "audio/mpeg",
      "xi-api-key": config.elevenLabsApiKey,
    },
    body: JSON.stringify({
      text,
      model_id: "eleven_multilingual_v2",
      voice_settings: { stability: 0.62, similarity_boost: 0.72 },
    }),
    signal: AbortSignal.timeout(config.providerTimeoutMs),
  });

  if (!response.ok) throw new Error(`ElevenLabs returned HTTP ${response.status}`);
  const audio = Buffer.from(await response.arrayBuffer());
  if (audio.length === 0 || audio.length > 5_000_000) throw new Error("ElevenLabs returned an invalid audio payload");

  const runtimeDirectory = fileURLToPath(new URL("../.runtime/audio/", import.meta.url));
  await mkdir(runtimeDirectory, { recursive: true });
  const fileName = `${randomUUID()}.mp3`;
  await writeFile(join(runtimeDirectory, fileName), audio);
  return fileName;
}

export async function createNarration(event: NarrationEvent, config: BridgeConfig): Promise<NarrationResult> {
  const fallback = offlineLine(event);
  let text = fallback;
  let generatedByGemini = false;

  try {
    const generated = await generateGeminiLine(event, fallback, config);
    if (generated !== null) {
      text = generated;
      generatedByGemini = true;
    }
  } catch {
    // Keep the curated line and still allow ElevenLabs to voice it.
  }

  try {
    const audioFileName = await synthesizeElevenLabs(text, config);
    if (audioFileName !== null) {
      return {
        event,
        text,
        source: generatedByGemini ? "gemini-elevenlabs" : "elevenlabs",
        audioFileName,
      };
    }
  } catch {
    // Text remains usable; voice is optional and must never block gameplay.
  }

  return { event, text, source: generatedByGemini ? "gemini" : "offline" };
}
