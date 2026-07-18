import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

/**
 * Runtime configuration for the Unifold reward bridge.
 *
 * Secrets live only in the ignored root `.env`. The bridge defaults to `mock`
 * mode so the Night Watch demo runs with no keys, no funds, and no network —
 * preserving the project-wide "replay/fake path at every layer" rule. Live
 * payouts require an explicit opt-in flag plus a secret key, treasury account,
 * and recipient address.
 */
export interface UnifoldBridgeConfig {
  readonly host: string;
  readonly port: number;
  /** "live" only when rewards are explicitly enabled and fully configured. */
  readonly mode: "mock" | "live";
  readonly rewardsEnabled: boolean;
  readonly apiBaseUrl: string;
  readonly secretKey: string;
  readonly treasuryAccountId: string;
  /** Chain the treasury holds USDC on: 137 (Polygon), 8453 (Base), or mainnet (Solana). */
  readonly treasurySourceChainId: string;
  /** Where the sandbox reward is delivered (a demo wallet; a real app uses the player's wallet). */
  readonly recipientAddress: string;
  readonly rewardChainType: string;
  readonly rewardChainId: string;
  readonly rewardTokenAddress: string;
  readonly rewardTokenDecimals: number;
  /** Safety ceiling (USD) so a bad tier table can never mint an oversized payout. */
  readonly maxRewardUsd: number;
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
  const parsed = Number.parseInt(value ?? "8788", 10);
  return Number.isInteger(parsed) && parsed > 0 && parsed <= 65535 ? parsed : 8788;
}

function parsePositiveInt(value: string | undefined, fallback: number): number {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function parsePositiveNumber(value: string | undefined, fallback: number): number {
  const parsed = Number.parseFloat(value ?? "");
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

/** Base USDC (6 decimals) is the default sandbox reward token. */
const DEFAULT_REWARD_TOKEN_ADDRESS = "0x833589fcd6edb6e08f4c7c32d4f71b54bda02913";

export function loadConfig(): UnifoldBridgeConfig {
  loadRootEnv();

  const requestedHost = process.env.UNIFOLD_BRIDGE_HOST ?? "127.0.0.1";
  const host = requestedHost === "localhost" || requestedHost === "::1" || requestedHost === "127.0.0.1"
    ? requestedHost
    : "127.0.0.1";

  const rewardsEnabled = process.env.ENABLE_UNIFOLD_REWARDS === "true";
  const secretKey = (process.env.UNIFOLD_SECRET_KEY ?? "").trim();
  const treasuryAccountId = (process.env.UNIFOLD_TREASURY_ACCOUNT_ID ?? "").trim();
  const recipientAddress = (process.env.UNIFOLD_DEMO_RECIPIENT_ADDRESS ?? "").trim();

  // Live payouts require every ingredient. Any gap falls back to mock so the
  // demo never blocks and no half-configured transfer is ever attempted.
  const canGoLive =
    rewardsEnabled &&
    secretKey.startsWith("sk_") &&
    treasuryAccountId.length > 0 &&
    recipientAddress.length > 0;

  return {
    host,
    port: parsePort(process.env.UNIFOLD_BRIDGE_PORT),
    mode: canGoLive ? "live" : "mock",
    rewardsEnabled,
    apiBaseUrl: (process.env.UNIFOLD_API_BASE ?? "https://api.unifold.io").replace(/\/+$/u, ""),
    secretKey,
    treasuryAccountId,
    treasurySourceChainId: (process.env.UNIFOLD_TREASURY_CHAIN_ID ?? "8453").trim(),
    recipientAddress,
    rewardChainType: (process.env.UNIFOLD_REWARD_CHAIN_TYPE ?? "ethereum").trim(),
    rewardChainId: (process.env.UNIFOLD_REWARD_CHAIN_ID ?? "8453").trim(),
    rewardTokenAddress: (process.env.UNIFOLD_REWARD_TOKEN_ADDRESS ?? DEFAULT_REWARD_TOKEN_ADDRESS).trim(),
    rewardTokenDecimals: parsePositiveInt(process.env.UNIFOLD_REWARD_TOKEN_DECIMALS, 6),
    maxRewardUsd: parsePositiveNumber(process.env.UNIFOLD_MAX_REWARD_USD, 5),
    providerTimeoutMs: 10_000,
  };
}
