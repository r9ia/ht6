import type { UnifoldBridgeConfig } from "./config.js";

/**
 * The Night Watch Contract rewards two things Unity measures locally: how long
 * the player survived, and how calm they stayed while surviving (being less
 * scared). Unity folds those into ONE opaque achievement tier and sends only
 * the tier + an opaque claim id. Raw survival time, composure, Director scores,
 * and biometrics never cross into this bridge — the allowlist below is the full
 * vocabulary the bridge understands.
 *
 * Tiers ascend with survival duration and composure, so a longer, calmer night
 * earns a larger sandbox stablecoin reward.
 */
export const REWARD_TIERS = {
  /** Survived a meaningful stretch of the night. */
  endured: 1.0,
  /** Survived the full night. */
  survivor: 1.5,
  /** Survived the full night while holding composure — visibly less scared. */
  composed_survivor: 2.5,
  /** Survived a long night and stayed remarkably calm throughout. */
  unshaken: 5.0,
} as const;

export type RewardTier = keyof typeof REWARD_TIERS;

export type NormalizedStatus = "completed" | "pending" | "cancelled" | "failed";

export interface RewardClaim {
  readonly version?: unknown;
  readonly claimId?: unknown;
  readonly tier?: unknown;
}

export interface ClaimResult {
  readonly claimId: string;
  readonly tier: RewardTier;
  readonly status: NormalizedStatus;
  readonly amountUsd: number;
  readonly amountBaseUnits: string;
  readonly reference: string | null;
  readonly mode: "mock" | "live";
  readonly reused: boolean;
  readonly failureReason?: string;
}

export class ClaimValidationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "ClaimValidationError";
  }
}

const CLAIM_ID_PATTERN = /^[A-Za-z0-9_-]{8,128}$/u;

export function isAllowedTier(value: unknown): value is RewardTier {
  return typeof value === "string" && Object.prototype.hasOwnProperty.call(REWARD_TIERS, value);
}

export function rewardUsdForTier(tier: RewardTier): number {
  return REWARD_TIERS[tier];
}

/** Convert a USD amount to integer base units for a token with `decimals`. */
export function usdToBaseUnits(usd: number, decimals: number): string {
  if (!Number.isFinite(usd) || usd <= 0) return "0";
  // Round to the token's precision using integer math to avoid float drift.
  const scaled = Math.round(usd * 10 ** decimals);
  return scaled.toString();
}

/** Map any provider status string onto the four states Unity understands. */
export function normalizeStatus(providerStatus: unknown): NormalizedStatus {
  const status = typeof providerStatus === "string" ? providerStatus.toLowerCase() : "";
  switch (status) {
    case "completed":
    case "succeeded":
    case "success":
      return "completed";
    case "failed":
    case "error":
      return "failed";
    case "canceled":
    case "cancelled":
      return "cancelled";
    case "pending":
    case "processing":
    case "created":
    case "":
      return "pending";
    default:
      return "pending";
  }
}

interface ValidatedClaim {
  readonly claimId: string;
  readonly tier: RewardTier;
}

/** Validate an inbound claim. Throws {@link ClaimValidationError} on bad input. */
export function validateClaim(payload: RewardClaim): ValidatedClaim {
  if (payload === null || typeof payload !== "object") {
    throw new ClaimValidationError("invalid-body");
  }
  if (payload.version !== undefined && payload.version !== 1) {
    throw new ClaimValidationError("unsupported-version");
  }
  if (typeof payload.claimId !== "string" || !CLAIM_ID_PATTERN.test(payload.claimId)) {
    throw new ClaimValidationError("invalid-claim-id");
  }
  if (!isAllowedTier(payload.tier)) {
    throw new ClaimValidationError("unsupported-tier");
  }
  return { claimId: payload.claimId, tier: payload.tier };
}

interface TransferOutcome {
  readonly status: NormalizedStatus;
  readonly reference: string | null;
  readonly failureReason?: string;
}

interface OutboundTransferBody {
  readonly source: { treasury_account_id: string; currency: string; chain_id: string };
  readonly external_user_id: string;
  readonly destination: {
    recipient_address: string;
    chain_type: string;
    chain_id: string;
    token_address: string;
  };
  readonly amount: string;
}

function buildTransferBody(config: UnifoldBridgeConfig, claimId: string, amountBaseUnits: string): OutboundTransferBody {
  return {
    source: {
      treasury_account_id: config.treasuryAccountId,
      currency: "usdc",
      chain_id: config.treasurySourceChainId,
    },
    // The claim id is the only cross-boundary identifier; reuse it as the
    // opaque external user id so Unifold never receives player identity.
    external_user_id: claimId,
    destination: {
      recipient_address: config.recipientAddress,
      chain_type: config.rewardChainType,
      chain_id: config.rewardChainId,
      token_address: config.rewardTokenAddress,
    },
    amount: amountBaseUnits,
  };
}

