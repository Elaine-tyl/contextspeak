# Godot Arcade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Play the existing eight learning mini-games through a real Godot 2D Web renderer, sharing website accounts, audio and authoritative results.

**Architecture:** Extract the current round hook/controller and mount an allowlisted same-origin Godot iframe. Keep web navigation, account lifecycle, microphone, audio and scoring in the React host. Unity migration follows after the user installs the Editor.

**Tech Stack:** Existing TypeScript/React, Zod, Godot 4.7.2/GDScript, WebGL2, Vitest/Playwright.

**Spec:** docs/superpowers/specs/2026-11-11-engine-split-design.md

## Global Constraints

- No new npm runtime dependency, paid assets, exposed tokens, raw audio in iframe or public grading endpoint.
- Keep current quiz/progression/auth/audio behaviour and English UI; readable functions and focused comments.
- Web preview first; native account pairing and Unity compilation are separate verification gates.
- Keep working web/world clients. Do not commit or push this migration without a new request.

## Review Focus

- Foreign-window or cross-kind messages never trigger API requests (Task 1 tests).
- Lost responses keep exact original event and answer despite new input (Task 1 and 3 tests).
- Same-round status messages preserve input and focus (Task 2 headless test).
- Six long blanks fit mobile and stay answerable without leaking expected answers (Task 2 tests).
- Navigation/account expiry tears down microphone, playback and pending requests (Task 3 browser tests).

### Task 1: Shared round state and bounded bridge

**Files:** apps/web/app/games/{useGameRound.ts,GameRoundView.tsx,GamesPage.tsx,page.tsx,arcade/page.tsx,arcadeProtocol.ts}; apps/web/test/arcade-protocol.test.ts.

**Interfaces:** useGameRound(GameRoundProps) returns existing UI state/actions plus startRecording and submit(blob?, answerOverride?). readArcadeAction(event, origin, frame, round) returns a validated action or null. Never accept event IDs/marks/identity from child.

- [x] Add tests for exact origin/frame/tag, strict action payload, nonblank bounded answers, choices owned by current round and 1–6 gaps matching current round.
- [x] Run tests and observe missing-module failure.
- [x] Extract controller and hook; add explicit answer override and dynamic route base. Keep existing web JSX/behaviour.
- [x] Pass unit/type checks and existing Games browser regressions.

### Task 2: Native Godot board and reproducible export

**Files:** games/arcade/{project.godot,export_presets.cfg,main.tscn,board.gd,picture.gd,web/bridge.js,tests/board_test.gd}; tools/arcade/build.py; package.json; .gitignore.

**Interfaces:** window.ContextSpeakArcade.subscribe(callback) receives JSON snapshots; emit(action,payload) posts to same-origin parent. Board renders snapshot round/result/status, emits actions, never grades locally.

- [x] Write a failing headless board test for eight modes, six inputs, repeated snapshot focus preservation, result/next transition and mobile layout.
- [x] Observe missing script failure; implement native controls and vector illustrations, guarded Web bridge, result actions and adaptive widths.
- [x] Export into ignored apps/web/public/arcade-engine with pinned existing engine/templates; run headless tests and verify export.

### Task 3: Browser host, real journeys and documentation

**Files:** apps/web/app/games/{GodotGameRoundView.tsx,games.css}; tests/e2e/arcade.spec.ts; docs/GAME-ENGINES.md.

**Interfaces:** GodotGameRoundView(GameRoundProps) uses shared hook, verified iframe messages and plain state snapshots. Browser host retains accessible answer feedback/IPA and missing-export fallback.

- [x] Add failing browser test for real WebGL startup and playback→answer→server score→next/reload.
- [x] Implement host and fallback; no answers accepted before completed playback, no recordings sent into iframe.
- [x] Test dictation, gaps, picture, comprehension, all speaking layouts, auto-submission, retry, frame spoofing and navigation cleanup using fixture audio/microphone.
- [x] Run source suite, typecheck/format, production web build and scoped fresh review; fix findings with regression tests.
- [x] Document setup/limitations and Unity installation gate. Restore ordinary dev servers for user preview.
