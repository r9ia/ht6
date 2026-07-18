# mdmd — Devpost

## Inspiration

Every game is balanced for a player who doesn't exist — the "average" one.
Difficulty sliders and adaptive AI have gotten good at reacting to how well you
*play*, but nothing reacts to how you actually *feel*. It shows up in any
experience built to move you: a moment that grips one player leaves the next
cold, and designers just have to guess where the emotional beats should land.

Meanwhile, the tech to read a person's emotional state quietly got real — you
can now estimate heart rate, heart-rate variability, and breathing from an
ordinary webcam, no wearables. But that only gets you *numbers*. There was no
easy way for a game developer to go from "here's a heart rate" to "so make the
game do something about it." We wanted to build the missing layer: the thing
that turns a player's real, live emotional state into decisions a game can act
on — and make it a drop-in framework so any developer can use it.

## What it does

**mdmd** is a framework that lets a game sense how a player is really feeling and
adapt in real time. A normal webcam reads the player; our system translates the
messy biosignals into three simple, game-ready values — **arousal**, **sustained
stress**, and **composure** — and a live "director" decides what the experience
should do next: ramp the tension, ease off when the player is overwhelmed, or
let them recover.

Developers never touch raw biometrics or signal processing. They get high-level
cues (`escalate`, `panic`, `recover`) and plug their own reactions into them —
enemy behavior, music, lighting, pacing.

Our reference build, **Night Watch**, is a demo where a stalking creature hunts
harder when you're calm and backs off when you break — with a survival "contract"
that rewards staying composed. It proves the whole loop end to end. But it's just
one showcase: nothing about the framework is tied to that genre, or even to games
at all — it's a general way for software to respond to how a person feels.

## Beyond gaming

Games are our first target, but the same three signals and the same "sense →
decide → adapt" loop apply anywhere software would benefit from knowing how a
person feels. In **VR and simulation training**, it can drive stress-inoculation
— ramping pressure only while the trainee stays composed and easing off before
they're overwhelmed. In **film, immersive theater, and theme parks**, scenes can
pace themselves to the audience's real reactions. In **UX and product research**,
teams can measure genuine engagement and stress without interrupting people with
surveys. In **wellness and biofeedback**, it can power breathing and calm-coaching
tools that respond to your actual state. And in **accessibility**, it can quietly
dial intensity, difficulty, or sensory load down when someone is getting
overwhelmed. Same framework, same privacy guarantees — just pointed at a
different experience.

## How we built it

- **A three-tier architecture** that keeps sensitive data isolated:
  - **Sensor host (Linux):** webcam + camera-based vitals (pulse, HRV, breathing).
  - **Decision core (QNX on a Raspberry Pi):** a portable, deterministic C++17
    "Director" that fuses and confidence-gates the raw signals into normalized
    cues and makes the pacing calls. It's dependency-free — no engine, vendor, or
    OS headers — so it's genuinely reusable.
  - **Game (Unity 6 / URP):** receives only high-level JSON events over local UDP
    and turns them into gameplay.
- **A single event contract** (`state / escalate / panic / recovery`) that
  everything speaks.
- **A Unity integration layer** — one bridge that unifies live signals and
  fake/replay input, plus reactive systems (adaptive monster AI, lighting, audio
  stings, a cinematic death sequence, and the composure-based reward contract).
- **A fake/replay path at every layer**, so the whole thing is buildable and
  demoable without a camera or sensor attached.

## Challenges we ran into

- **Raw biosignals are noisy and slow to trust.** HRV isn't reliable until ~60
  seconds in, breathing needs ~30, and confidence comes as percentages, not clean
  values. Turning that into something a developer can use without a physiology
  degree — while being honest about "we don't know yet" — was the hard part.
- **Keeping the decision core truly portable.** No Unity, no vendor SDKs, no
  OS-specific headers meant designing clean contracts up front and resisting
  shortcuts.
- **Cross-device standardization.** Cameras, drivers, and lighting all behave
  differently; a lot of the work was making the *output* identical regardless of
  the setup underneath.
- **Making it demoable without hardware in the room**, so a bad webcam or bad
  lighting never breaks the pitch.
- **Tuning "feel."** Balancing the monster, pacing, and even the lighting so the
  experience is tense but readable took a lot of iteration.

## Accomplishments that we're proud of

- We built the *decision layer*, not just another measurement wrapper — the part
  that actually turns vitals into behavior.
- A clean privacy story that's baked into the architecture: raw video and
  biometrics **physically never leave the sensor device**; the game only ever
  sees high-level cues.
- A fully playable end-to-end reference game that visibly reacts to emotional
  state.
- A framework that's genuinely reusable beyond our demo — same three signals, any
  medium.
- It all degrades gracefully: no hardware, no internet, still works.

## What we learned

- The value isn't in measuring emotion — that's becoming a commodity — it's in
  **deciding what to do with it**. That reframing shaped the whole project.
- Good abstractions are a feature: collapsing pulse/HRV/breathing into three
  stable numbers is what makes this usable.
- Privacy and portability are easier when they're design constraints from day
  one, not afterthoughts.
- Honesty about uncertainty (confidence windows, warm-up time) makes the system
  more trustworthy, not less.

## What's next for mdmd

- **A designer-friendly toolkit:** tunable pacing profiles, a ready-made reactor
  library, and analytics that show designers a per-session "fear timeline."
- **More signals and smarter decisions** as the models mature.
- **Pilots outside gaming:** partnering with teams in training, interactive
  media, and research to validate the framework in the wild (see *Beyond gaming*).
- **A standardized sensor appliance** so studios integrate once and get
  identical, reproducible signals everywhere.
- **Engine support beyond Unity** (Unreal, Godot) on the same event contract.
