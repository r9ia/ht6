# mdmd Devpost

## Inspiration

Most games are tuned for an average player who doesn't really exist. Difficulty
settings and adaptive AI react to how well you play, but not to how you feel
while you play. You see it in any experience meant to move you: a moment that
grips one person does nothing for the next, and the designer is left guessing
where the emotional beats should land.

At the same time, reading someone's physical state got a lot easier. You can now
estimate heart rate, heart rate variability, and breathing from a normal webcam,
with no wearable. The catch is that this only gives you numbers. There was no
simple way for a developer to go from "the player's heart rate is 92" to "so
change what the game is doing." We wanted to build that missing piece and package
it so any developer can drop it in.

## What it does

mdmd lets a game sense how a player is feeling and respond in real time. A small
sensor unit, a Raspberry Pi with a camera and a contact pulse sensor, reads the
player. Our system turns the noisy raw signals into three values that are easy to
work with: arousal, sustained stress, and composure. A live "director" then
decides what should happen next, whether that is building tension, easing off
when the player is overwhelmed, or giving them room to recover.

Developers never touch raw biometrics or signal processing. They receive
high-level cues (escalate, panic, recover) and connect their own reactions to
them, such as enemy behavior, music, lighting, or pacing.

Our reference build, Night Watch, is a demo where a stalking creature hunts
harder when you stay calm and backs off when you panic, with a survival contract
that rewards keeping your composure. It runs the full loop from camera to
gameplay. It is only one example, though. Nothing in the framework is tied to
that style of game, or to games at all.

The same three signals and the same sense, decide, and adapt loop work anywhere
software could use a read on how someone feels: VR training that raises pressure
only while a trainee stays composed, interactive media that paces itself to the
audience, and UX research, wellness tools, or accessibility features that ease
off when someone is overwhelmed. A few concrete examples:

- Sales and client calls: a live read on how engaged or tense the other person
  is, so the rep knows when to ease off or dig in. Best as a consented,
  both-sides tool.
- Coaching and telehealth: it flags when a speaker, or a patient on a video
  visit, is getting overwhelmed, even when they say they are fine.
- Learning and tutoring: the lesson slows down or re-explains when a student is
  clearly confused or checked out.

## How we built it

We split the system into tiers so the sensitive data stays isolated:

- Sensor unit (Raspberry Pi with a camera and a contact pulse sensor): the camera
  estimates breathing and heart rate from the face, and the pulse sensor reads the
  heartbeat directly. Reading both on one small board keeps all the raw data local
  and gives us a clean reference to check the camera against.
- Decision core (portable, deterministic C++17 "Director"): fuses the camera and
  pulse data, gates it by confidence, turns it into normalized cues, and makes the
  pacing calls. It has no engine, vendor, or OS-specific headers, so it can be
  reused elsewhere.
- Game (Unity 6, URP): receives only high-level JSON events over local UDP and
  turns them into gameplay.

Everything speaks one small event contract: state, escalate, panic, recovery. The
Unity side has a single bridge that treats live signals and fake or replayed
input the same way, plus the reactive systems built on top of it (monster AI,
lighting, audio stings, the death sequence, and the composure-based reward
contract). Every layer also has a fake or replay path, so we can build and demo
the whole thing with no camera or sensor connected.

We built our own sensor device on purpose. A webcam alone can estimate heart
rate, but it is easy to throw off by lighting, movement, and skin tone, and
slower signals like HRV need a warm-up before they can be trusted. A contact
pulse sensor gives a clean heartbeat from the first second, so we use it to
anchor and sanity-check the camera estimate, cover the cold-start gap, and catch
drift. Fusing the two is more reliable than either one alone, and running it on a
dedicated Raspberry Pi produces the same calibrated signal on every setup instead
of making a studio babysit camera drivers and lighting.

Privacy is the other reason, and maybe the bigger one. People are understandably
wary of a game watching them through their webcam and reading their body. Because
the sensing lives on a separate device and only sends out the three simple
signals, the game never sees video, a heartbeat trace, or anything that
identifies the person. The raw data stays on the box, and the device is open
source, so anyone can check what it measures and what it sends instead of being
asked to trust it.

## Challenges we ran into

- Raw biosignals are noisy and slow to trust. HRV is not reliable for about the
  first 60 seconds, breathing needs around 30, and confidence arrives as
  percentages rather than clean numbers. Turning that into something a developer
  can use without a physiology background, while staying honest about when we do
  not know yet, took real work.
- Keeping the decision core portable. No Unity, no vendor SDKs, and no
  OS-specific headers meant we had to design clean contracts up front instead of
  taking shortcuts.
- Handling different hardware. Cameras, drivers, and lighting all behave
  differently, so a lot of the effort went into making the output consistent no
  matter the setup underneath.
- Demoing without hardware in the room, so a bad webcam or bad lighting never
  breaks the pitch.
- Tuning the feel. Balancing the monster, the pacing, and even the lighting so
  the experience stays tense but readable took a lot of iteration.

## Accomplishments that we're proud of

- We built the decision layer, not just a wrapper around the measurements. This
  is the part that turns vitals into behavior.
- The privacy model is built into the architecture. Raw video and biometrics stay
  on the sensor device, and the game only ever sees high-level cues.
- We have a playable reference game, running from camera to gameplay, that
  visibly responds to the player's state.
- The framework works well past our own demo. Same three signals, any medium.
- It degrades gracefully. No hardware, no internet, and it still runs.

## What we learned

- The hard part is not measuring emotion, which is becoming common, but deciding
  what to do with it. That shaped how we scoped the project.
- Simple, stable outputs matter. Collapsing pulse, HRV, and breathing into three
  numbers is what makes the thing usable.
- Privacy and portability are much easier when you treat them as constraints from
  the start.
- Being upfront about uncertainty, like confidence windows and warm-up time,
  makes the system easier to trust, not harder.

## What's next for mdmd

- A toolkit for designers: tunable pacing profiles, a set of ready-made
  reactions, and a per-session view of where players tensed up.
- More signals and better decisions as the models improve.
- Pilots outside gaming, working with teams in training, interactive media, and
  research to test the framework in real use.
- A standardized sensor device so studios integrate once and get the same signals
  everywhere.
- Support for more engines beyond Unity, such as Unreal and Godot, on the same
  event contract.
