import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

export interface BridgeConfig {
  readonly host: string;
  readonly port: number;
  readonly cloudEnabled: boolean;
  readonly geminiApiKey: string;
  readonly geminiModel: string;
  readonly elevenLabsApiKey: string;
  readonly elevenLabsVoiceId: string;
  readonly providerTimeoutMs: number;
}

function loadRootEnv(): void {
  const envPath = fileURLToPath(new URL("../../.env", import.meta.url));
  let content: string;
  try {
    content = readFileSync(envPath, "utf8");
  } catch {
    return;
  }

  for (const rawLine of content.split(/\r?\n/u)) {
    const line = rawLine.trim();
    if (line.length === 0 || line.startsWith("#")) continue;
    const separator = line.indexOf("=");
    if (separator <= 0) continue;
    const key = line.slice(0, separator).trim();
    const value = line.slice(separator + 1).trim().replace(/^(["'])(.*)\1$/u, "$2");
    if (process.env[key] === undefined) process.env[key] = value;
  }
}

function parsePort(value: string | undefined): number {
  const parsed = Number.parseInt(value ?? "8787", 10);
  return Number.isInteger(parsed) && parsed > 0 && parsed <= 65535 ? parsed : 8787;
}

export function loadConfig(): BridgeConfig {
  loadRootEnv();
  const requestedHost = process.env.NARRATION_BRIDGE_HOST ?? "127.0.0.1";
  const host = requestedHost === "localhost" || requestedHost === "::1" || requestedHost === "127.0.0.1"
    ? requestedHost
    : "127.0.0.1";

  return {
    host,
    port: parsePort(process.env.NARRATION_BRIDGE_PORT),
    cloudEnabled: process.env.ENABLE_CLOUD_NARRATION === "true",
    geminiApiKey: process.env.GEMINI_API_KEY ?? "",
    geminiModel: process.env.GEMINI_MODEL ?? "gemini-2.0-flash",
    elevenLabsApiKey: process.env.ELEVENLABS_API_KEY ?? "",
    elevenLabsVoiceId: process.env.ELEVENLABS_VOICE_ID ?? "",
    providerTimeoutMs: 4_000,
  };
}
