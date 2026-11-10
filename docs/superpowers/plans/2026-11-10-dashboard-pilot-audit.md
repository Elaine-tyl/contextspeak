# Dashboard pilot usability fixes

> **For agentic workers:** Use superpowers:executing-plans to implement task by task.

**Goal:** A learner can choose a short review/listen/speak task, confirm goals and understand a compact, honest priority list.

**Architecture:** Preserve the existing green website, endpoints and learning evidence. Add accurate analysis eligibility and goal matches to the owned personalization response; use evidence contributions without rewarding missing frequency. Preserve practice mode in existing URLs and use safe allowlisted Dashboard return links. No dependency, model call or schema migration is needed.

**Evidence:** Current audit screenshots in `/private/tmp/contextspeak-learner-audit`: Dashboard uses excessive first-screen space; goal settings and 20-item priorities dominate scrolling; Speaking shortcut ultimately selects Listening. Stored suggestions exist but show misleadingly precise uncalibrated confidence percentages. Code confirms total-context count differs from required new-context eligibility, and missing-weight normalization can rank an unknown-frequency word above the identical word with known frequency.

**Design choices:** Scoped refinement of the existing design, not a new brand or route. Put an actionable Review / Listen / Speak strip above compact statistics. Show five priority rows, expandable full list and explanations, target-language labels, and matching confirmed scenarios. Keep numeric priority only in explanation details, never as an apparent mastery mark. Hide implementation math and source details behind an accessible disclosure while retaining attribution. Keep Save/Reset goals, mark unsaved drafts and disable unintended suggestion actions during drafts; analysis button reflects new-context readiness and cached status. Preserve dirty drafts on refresh, indicate external changes and require refresh/reapply rather than silently overwriting them. Record button-triggered failures as honest uncertain outcomes.

- [x] Add failing pure/API tests for missing-frequency boost, goal matches and analysis freshness.
- [x] Implement bounded owned response and ranking fixes; verify existing behavior.
- [x] Add failing navigation tests; preserve requested mode and safe Dashboard return navigation across hub/detail/practice.
- [x] Refine Dashboard and plan layout using existing components and CSS. No custom art or external assets needed.
- [x] Test actual Chrome journey, goal Save/Reset, eligibility/errors, keyboard access, mobile reflow and regression suite. Use disposable learners for persisted test changes; inspect the real account without altering its results.
- [x] Record accepted before/after screenshots and concise findings, limitations and pilot checklist. Keep real human speech calibration and deployment/recovery gates open.

Ruling: keep the original practice-hub URL unchanged for default Listening; append mode only when Speaking is requested. This avoids unnecessary URL churn while supporting the Dashboard shortcut. Existing tests that intentionally switch from Speaking to dictation now explicitly select Listening; they also assert that scenario changes preserve Speaking and still stop/clear recordings.

Ruling: keep screenshots showing the real learner account outside Git. Public documentation records the steps, findings and limits without copying private vocabulary into the repository.

Verification: 261 unit/database tests across 56 files passed; 60 browser tests passed after fixing fixture teardown and adapting old dictation tests to explicit mode selection. Typecheck, formatting, website and extension builds, and diff checks passed. Independent code review found a scenario/mode consistency issue, fixed with regression coverage. Normal development app restarted with original database. Screenshots and direct Chrome checks document the real-account experience; mutations/failure cases used disposable learners. No private AI analysis or microphone recording was submitted.

Final live-account follow-up: quick-start now selects a short expression (at most six words/60 characters), displays it, and falls back to the library if none qualifies. Longer saved selections remain available. Added a regression proving a long passage does not override a shorter quick-start candidate. Empty skill summaries explain that older/ineligible history marks are excluded.
