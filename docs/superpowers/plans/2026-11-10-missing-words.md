# Missing Words Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline. One focused
> fresh review after integration; preserve the already dirty isolated worktree.

**Goal:** Hear a sentence and fill its missing words, with saved feedback/levels.

**Architecture:** Extend existing typed game catalog, submission and run system.
One small grading/content module and reusable input/result UI. Reuse AI speech.

**Tech Stack:** TypeScript, Next.js/React, NestJS, Prisma/PostgreSQL; no additions.

**Spec:** `docs/superpowers/specs/2026-11-10-missing-words-design.md`

## Global constraints

English UI. Thirty original sentences, ten levels, three consistent themes.
No database migration or dependencies. No 3D changes, commit or push.
Preserve content IDs, current speech caching and uncertain-request replay.

## Review focus

- Visible words must not inflate the score for missing words.
- Wrong blank counts, conflicting answer formats and client scores must fail.
- A lost response must not let an edited answer reuse the original event.
- Reload and account ownership must preserve/isolate results and level unlocks.
- Multiple blanks and feedback must fit a narrow screen and be keyboard usable.

### Task 1: Add the complete listening mode

**Files:** contracts `games.ts`; API `games/{gap-fill,catalog,games.service}.ts`;
web `games/{GameRoundView,GapFill,LevelResult,games.css}`; focused unit/API/browser
tests; run notes.

**Interfaces:** `GameRound.gapText?: string[]`, submission `gapAnswers?: string[]`,
receipt `feedback.gapFill?: GapFillFeedback`; `gradeGapFill(expected, answers)`;
public catalog strips private `gapAnswers`. Existing level runs consume the
new mode through `gameModes`, with no database schema changes.

- [x] Add failing content/scoring tests, then minimum types/catalog/grading.
  Verify reconstruction, 1/2/3/4 blanks and fractional scores with literal cases.
- [x] Add failing API regressions, then wire validation, saved feedback and mode
  progression. Verify invalid shape, no audio play, forgery, replay and ownership.
- [x] Add failing browser regression, then inline blank inputs/correction UI.
  Verify playback gate, corrections, uncertain retry, reload, level Retry/Next.
- [x] Run full source tests, typecheck, formatting, relevant Games browser cases
  and web build. Inspect narrow UI and local AI speech. Expected: all pass.
- [x] Obtain one scoped independent review, fix material findings with regressions
  and update notes with actual verification. Leave the new mode easy to open.

## Ledger

- Existing linked worktree retained; no unrelated changes reset or staged.
- Pre-flight: API feedback and UI consume the same ordered gap-answer contract;
  existing level/session services accept kinds from the shared mode list.
- Routine choice: website Listening games, not the 3D world; clear existing home
  for short listening activities. User can reach it directly with `?mode=gap-fill`.
- Review fix: require a level session for Missing words. Unscoped legacy
  submissions otherwise raised shared listening access; regression failed
  (201 vs 400), then passed. New mode also starts at 1 for legacy users.
- Browser fix: await Next question and disable playback during the transition.
  A held-read regression reproduced audio being requested for the previous
  question. It now passes with the rest of the Games suite.
- Review choice: extra words in one blank receive a wrong-answer correction,
  rather than a format error; documented in the spec and player notes. Cost:
  accidentally pasted extra text loses that blank's mark; Retry remains available.
- Final verification: 501 source tests / 68 files; all 8 Games browser cases;
  typecheck, formatting, whitespace and production web build passed.
- Mobile input/feedback screenshots are under `docs/playtest/missing-words`.
  A local Phonon sample passed WAV/non-silence validation (2.16 seconds).
- After the user completed Google verification, real local AI audio completed
  in the normal signed-in website. The first sentence is left open, empty and
  ready to answer. The normal app and existing 3D companion remain running.
  No practice marks, commit or push were added to the user's account/repository.
