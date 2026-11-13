# Reading Recall Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement the steps below in the current worktree.

**Goal:** Start a passage narration, see marked keywords, then fill those words only after playback finishes.

**Architecture:** Add a recall flag to new reading-question editions; retain existing MCQ editions for saved runs. Curated keyword lists produce 3–6 gaps per passage. Reuse cached AI speech, gap grading and native letter inputs. No new game engine or dependencies.

**Tech Stack:** React, TypeScript, NestJS, existing AI speech adapter and PostgreSQL.

## Constraints and review focus

- English UI; concise readable functions; preserve existing progress and IDs.
- Failed or interrupted playback leaves the marked preview visible and submission unavailable.
- Replay after completion does not reveal words or erase typed answers.
- Server validates owned level session, completed playback and all blanks; event retries remain idempotent.
- Old MCQ runs stay answerable; unrelated text games require no speech.
- Respect paragraph breaks, keyboard input, small screens and reduced motion.

## Tasks

- [x] Add failing API tests for active recall editions, old-edition compatibility, playback gate, partial marks and replay deduplication.
- [x] Create curated keyword data and recall edition builder; update contract/audio classification and server grading.
- [x] Add browser regression for highlighted preview → playback → blanks, failure/retry, replay and saved feedback.
- [x] Implement a small ReadingRecall component and wire the existing Start/audio/submit controls.
- [x] Run regression suites, type/format/build checks and a live-page smoke test. Review before reporting completion.

## Verification

- 606 tests across 83 files passed.
- 30 browser flows passed, including preview/completion, retry, interruption, replay and multi-chunk advanced narration.
- Real signed-in narration completed and replaced keywords with inputs; no learner score was submitted during live testing.
- Catalog-wide speech schema checks cover all 45 recall editions. Review identified the original 600-character limit; continuous sentence-boundary chunks resolve it without changing endpoint quotas.
