# Everyday conversation scenarios

Goal: extend the existing saved-vocabulary practice flow with introductions, making plans and asking for help.

Approach: use three original, fixed English partner turns and response goals. Learners choose a saved English expression and use it in their own reply. This is curated role-play, not AI-generated dialogue or automated assessment. Keep the existing saved-example mode and on-device playback/recording; no dependencies, provider calls or migrations.

- [x] Add shared scenario identifiers and task metadata, plus strict API query/submission validation.
- [x] Build scenario tasks from owned successful English encounters. Keep expected meaning hidden until reveal, bind submitted scenario to the server task snapshot and distinguish it in receipts.
- [x] Add scenario selection in the practice hub/page, contextual speaking instructions, and cleanup/reset when switching scenarios. Lock selection while a save is unconfirmed.
- [x] Verify API ownership, language gating, idempotency, stored snapshots and no FSRS updates; verify browser navigation, hidden/revealed content, scenario switching, recording cleanup and unchanged saved-example practice.
- [x] Update documentation, run typecheck/tests/build and commit locally.

Boundary: listening uses a short partner turn with an English meaning guide; speaking asks for an original response using the selected vocabulary. Scenarios target adult English learners and do not change translation-language settings. Fixed prompts do not guarantee every selected word naturally fits: learners may choose another expression. Recordings remain local, and saved self-checks are not mastery evidence.

Verification: 128 unit/API tests, 11 focused Chromium regressions, TypeScript, formatting and production web build passed. A real local API browser flow saved a scenario self-check. Review caught same-path query navigation retaining initial state; the route now keys the practice page by item, lookup and requested scenario so cleanup runs on navigation. No schema migration or extension rebuild is needed.
