# Classified games and scored level runs

Implement the user's requested flow under the earlier instruction to proceed with design choices. Keep UI English, readable functions and regression tests.

- Seven modes: dictation, picture choice, listening questions, speaking questions, riddles, puzzles and AI PK. Existing open-ended speaking excerpts become Speaking questions. Each mode has ten levels and available reviewed themes/source filters; do not duplicate or mislabel text to fill gaps.
- A server-owned level run selects up to three different matching tasks. Store its ordered IDs, filters, owner and completion score in a small additive Prisma model. Existing unscoped game receipts and earned access are preserved.
- Submit one answer per run task in order, through existing scoring. The server computes the mean only after every task has a reliable score; any unscorable task prevents a numeric level pass. At 70+, Next unlocks the next level for this mode only. Retry creates a fresh run without erasing previous results. At Level 10 Next is disabled.
- Result view has Retry and Next, with expandable per-answer feedback. Nonfinal tasks keep automatic speech feedback and explicit Next question. Whole-level scoring appears automatically after the last saved answer.
- Protect ownership, start/answer idempotency, ordering, locks, incomplete/uncertain runs, concurrent submissions and deletion. Resume runs from URL on refresh; no raw audio storage. Retain credited-source display and filters.
- Verify failing regressions before implementation. Run API/unit and browser tests, migration/static checks and production build; commit locally and restart the normal app. No RAG, new provider or dependency.

## Verification

- 205 unit/API tests across 41 files passed. Six level-run API regressions cover starts, ownership, ordering, scoring, mode isolation, low/uncertain results, Level 10, concurrent events, deletion and legacy access. Catalog tests check all seven modes at all ten levels.
- 51 browser tests passed. New flows exercise scored multi-question runs, Retry/Next, refresh recovery, each speaking mode, choice questions, source/theme selection, empty sources, failure retries and microphone cleanup.
- The new browser regression exposed a navigation lock left behind when the last question unmounted; the parent now releases it on confirmed completion. Test helpers wait for the new server-confirmed run URL rather than reading the prior run during async starts.
- Typecheck, formatting, diff checks, committed-corpus validation, extension build and production web build passed. Reviewed the saved level-result screenshots.
- One additive migration introduces owned level sessions and optional scoped attempts; existing rows and source texts remain unchanged. No new dependency, provider or RAG.
