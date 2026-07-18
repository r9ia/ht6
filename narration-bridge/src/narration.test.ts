import assert from "node:assert/strict";
import test from "node:test";
import type { BridgeConfig } from "./config.js";
import { cleanGeneratedText, createNarration, isAllowedEvent, offlineLine, type NarrationEvent } from "./narration.js";

const offlineConfig: BridgeConfig = {
  host: "127.0.0.1",
  port: 8787,
  cloudEnabled: false,
  microphoneConversationEnabled: false,
  geminiApiKey: "",
  geminiModel: "gemini-2.0-flash",
  elevenLabsApiKey: "",
  elevenLabsVoiceId: "",
  elevenLabsModelId: "eleven_multilingual_v2",
  providerTimeoutMs: 100,
};

const allowedEvents: NarrationEvent[] = [
  "calibration_complete",
  "escalation_low",
  "escalation_high",
  "panic_backoff",
  "recovery",
];

test("generated cloud lines are sanitized and limited to fifteen words", () => {
  const cleaned = cleanGeneratedText("<one> two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen\nseventeen");
  assert.equal(cleaned, "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen");
  assert.equal(cleaned.split(" ").length, 15);
  assert.doesNotMatch(cleaned, /[<>\r\n]/u);
});

test("only documented generic gameplay event labels are accepted", () => {
  for (const event of allowedEvents) assert.equal(isAllowedEvent(event), true);
  assert.equal(isAllowedEvent("heart_rate"), false);
  assert.equal(isAllowedEvent("arousal_0.99"), false);
  assert.equal(isAllowedEvent({ event: "recovery" }), false);
});

test("every offline event has multiple non-repeating responses", () => {
  for (const event of allowedEvents) {
    const first = offlineLine(event, () => 0);
    const second = offlineLine(event, () => 0);
    assert.ok(first.length > 10);
    assert.ok(second.length > 10);
    assert.notEqual(first, second);
  }
});

test("offline lines contain no medical claims", () => {
  for (const event of allowedEvents) {
    const line = offlineLine(event, () => 0.75);
    assert.doesNotMatch(line, /diagnos|treat|blood pressure|medical/iu);
  }
});

test("offline mode never requires provider credentials or audio", async () => {
  const result = await createNarration("panic_backoff", offlineConfig);
  assert.equal(result.source, "offline");
  assert.equal(result.audioFileName, undefined);
  assert.ok(result.text.length > 10);
});
