# Compact translation card and library navigation

Goal: keep automatic translation while reducing reading obstruction and making saved vocabulary findable.

- Compact dark card: visible Close, selected text, language controls, translation, truthful save state (Saved locally / Saved to library / Not saved). Native details/summary disclosure hides generated explanations by default. Closing removes only the card, never a saved encounter; late responses must not reopen it. Escape closes too.
- Library: search selected text/context/translated meaning in history, selected text in candidates; source and target filters with clear/reset and no-results state. Backend filtering precedes pagination and remains user-scoped. Filter changes reset pagination, invalidate stale responses, and survive polling. Detected source language matches history filters.
- Reuse existing components and CSS. No UI dependencies, database migration or paid/live-provider tests.

Tasks:
- [x] Write failing API filter/ownership/pagination and browser card/navigation tests.
- [x] Implement compact disclosure, dismiss and saved-state behavior; preserve automatic flow and transient behavior.
- [x] Add validated API filters and accessible library toolbar with race-safe refresh/reset.
- [x] Run unit/integration suite, browser regressions, typecheck, formatting and screenshot QA. Update the installed extension folder and restart normal app; no commit unless requested.

Validation: 99 unit/integration tests, 14 capture/navigation browser tests and the popup regression passed. Typecheck, formatting and production web/extension builds passed. Both saved-state review findings reproduced with failing tests and fixed; re-review clear. Fixture screenshots inspected.
