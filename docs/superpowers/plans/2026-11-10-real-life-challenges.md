# Real-life Challenges Implementation Plan

> **For agentic workers:** Use scoped independent content/backend work and root
> integration. Root owns Godot state/UI/world adapter, exports and browser checks.
> Use test-driven changes and one independent final review. No commit/push.

**Goal:** Harder everyday English with same-NPC follow-ups and grounded feedback.

**Architecture:** Separate authored challenge catalog; backwards-compatible API,
lesson phase and session keys; existing local models and rendering.

**Tech Stack:** Godot 4.7.2, TypeScript/Zod, local Qwen/Whisper/Phonon.

**Spec:** `docs/superpowers/specs/2026-11-10-real-life-challenges-design.md`

## Global constraints

Keep original café and Everyday paths/saves intact. No dependencies/RAG/cloud.
English UI, US and Canada settings as selected by the user.
No financial/consumer-policy promises; venue rules are fictional and explicit.
No changes to scene art, player controls, account data or unrelated work.

## Review focus

- Corrected opening must not skip its follow-up or award the goal early.
- Restore during a follow-up or correction must retain phase, choices and mode.
- Old clients/saves keep history and state semantics; modes cannot overwrite.
- Evidence from earlier history or invented quotes cannot pass a current reply.
- Repeat, scene switches, late results and microphone errors cannot advance.

## Tasks

### 1. Author real-life content (independent scope)

**Files:** new `games/cafe/data/real_life.json`, content contract regression.
**Produces:** exact catalog contract in the spec; 11 roles, 33 goals, 66 exchanges.

- [x] Write/run a failing content test for catalog coverage, follow-up distinction
  and two bounded criteria per exchange.
- [x] Author original US/Canadian situations with clear facts and natural wording;
  examples meet both criteria, alternatives remain valid. Preserve NPC/action IDs.
- [x] Run content checks and inspect samples for contradictions/stereotypes.

### 2. Validate and review challenge exchanges (independent scope)

**Files:** `packages/world/roleplay.ts`, `providers.ts`, a focused challenge
module if needed, corresponding TypeScript tests.
**Consumes:** task 1 catalog. **Produces:** optional API fields from the spec and
unchanged public feedback shape, active-exchange context and grounded review.

- [x] Write/run failing input/history, current-phase, criterion-evidence and
  misleading feedback cases while proving old requests still parse.
- [x] Validate catalog/routes; supply local model with only the selected question,
  criteria, role, setting and data history. Validate two current-reply quotes.
- [x] Run world TS tests; root runs the full project suite after integration.

### 3. Add playable challenge state and selection

**Files:** `roleplay.gd`, `roleplay_controller.gd`, `scene_menu.gd`, new
`cafe_roleplay_world.gd`, targeted Godot regressions and test runner.
**Consumes:** tasks 1/2. **Produces:** two-exchange lesson, difficulty selection,
keyed/migrated saves, original forest-café adapter and repeat handling.

- [x] Write/run failing state tests: opening holds goal, follow-up advances,
  assisted propagation, correction retry, phase restore, legacy migration.
- [x] Implement bounded state/history/session-key helpers and validation.
- [x] Write/run failing runtime tests for mode isolation, selector focus,
  café adapter routes/seating and repeat without assessment/progress changes.
- [x] Integrate UI/controller/API payload; preserve cancellation/fallback controls.
- [x] Run all Godot suites and existing scene runtime cases.

### 4. Integrate, verify and review

**Files:** browser challenge regression and shared scene-menu test helper,
README/playtest notes and this task's verification ledger below.

- [x] Write browser regressions for opening → follow-up, synthetic recording,
  save/reload, correction, completion and switching to Everyday/original café.
- [x] Run full TS tests, typecheck/format, Godot tests and relevant browser suite.
- [x] Check local model on original public samples; export Web/macOS and check
  native startup. Inspect menu/dialogue at desktop/narrow widths.
- [x] Independent focused source review; fix material findings with regressions.
- [x] Update run notes and leave the verified challenge open.

## Ledger

- Scope: user has already delegated routine design choices; proceed inline with
  targeted content/backend delegation under dispatching-parallel-agents. Keep
  current dirty worktree; it is already isolated and includes user work.
- Interface pre-flight: catalog fields, two-exchange indexing, optional API
  defaults and save keys are pinned in the spec for all scopes.

- Review fixes: early Alex greetings no longer seat/lock the learner; all clear
  repeat forms bypass assessment, including pending corrections; native tests
  use unique temporary projects/user data rather than backup/restore of real saves.
- Browser finding: JSON versions arrive as floats in Godot. Numeric comparison
  replaces type-sensitive array membership; v1 and v2 JSON roundtrips are pinned.
- Local-model finding: Ollama rejects tuple-style model-array JSON schema. Use
  an object item schema with exact length 2 and a wire-schema regression.
- Content correction: fixed the article in “a $4 tea”; prose receives manual
  review rather than a source-string change-detector test.
- Review rulings: authored finite dialogue remains the chosen design. Live model,
  browser and native export behaviour are verified by root rather than inferred
  from source review. No pronunciation/proficiency validation is claimed.

- Local-model copy repair: exact current-answer excerpt grammar, capped at eight
  choices, avoids free-copy typos in evidence. Same one inference/public output.
  Four live public sample probes now pass; this does not validate general scoring.
- Content setting names align with the existing 3D venue signage.
- Full TS suite after model changes: 497/497; isolated Godot suite: 20/20.
- Browser verification: 15 passed on the full run; its long challenge case hit
  the keyboard-typing budget. Shorter natural fixture replies preserve every
  checkpoint assertion; the targeted rerun passed in 2.6 minutes. Thus all 16
  applicable cases passed across the runs; one optional live-STT case skipped.
- Final Web/macOS exports and extracted macOS headless startup passed. Menu and
  dialogue were inspected at desktop and 573 × 793; the live local voice
  finished and returned to editable input. Screenshots and reproduction steps
  are in `docs/REAL_LIFE_CHALLENGES_PLAYTEST.md`.
- Typecheck, formatting, pinned asset verification and whitespace checks passed.
  Companion remains available on loopback port 4320; the user's game is left
  at the new Maya opening, without submitting a reply or changing old progress.
- Native GUI/human microphone and broader assessment validation remain pilot
  work. No commit or push was performed for this task.
