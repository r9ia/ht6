import { mkdir, writeFile } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import type { BridgeConfig } from "./config.js";

const LINES = {
  calibration_complete: "Calibration complete. Try not to let the room learn you too quickly.",
  escalation_low: "Something in the room has shifted. Keep watching the corners.",
  escalation_high: "It knows where you are. Do not look away.",
  panic_backoff: "Easy now. The room is giving you one breath.",
  recovery: "Your pulse settles. Somewhere in the dark, it starts waiting again.",
} as const;

export type NarrationEvent = keyof typeof LINES;
export type NarrationSource = "offline" | "gemini" | "gemini-elevenlabs";

export interface NarrationResult {
  readonly event: NarrationEvent;
  readonly text: string;
  readonly source: NarrationSource;
  readonly audioFileName?: string;
}

export function isAllowedEvent(value: unknown): value is NarrationEvent {
  return typeof value === "string" && Object.prototype.hasOwnProperty.call(LINES, value);
}

export function offlineLine(event: NarrationEvent): string {
  return LINES[event];
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
        parts: [{ text: `Write one short PG-13 horror narrator line, maximum 22 words, for the fixed game event '${event}'. No names, biometrics, health claims, instructions, or markdown. Tone reference: ${fallback}` }],
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
  let source: NarrationSource = "offline";

  try {
    const generated = await generateGeminiLine(event, fallback, config);
    if (generated !== null) {
      text = generated;
      source = "gemini";
    }
  } catch {
    return { event, text: fallback, source: "offline" };
  }

  try {
    const audioFileName = await synthesizeElevenLabs(text, config);
    if (audioFileName !== null) return { event, text, source: "gemini-elevenlabs", audioFileName };
  } catch {
    // Text remains usable; voice is optional.
  }

  return { event, text, source };
}
