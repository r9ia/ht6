import { createServer, type IncomingMessage, type ServerResponse } from "node:http";
import { loadConfig } from "./config.js";
import { ClaimValidationError, createRewardService, REWARD_TIERS, type RewardClaim } from "./rewards.js";

const config = loadConfig();
const rewards = createRewardService(config);
const maxJsonBodyBytes = 2_048;

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

const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url ?? "/", `http://${config.host}:${config.port}`);

    if (request.method === "GET" && url.pathname === "/health") {
      sendJson(response, 200, {
        status: "ok",
        mode: config.mode,
        rewardsEnabled: config.rewardsEnabled,
        rewardChainType: config.rewardChainType,
        rewardChainId: config.rewardChainId,
        tiers: Object.keys(REWARD_TIERS),
      });
      return;
    }

    if (request.method === "POST" && url.pathname === "/v1/reward/claim") {
      const payload = (await readJson(request)) as RewardClaim;
      try {
        const result = await rewards.claim(payload);
        sendJson(response, 200, result);
      } catch (error) {
        if (error instanceof ClaimValidationError) {
          sendJson(response, 400, { error: error.message });
          return;
        }
        // Never surface provider internals or secrets to the caller.
        sendJson(response, 502, { error: "reward-unavailable" });
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
  console.log(
    `[Dread Director] Unifold reward bridge listening on http://${config.host}:${config.port}; mode=${config.mode}; rewards=${config.rewardsEnabled ? "enabled" : "disabled"}.`,
  );
});
