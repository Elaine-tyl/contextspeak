# Automatic review refresh

Goal: continue the approved vocabulary review milestone by making empty queues show newly due cards without manual refresh. Keep readable functions and existing server-owned FSRS scheduling.

Design: a single cleaned-up timer checks an empty queue at its next due time, capped at 60 seconds to avoid long timer overflow and tolerate clock changes. Returning to a visible tab also checks an empty queue. Never refresh an active recall card automatically, so revealed answers and unconfirmed rating event IDs survive. Failed requests stop automatic retry until the user refreshes. No API, schema, dependency or provider changes.

Files: `apps/web/app/library/ReviewQueue.tsx`, browser regressions in `tests/e2e/reviews.spec.ts`, and the review/progress documentation.

- [x] Add browser regressions: next due card appears automatically; a revealed card and failed rating survive elapsed time/tab return; a failed automatic request does not loop.
- [x] Run the tests before implementation and verify the missing automatic refresh causes failure.
- [x] Implement the timer and visibility cleanup in the existing client component.
- [x] Run review regressions, the complete unit and default browser suites, type/format checks and web build.
- [x] Correct stale implementation status, review the diff and commit locally. Preserve the pending icon-only Close change.

Validation: 109 unit/integration tests passed; the full default browser suite passed (19 tests), then all five review browser tests passed including the added empty-tab-return regression. Typecheck, formatting, extension build and production web build passed. The three new behaviors were verified with failing tests before their implementation or a controlled removal of the visibility listener. Code review found no correctness issues; its coverage gap was closed by the tab-return regression.
