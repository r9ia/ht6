# Dread Director SDK — What It Gives Game Developers

> **The one-line pitch:** Dread Director lets a horror game read how scared the
> player *actually* is — from an ordinary webcam — and adapt the experience in
> real time, so every player gets a night tuned to their own nerve. Developers
> get all of this as a drop-in toolkit; they never touch biometrics, hardware,
> or the real-time brain that makes the calls.

This document is the high-level tour of what the SDK offers. It is deliberately
broad — the detailed contracts live in [`ARCHITECTURE.md`](ARCHITECTURE.md).

---

## The problem we solve

Horror games are tuned for an *imaginary average player*. The same jump scare
lands hard on one person and bores another. Designers guess at pacing and hope.

Dread Director replaces the guess with a signal: a live, privacy-safe read on
the player's arousal and composure. The game can then **push when the player is
comfortable and back off when they're overwhelmed** — the way a great haunted-
house actor reads a group and adjusts.

The reusable **SDK is the product.** The *Night Watch* demo in this repo is just
one game built on top of it to prove it works.

---

## What a developer gets (six pillars)

### 1. An adaptive "director" that reads the player
The heavy lifting — camera capture, signal processing, and the moment-to-moment
"should we escalate?" decisions — is done for you and delivered as a handful of
simple, game-ready cues: *stay calm, ramp up, ease off, recover.* You react to
those cues; you never process a heartbeat.

### 2. Plug-and-play reactions
A small "react to the director" pattern plus a starter library of ready-made
behaviors — adaptive music, enemy aggression, light failures, scare timing,
creeping screen effects. Drop them on your objects and they respond to the
player automatically. Studios can ship something reactive on day one and swap in
their own behaviors later.

### 3. Controls for designers, not just programmers
All the pacing rules — how fast tension builds, how often scares can fire, when
to back off — live in simple, editable profiles. A designer can dial in "slow-
burn dread" versus "roller-coaster" without writing code, and ship several
presets in one game.

### 4. Build and test without any hardware
Every layer has a fake/replay path. Developers can build, QA, and demo the whole
game with **no webcam and no sensor** — using keyboard cues, recorded sessions,
or synthetic tension curves. Live hardware is never required to make progress.

### 5. Insight into how players actually felt
The SDK can produce a per-session "fear timeline" for designers: where players
tensed up, where they went numb, which scares worked. It turns horror pacing
from a hunch into something you can measure and balance.

### 6. Privacy and trust built into the architecture
This is a feature, not fine print. Raw video, pulse, and anything identifying
**never leave the sensor device.** Only high-level cues ("ramp up," "ease off")
ever reach the game. The SDK makes **no** medical, diagnostic, or surveillance
claims. That boundary is what makes it shippable in a real product.

---

## Optional: rewards that respect the player

A separate, opt-in module turns "how calm did you stay" into a reward — a
leaderboard placement or a sandbox payout — using only an anonymous achievement
token. No biometrics, scores, or identity are ever sent anywhere. It's a clean
way to add stakes or monetization without compromising the privacy promise.

---

## How it fits together (plain version)

```
Webcam device            The "brain"              Your game
(sensor host)     →      (decision core)    →     (Unity, your engine)
reads the face,          decides pace:            receives simple cues,
keeps it local           calm / ramp / ease       plays YOUR reactions
```

Developers only ever work at the far right. The first two boxes are what the SDK
manages for them.

---

## What integration feels like

1. Add the director component to your scene.
2. Drop a reaction on anything you want the player's fear to influence — the
   monster, the music, the lights.
3. Pick or tweak a pacing profile.
4. Playtest with the keyboard/replay path; plug in the webcam when you're ready.

Illustrative sketch of a custom reaction (concept, not a required API):

```
// "When the director escalates, make my creature hunt faster.
//  When the player is overwhelmed, have it slink away."
onEscalate(intensity):  creature.speed = lerp(calm, frenzy, intensity)
onPanic():              creature.retreat()
onRecovery():           creature.resumeStalking()
```

That's the whole mental model: **the director tells you the mood; you decide
what your game does about it.**

---

## Why this matters for judges

- **Reusable platform, not a one-off game.** The demo is proof; the SDK is the
  deliverable.
- **Real, hard tech made simple.** Webcam biometrics + a portable real-time
  decision core, exposed as four friendly cues.
- **Privacy-first by design.** The sensitive data physically cannot reach the
  game or the cloud.
- **Adoptable.** Fake-input and designer profiles mean a studio can try it in an
  afternoon.
