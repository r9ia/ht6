import assert from "node:assert/strict";
import test from "node:test";
import type { BridgeConfig } from "./config.js";
import { createNarration, isAllowedEvent, offlineLine } from "./narration.js";

const offlineConfig: BridgeConfig = {
  host: "127.0.0.1",
  port: 8787,
  cloudEnabled: false,
  geminiApiKey: "",
  geminiModel: "gemini-2.0-flash",
  elevenLabsApiKey: "",
  elevenLabsVoiceId: "",
  providerTimeoutMs: 100,
};

test("only documented event labels are accepted", () => {
  assert.equal(isAllowedEvent("escalation_low"), true);
  assert.equal(isAllowedEvent("arousal_0.99"), false);
  assert.equal(isAllowedEvent({ event: "recovery" }), false);
});

test("offline lines are non-empty and contain no health claim", () => {
  const line = offlineLine("recovery");
  assert.ok(line.length > 10);
  assert.doesNotMatch(line, /diagnos|treat|blood pressure/iu);
});

test("offline mode never requires provider credentials", async () => {
  const result = await createNarration("panic_backoff", offlineConfig);
  assert.equal(result.source, "offline");
  assert.equal(result.audioFileName, undefined);
  assert.equal(result.text, offlineLine("panic_backoff"));
});
