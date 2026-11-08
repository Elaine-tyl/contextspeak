# Learner audit improvements

Fix the observed audit findings within the existing layout and backend. Keep comments/readable functions, avoid dependencies, and do not silently rewrite saved translations.

- [x] Regression-test URL state/return-link validation, context-first prompts, scenario refresh, loading feedback and post-result actions.
- [x] Persist library/practice filters in query parameters and carry validated library return links through detail/practice pages. Restore saved list scroll after content loads.
- [x] Persist scenario changes in the practice URL; handle Back/Forward without stale audio or answers.
- [x] Add accessible AI preparation status and nearby Replay / Try again / Next expression actions after grading.
- [x] Compact the existing library header/account panel and use learner-facing language/status names where known.
- [x] Prioritize contextual meaning in future AI translations, version the prompt and smoke-check the public Whether example; keep existing results intact.
- [x] Run relevant unit/API/browser tests, typecheck/format/build; verify screens and commit locally.

Verification: 137 unit/API tests, all 38 browser tests, the popup browser check, typecheck, formatting and both builds passed. Strengthened practice return/filter checks also passed sequentially. The live public Whether smoke test returned 无论 with prompt version 2. Real Chrome verification confirmed the signed-in compact practice list and visible cards. Extension 0.7.2 was copied into the existing test folder, retaining its path and manifest test name; Chrome reload is still required.

Review corrections: restore loaded history pages before consuming scroll position; stop automatic restoration after errors and allow manual retry; retain practice filters for Choose next expression; render sign-in separately from collapsed account settings to avoid hydration/toggle races. Scroll restoration stores only position and bounded page count, never vocabulary/session credentials. Saved translations and review ratings are not rewritten.
