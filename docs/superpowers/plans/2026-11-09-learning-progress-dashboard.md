# Learning progress and remaining MVP work

Implement the requested dashboard under the established proceed-with-design-choices workflow. English UI; concise readable functions; no RAG/new dependency or migration.

- Owned read-only `/v1/progress`: lifetime vocabulary/review totals, due/paused counts, last-30-day review ratings and difficult vocabulary; recent listening/speaking scored samples, unscorable counts and evidence-based focus; all seven authoritative game-mode access levels.
- Bound scored samples to 100 per source/skill, filtering invalid/unscorable/self-check-only records before sampling. Explain the window/sample and avoid mastery/CEFR claims. Weak dimensions need two different independent scorable tasks; listening focus needs three.
- `/progress`: responsive cards, actionable review/practice links, empty/stale/error/expired states, refresh without losing saved display, periodic session checks and owned URL navigation. Global/library navigation; game-mode deep links.
- Tests: owner separation, empty history, due vs paused, recent vs old ratings, invalid/uncertain/assisted/repeated speech, observed game unlocks, real UI refresh/errors/sign-out/deep links. Run unit/API, browser, type/format/build checks, commit and restart.
- Audit PLANNING against code, update completed milestones and remaining MVP backlog. User asked to complete remaining work; scope clarification is pending for current MVP versus later school/native expansion. Do not call deployment, human scoring validation or unimplemented roadmap items complete.

Scope confirmed: finish the current browser-extension/website MVP, excluding school/native expansion. Dashboard verification: 212 unit/API tests and 53 browser tests passed; typecheck, formatting and diff checks passed. The UI is reviewed through a browser screenshot. Production build passed. A further three-test dashboard browser rerun passed, including 375px layout and non-overlapping review controls. Follow-on work is tracked in docs/MVP_CHECKLIST.md; it is not marked complete by this dashboard slice.
