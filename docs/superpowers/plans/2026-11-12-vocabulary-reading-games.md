# Vocabulary and reading games

Add two independent game tracks with four modes: Word meanings, Word in context, Reading questions, Paragraph order. Use original graded content across ten editorial practice levels, native interactive controls and themed Three.js props. Text games require no audio playback or AI quota. Keep all existing listening/speaking behavior and historical question IDs unchanged.

- Add versioned question content and public passage fields. Solutions and correct paragraph order remain server-owned.
- Keep progress per mode through existing level sessions and server grading; new games require a level session. Scores must not contaminate listening/speaking summaries or vocabulary FSRS.
- Retain compatibility with older catalogs containing only listening/speaking progress. Add new recommendations from game evidence, separate from audio practice.
- Reuse lobby/setup/studios with four category tabs, meaning/context surfaces, readable passages and an undoable paragraph-order board. No audio prerequisite on text games.
- Add database-backed regressions for grading, replay safety, locked levels, account ownership, no-audio submission and skill separation. Add browser flows for four modes, article/order rendering, submission, mobile layout and absence of speech requests.
- Run source suite, browser regressions, typecheck, formatting and production build. Inspect screenshots and review scope before completion.

## Verification and review

Implemented four tracks and sixteen total modes. New versioned content: 60 vocabulary tasks, 30 comprehension passages/questions and 30 independent ordering texts, three tasks per mode per level. Choices use shuffled positions; ordering solutions are not published as passages in another task. Added a cross-catalog regression after review identified that a shared comprehension/order text could disclose the order.

All 593 source tests across 79 files passed, including isolated PostgreSQL grading, independent unlocks, zero-play text submission, replay/ownership checks, wrong-order rejection and unchanged audio gating. All 20 browser flows passed, including the four new modes, reload/resume, complete-level Retry/Next, zero speech requests, undo and phone ordering. Typecheck, format and production build passed. Screenshots use original fixture content and are saved under docs/images/contextspeak-word-*.png and contextspeak-reading-*.png. Read-only review found no remaining material issue after the text separation.
