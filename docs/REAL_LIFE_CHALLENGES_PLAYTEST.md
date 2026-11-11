# Real-life challenges: US and Canada

## Use

Open the local game at `http://127.0.0.1:4320`. Choose **Scenes → a setting →
a role → Real-life challenge · US & Canada → Enter scene**. For the forest café,
leave the setting on After Class Cafe and choose the challenge.

Each role has three goals and six exchanges. The opening and follow-up happen
with the same NPC. Completing both unlocks that goal's action and the next stop.
Stop recording to submit automatically, or use the typed reply. Clear requests
such as “Please say that again” replay without being marked wrong.

The original café and Everyday scenes remain available. Their saves are separate
from challenge checkpoints; unfinished follow-ups and corrections restore.

## Content

There are 33 goals across 11 roles in six settings: shopping, farm, market,
restaurant, amusement park and forest café. Six role contexts are US-based and
five are Canadian. Fictional venue names match the existing scene signs.

Examples include fitting a purchase into a budget and transit schedule,
arranging an exchange, selecting seasonal produce, correcting a bill, managing a
restaurant wait, respecting a friend's ride preferences, handling unavailable
milk, identifying a pickup mix-up and choosing a weather backup before a bus.
Prices include any tax for the exercise. Store policies, stock and times are
explicit fictional facts, not national customs or actual consumer-rights advice.

Prompts and examples are original. The US Department of State's
[Everyday Conversations](https://americanenglish.state.gov/resources/everyday-conversations-learning-american-english),
British Council [speaking functions](https://learnenglish.britishcouncil.org/free-resources/speaking/b1)
and Toronto's [transport overview](https://www.toronto.ca/explore-enjoy/visitor-toronto/getting-around-the-city/)
were reference context; no source dialogue, recording or image was copied.

## Review and fixes

- Openings originally risked awarding a goal early. State tests now require the
  same NPC's follow-up, including after an assisted opening.
- Early Alex greetings seated and trapped the learner. Seating now requires
  Alex to be the current goal; camera focus and physical seating are separate.
- Polite repeat variants could become failed answers. Anchored replay handling
  works both normally and during a pending correction without assessment/state
  changes; in-world requests for help/clarification can still be reviewed.
- Godot JSON versions are floats. Type-sensitive membership rejected browser
  saves; numeric comparison and real v1/v2 JSON roundtrips protect restoration.
- Native tests had used the real progress path. The runner now creates a unique
  temporary project/user directory and removes it in a finally block. Runtime
  tests refuse shared storage. Fresh isolated runs avoid old-save contamination.
- Ollama rejects tuple-style model-array schemas. The private evidence array now
  uses object items and exact length bounds, verified at the wire boundary.
- Real Qwen sometimes miscopied valid quoted evidence. Its output grammar now
  chooses from at most eight exact current-answer excerpts, including the full
  reply; history cannot supply evidence. This uses one inference, with defensive
  quote checking. Natural alternatives still rely on semantic model judgement.
- A long browser flow hit its overall typing budget. Short natural fixture
  replies retain the same record/reload/follow-up/completion assertions; full
  authored examples and review semantics are covered separately.

## Verification

- Full TypeScript suite: 497 tests / 67 files passed.
- All 20 Godot scripts passed in isolated native storage; cleanup verified.
- Real local Qwen probes passed: alternate detailed order, legitimate in-scene
  clarification, incomplete order rejection and generic-agreement rejection.
- Browser suite: 15 cases passed on the full run; its long challenge case hit
  the typing budget. After shortening only fixture replies, that case passed in
  2.6 minutes. All 16 applicable cases are verified across these runs; the
  optional live-transcription case was skipped. Synthetic recording,
  follow-ups, replay, reload/checkpoints, completion and mode isolation passed.
- Web and macOS exports passed. The extracted macOS package started headlessly
  and exited successfully. Native GUI/microphone use remains a pilot check.
- Typecheck, formatting, asset integrity and `git diff --check` passed.
- The scene menu and live counter question were inspected at 573 × 793; desktop
  follow-up and completion views were also inspected. The live local NPC voice
  completed and the input returned to “Your turn.”

Screenshots:

- [Selected challenge mode](../games/cafe/docs/playtest/real-life/in-app-mode.png)
- [Live narrow counter view](../games/cafe/docs/playtest/real-life/in-app-counter.png)
- [Same-NPC follow-up](../games/cafe/docs/playtest/real-life/cafe-challenge-follow-up.png)
- [Completed challenge](../games/cafe/docs/playtest/real-life/cafe-challenge-complete.png)

To reproduce the source checks:

```sh
pnpm test --maxWorkers=4
pnpm typecheck
pnpm format:check
pnpm world:test
python3 tools/world/build.py --test --script challenge_state_test.gd
pnpm world:build
pnpm world:dev
pnpm exec playwright test --config playwright.world.config.ts
```

## Limits

The scene is still a local guest prototype. No recordings are sent to a cloud
service. Browser microphone regressions use synthetic audio. Four model probes
are smoke tests, not a validated language-assessment benchmark. Feedback covers
wording and communication goals, not measured accent, pronunciation, fluency or
CEFR level. Local model speed and browser graphics vary by device; the known
software-renderer performance limit remains. Native clients need the companion
and installed local models. Human microphone/native GUI pilot testing remains.