/**
 * Load the official `@unifold/node` SDK if the operator has installed it.
 * The specifier is assembled at runtime so the bridge type-checks and builds
 * with zero dependencies; the SDK is used SDK-first when present, otherwise the
 * documented REST endpoint is called directly. Returns `null` when absent.
 */
async function loadUnifoldSdk(): Promise<unknown | null> {
  try {
    const specifier = ["@unifold", "node"].join("/");
    const mod = (await import(specifier)) as { default?: unknown };
    return mod.default ?? mod;
  } catch {
    return null;
  }
}

async function transferViaSdk(
  sdk: unknown,
  config: UnifoldBridgeConfig,
  claimId: string,
  amountBaseUnits: string,
): Promise<TransferOutcome> {
  const body = buildTransferBody(config, claimId, amountBaseUnits);
  const client = new (sdk as new (key: string) => any)(config.secretKey);
  const transfer = await client.treasury.outboundTransfers.create(body, { idempotencyKey: claimId });
  return { status: normalizeStatus(transfer?.status), reference: typeof transfer?.id === "string" ? transfer.id : null };
}

async function transferViaRest(
  config: UnifoldBridgeConfig,
  claimId: string,
  amountBaseUnits: string,
): Promise<TransferOutcome> {
  const body = buildTransferBody(config, claimId, amountBaseUnits);
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), config.providerTimeoutMs);
  try {
    const response = await fetch(`${config.apiBaseUrl}/v1/treasury/outbound_transfers`, {
      method: "POST",
      headers: {
        authorization: `Bearer ${config.secretKey}`,
        "content-type": "application/json",
        // Required by Unifold; the claim id makes retries safe and deduped.
        "idempotency-key": claimId,
      },
      body: JSON.stringify(body),
      signal: controller.signal,
    });

    const json = (await response.json().catch(() => null)) as { id?: unknown; status?: unknown } | null;
    if (!response.ok) {
      return { status: "failed", reference: null, failureReason: `http-${response.status}` };
    }
    return {
      status: normalizeStatus(json?.status),
      reference: typeof json?.id === "string" ? json.id : null,
    };
  } finally {
    clearTimeout(timer);
  }
}

/** Deterministic, network-free stand-in used whenever live mode is not configured. */
export function mockTransfer(claimId: string): TransferOutcome {
  return { status: "completed", reference: `mock_obt_${claimId}` };
}

export interface RewardService {
  claim(payload: RewardClaim): Promise<ClaimResult>;
  readonly mode: "mock" | "live";
}

/**
 * Build the reward service. Idempotency is keyed on the opaque claim id: a
 * repeated claim returns the original result (marked `reused`) and never issues
 * a second transfer, matching the "credit exactly once" pattern.
 */
export function createRewardService(config: UnifoldBridgeConfig): RewardService {
  const processed = new Map<string, ClaimResult>();

  async function runTransfer(claimId: string, amountBaseUnits: string): Promise<TransferOutcome> {
    if (config.mode === "mock") return mockTransfer(claimId);
    const sdk = await loadUnifoldSdk();
    if (sdk !== null) return transferViaSdk(sdk, config, claimId, amountBaseUnits);
    return transferViaRest(config, claimId, amountBaseUnits);
  }

  return {
    mode: config.mode,
    async claim(payload: RewardClaim): Promise<ClaimResult> {
      const { claimId, tier } = validateClaim(payload);

      const existing = processed.get(claimId);
      if (existing !== undefined) return { ...existing, reused: true };

      const amountUsd = Math.min(rewardUsdForTier(tier), config.maxRewardUsd);
      const amountBaseUnits = usdToBaseUnits(amountUsd, config.rewardTokenDecimals);

      let outcome: TransferOutcome;
      try {
        outcome = await runTransfer(claimId, amountBaseUnits);
      } catch (error) {
        outcome = {
          status: "failed",
          reference: null,
          failureReason: error instanceof Error ? error.name : "transfer-error",
        };
      }

      const result: ClaimResult = {
        claimId,
        tier,
        status: outcome.status,
        amountUsd,
        amountBaseUnits,
        reference: outcome.reference,
        mode: config.mode,
        reused: false,
        ...(outcome.failureReason !== undefined ? { failureReason: outcome.failureReason } : {}),
      };

      // Only memoize terminal-or-in-flight successes/pends; allow a failed live
      // transfer to be retried under the same claim id later.
      if (result.status !== "failed") processed.set(claimId, result);
      return result;
    },
  };
}
