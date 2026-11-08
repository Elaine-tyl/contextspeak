# Vocabulary review queue and spaced repetition

Goal: turn explicitly selected vocabulary candidates into self-rated recall practice without using translation lookups as mastery evidence.

Design: add a server-owned FSRS review card to each enrolled learning item, freeze the selected successful translation as the review answer, store due time/version and a small immutable rating log. Use pinned MIT ts-fsrs 6.0.0 (FSRS-7, retention 0.9, fuzz disabled). Only due cards can be graded. User-scoped event IDs make repeated submissions idempotent; card versions reject stale tabs. A transaction locks the learner row before scheduling, serializing event IDs across that learner’s cards. No Gemini calls are needed.

UI: Start reviewing on a vocabulary candidate, then a Review queue navigation item. Show one due prompt, reveal its saved answer, then Again/Hard/Good/Easy controls. Empty queues show the next due time. Keep components and scheduler helpers separate from the existing large library page; use readable functions and no speculative abstractions.

Non-goals: automatic enrollment, speech/listening assessment, inferred mastery, parameter training, calendar notifications, bulk decks or school workflows.

- [x] Add scheduling and API regressions before implementation: ownership, successful-answer requirement, no duplicate enrollment, due gating, idempotent rating and stale version handling.
- [x] Add migration, contracts and focused review service/controller using server time and the pinned scheduler.
- [x] Add compact candidate enrollment and a focused review queue component with reveal/rating controls and retry-safe submission.
- [x] Browser-test enrollment → reveal → grade → next due, grading failures and account ownership; run full regression checks and production builds.
- [x] Review, remove redundant code, document limitations and commit the finished milestone. Preserve user data; no push unless requested.

Validation: 103 unit/integration tests, 16 default browser tests and one popup test passed, including a real local API enrollment/review flow. Typecheck, formatting and both production builds passed. Read-only review found no important issues. Screen inspected using deterministic English–Chinese fixtures.
