import assert from "node:assert/strict";
import test from "node:test";
import type { UnifoldBridgeConfig } from "./config.js";
import {
  ClaimValidationError,
  createRewardService,
  isAllowedTier,
  normalizeStatus,
  rewardUsdForTier,
  usdToBaseUnits,
  validateClaim,
} from "./rewards.js";

function makeConfig(overrides: Partial<UnifoldBridgeConfig> = {}): UnifoldBridgeConfig {
  return {
    host: "127.0.0.1",
    port: 8788,
    mode: "mock",
    rewardsEnabled: false,
    apiBaseUrl: "https://api.unifold.io",
    secretKey: "",
    treasuryAccountId: "",
    treasurySourceChainId: "8453",
    recipientAddress: "",
    rewardChainType: "ethereum",
    rewardChainId: "8453",
    rewardTokenAddress: "0x833589fcd6edb6e08f4c7c32d4f71b54bda02913",
    rewardTokenDecimals: 6,
    maxRewardUsd: 5,
    providerTimeoutMs: 10_000,
    ...overrides,
  };
}

test("tier allowlist accepts known tiers and rejects everything else", () => {
  assert.equal(isAllowedTier("endured"), true);
  assert.equal(isAllowedTier("unshaken"), true);
  assert.equal(isAllowedTier("cheater"), false);
  assert.equal(isAllowedTier(42), false);
  assert.equal(isAllowedTier(undefined), false);
  // Guard against prototype-key false positives.
  assert.equal(isAllowedTier("toString"), false);
});

test("reward tiers ascend with survival and composure", () => {
  assert.ok(rewardUsdForTier("endured") < rewardUsdForTier("survivor"));
  assert.ok(rewardUsdForTier("survivor") < rewardUsdForTier("composed_survivor"));
  assert.ok(rewardUsdForTier("composed_survivor") < rewardUsdForTier("unshaken"));
});

test("usd converts to token base units without float drift", () => {
  assert.equal(usdToBaseUnits(1, 6), "1000000");
  assert.equal(usdToBaseUnits(0.25, 6), "250000");
  assert.equal(usdToBaseUnits(2, 6), "2000000");
  assert.equal(usdToBaseUnits(0, 6), "0");
  assert.equal(usdToBaseUnits(-1, 6), "0");
});

test("provider statuses normalize to the four Unity states", () => {
  assert.equal(normalizeStatus("completed"), "completed");
  assert.equal(normalizeStatus("succeeded"), "completed");
  assert.equal(normalizeStatus("processing"), "pending");
  assert.equal(normalizeStatus("pending"), "pending");
  assert.equal(normalizeStatus("canceled"), "cancelled");
  assert.equal(normalizeStatus("cancelled"), "cancelled");
  assert.equal(normalizeStatus("failed"), "failed");
  assert.equal(normalizeStatus("something-new"), "pending");
  assert.equal(normalizeStatus(undefined), "pending");
});

test("claim validation enforces version, opaque id, and allowlisted tier", () => {
  assert.throws(() => validateClaim({ claimId: "abcd1234", tier: "survivor", version: 2 }), ClaimValidationError);
  assert.throws(() => validateClaim({ claimId: "short", tier: "survivor" }), ClaimValidationError);
  assert.throws(() => validateClaim({ claimId: "has spaces here", tier: "survivor" }), ClaimValidationError);
  assert.throws(() => validateClaim({ claimId: "validclaim123", tier: "unknown" }), ClaimValidationError);
  const ok = validateClaim({ claimId: "validclaim123", tier: "survivor", version: 1 });
  assert.deepEqual(ok, { claimId: "validclaim123", tier: "survivor" });
});

test("mock claim pays the tier amount and reports mock mode", async () => {
  const rewards = createRewardService(makeConfig());
  const result = await rewards.claim({ version: 1, claimId: "nightwatch_abc123", tier: "composed_survivor" });
  assert.equal(result.status, "completed");
  assert.equal(result.mode, "mock");
  assert.equal(result.tier, "composed_survivor");
  assert.equal(result.amountUsd, 1);
  assert.equal(result.amountBaseUnits, "1000000");
  assert.equal(result.reference, "mock_obt_nightwatch_abc123");
  assert.equal(result.reused, false);
});

test("repeat claims are idempotent and never re-pay", async () => {
  const rewards = createRewardService(makeConfig());
  const first = await rewards.claim({ claimId: "nightwatch_dup001", tier: "survivor" });
  const second = await rewards.claim({ claimId: "nightwatch_dup001", tier: "survivor" });
  assert.equal(first.reused, false);
  assert.equal(second.reused, true);
  assert.equal(second.reference, first.reference);
  assert.equal(second.amountBaseUnits, first.amountBaseUnits);
});

test("max reward cap clamps oversized tiers", async () => {
  const rewards = createRewardService(makeConfig({ maxRewardUsd: 0.5 }));
  const result = await rewards.claim({ claimId: "nightwatch_cap001", tier: "unshaken" });
  assert.equal(result.amountUsd, 0.5);
  assert.equal(result.amountBaseUnits, "500000");
});
