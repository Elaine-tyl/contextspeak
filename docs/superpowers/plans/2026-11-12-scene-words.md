# 3D Scene Words Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan in the existing worktree.

**Goal:** Add an immersive listening/cloze game inspired by the supplied room-and-word screenshots.

**Architecture:** A new `scene-words` listening mode shares level sessions, AI speech caching, per-word grading and saved feedback. Thirty original prompts cover ten levels and seven reusable rooms. An optional procedural Three.js room decorates native HTML controls; a CSS fallback keeps the game playable without WebGL. Editorial word notes are returned only after grading.

**Tech Stack:** Existing React/TypeScript, Three.js and NestJS; no new dependency, engine, paid assets or external download.

## Constraints and review focus

- English UI; target words and examples have concise Chinese glosses as learning content.
- Each scene has one missing word. Listen first, fill, check, then see corrections and word notes.
- Scores, ownership, idempotency and unlocks are server-owned; the new mode starts at level one.
- Existing modes and saved IDs are preserved. Voice failures keep answers editable after a completed earlier play and never create a grade.
- Respect motion/sound preferences, keyboard controls, phone layout and WebGL failure.
- Release graphics resources and stop cosmetic animation when hidden; no unnecessary second renderer.

## Tasks

- [x] Write failing catalog and API regressions for the new mode, private notes, ownership, grading and unlocks.
- [x] Add contract, authored content and grading integration, preserving existing game behavior.
- [x] Write browser tests for the 3D room, native input, feedback, no-WebGL fallback and mobile layout.
- [x] Build a compact dark cloze panel, original room geometry and post-answer word notebook with AI speaker icons.
- [x] Run regression/type/format/build checks; verify the real signed-in game and take screenshots.
- [x] Request a focused read-only review and resolve important findings.

## Verification

- Full source/API suite: 608 tests across 84 files passed; after adding a legacy-access guard regression, all three Scene words API tests passed.
- Browser game suite: 34 cases passed, including all mode studios, correct/wrong scene answers, phone/no-WebGL rendering and completed-run notebook reload.
- The completion notebook regression failed before the fix; the saved word notes are now included in the result review with working speech controls.
- Signed-in Chrome: level-one setup, real 3D bedroom and live AI narration verified. Playback completion focused and enabled the blank. No artificial learner score was saved.
- Typecheck, repository formatting, diff whitespace checks and the production web build passed.
- Live preview: `docs/images/scene-words-live.jpg`.
- Focused read-only review resolved its one important finding: retain the last word notebook on completion. Mobile lobby test now derives mode count rather than assuming eight listening games.
