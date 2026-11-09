# Game practice Implementation Plan

**Goal:** Add a signed-in game hub for listening and spoken challenges with independent, durable level unlocks. User selected AI opponent and authorized remaining design choices.
**Architecture:** Original immutable versioned content; three levels per skill. Listening has story/drama/literature/novel dictation, picture selection and comprehension. Speaking has riddles, puzzles and PK. Reuse cached Gemini audio, recorder and pronunciation feedback. One GameAttempt table stores server-graded results, never raw audio. Passing three distinct challenges at >=70 in the previous level unlocks the next. FSRS unchanged.

PK is labelled a simulated AI opponent with a fixed visible training benchmark (65/75/85), not live matchmaking or another player. Game marks are practice estimates. Audio generation uses existing quota/cache; speaking automatically assesses completed recordings, with explicit retry after failure. No browser speech fallback.

## Tasks
- [x] Versioned 27-round original catalog and public answer-free view; pure progression tests for distinct passing rounds, skill separation and sequential locks.
- [x] Migration/contracts and authenticated games API. Own server answers/levels/scores, preserve event-ID retries and concurrent coalescing, bound/rate-limit media, recheck account before saving and cascade deletion. Test ownership, forged scores, level bypass, retries and no raw audio.
- [x] Responsive game hub linked from library, skill/level navigation, AI audio, picture choices, typed dictation, comprehension and automatic recorded speaking. Show highlights, IPA, fixed PK benchmark and progression; stop audio/mic on leave/account expiry. Browser tests against fixtures and actual local API.
- [x] Document content, limits and progression. Run all tests/typecheck/browser/build/format, review, commit locally and restart.

## Constraints
No extra dependencies, new provider credentials, paid fallback, copied FluentRead code or third-party story text. English first. Short original excerpts rather than full novels in this release. Immutable round IDs preserve unlock history. Questions/audio are client-visible for playback; this is learning practice, not anti-cheat certification. Three challenge passes are game progression, not CEFR or vocabulary mastery.

## Verification

164 unit/API tests in 33 files and all 47 browser tests passed. TypeScript, formatting and diff checks passed; extension and production web builds verified before commit. Browser checks exercised the actual authenticated local games API, migration, saved distinct progress, pictures, comprehension, dictation and automatic speaking; later voice fixtures isolate the shared six-generation speech quota. No new credentials or personal audio uploads were used. Independent read-only review found no actionable issues. PK result screenshot visually inspected.

Local design choices: English first; 27 short immutable original rounds; three levels and three distinct >=70 passes per prior level; Echo uses a fixed visible benchmark rather than live matchmaking. Game progress is separate from FSRS/CEFR. Full novels, real-player matches and adaptive content generation remain later work.
