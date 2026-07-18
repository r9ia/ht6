import { createReadStream, existsSync } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { fileURLToPath } from "node:url";
import { loadConfig } from "./config.js";
import { createConversationNarration, createNarration, isAllowedEvent } from "./narration.js";

const config = loadConfig();
const maxJsonBodyBytes = 4_096;
const maxWavBodyBytes = 512_000;
const audioDirectory = fileURLToPath(new URL("../.runtime/audio/", import.meta.url));

function sendJson(response: ServerResponse, status: number, value: unknown): void {
  response.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
    "x-content-type-options": "nosniff",
  });
  response.end(JSON.stringify(value));
}

async function readBody(request: IncomingMessage, maxBytes: number): Promise<Buffer> {
  const chunks: Buffer[] = [];
  let bytes = 0;
  for await (const chunk of request) {
    const buffer = Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk);
    bytes += buffer.length;
    if (bytes > maxBytes) throw new Error("body-too-large");
    chunks.push(buffer);
  }
  return Buffer.concat(chunks);
}

async function readJson(request: IncomingMessage): Promise<unknown> {
  return JSON.parse((await readBody(request, maxJsonBodyBytes)).toString("utf8")) as unknown;
}

function audioUrl(fileName: string | undefined): string | null {
  return fileName === undefined ? null : `http://${config.host}:${config.port}/audio/${fileName}`;
}

async function serveAudio(pathname: string, response: ServerResponse): Promise<boolean> {
  const match = /^\/audio\/([0-9a-f-]{36}\.mp3)$/u.exec(pathname);
  const fileName = match?.[1];
  if (fileName === undefined) return false;
  const fullPath = `${audioDirectory}/${fileName}`;
  if (!existsSync(fullPath) || (await stat(fullPath)).size > 5_000_000) {
    sendJson(response, 404, { error: "audio-not-found" });
    return true;
  }

  response.writeHead(200, {
    "content-type": "audio/mpeg",
    "cache-control": "no-store",
    "x-content-type-options": "nosniff",
  });
  createReadStream(fullPath).pipe(response);
  return true;
}

const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url ?? "/", `http://${config.host}:${config.port}`);
    if (request.method === "GET" && url.pathname === "/health") {
      sendJson(response, 200, {
        status: "ok",
        cloudEnabled: config.cloudEnabled,
        microphoneConversationEnabled: config.microphoneConversationEnabled,
      });
      return;
    }

    if (request.method === "GET" && await serveAudio(url.pathname, response)) return;

    if (request.method === "POST" && url.pathname === "/v1/narration") {
      const payload = await readJson(request) as { event?: unknown };
      if (!isAllowedEvent(payload?.event)) {
        sendJson(response, 400, { error: "unsupported-event" });
        return;
      }

      const result = await createNarration(payload.event, config);
      sendJson(response, 200, {
        event: result.event,
        text: result.text,
        source: result.source,
        audioUrl: audioUrl(result.audioFileName),
      });
      return;
    }

    if (request.method === "POST" && url.pathname === "/v1/conversation") {
      if (!config.microphoneConversationEnabled) {
        sendJson(response, 403, { error: "microphone-disabled" });
        return;
      }
      if (!(request.headers["content-type"] ?? "").toLowerCase().startsWith("audio/wav")) {
        sendJson(response, 415, { error: "wav-required" });
        return;
      }
      const wav = await readBody(request, maxWavBodyBytes);
      try {
        const result = await createConversationNarration(wav, config);
        sendJson(response, 200, {
          text: result.text,
          source: result.source,
          audioUrl: audioUrl(result.audioFileName),
        });
      } catch (error) {
        const code = error instanceof Error ? error.message : "conversation-failed";
        const status = code === "invalid-wav" ? 400 : code === "microphone-disabled" ? 403 : 502;
        sendJson(response, status, { error: code });
      }
      return;
    }

    sendJson(response, 404, { error: "not-found" });
  } catch (error) {
    const status = error instanceof Error && error.message === "body-too-large" ? 413 : 400;
    sendJson(response, status, { error: status === 413 ? "body-too-large" : "invalid-request" });
  }
});

server.listen(config.port, config.host, () => {
  console.log(`[Dread Director] Narration bridge listening on http://${config.host}:${config.port}; cloud=${config.cloudEnabled ? "enabled" : "disabled"}; microphone=${config.microphoneConversationEnabled ? "enabled" : "disabled"}.`);
});
