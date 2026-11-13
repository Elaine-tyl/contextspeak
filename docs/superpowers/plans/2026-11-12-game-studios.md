# Individual game studios

Use the current React games and locally rendered Three.js props. Keep server grading, level progression, playback gating, recordings, saved runs and controls unchanged. Design choices are delegated by the user.

- Give all twelve modes distinct named environments, palettes and answer surfaces: notebook, crossword, gallery, reading room, microphone studio, mystery desk, puzzle workshop, arena, spelling blocks, sentence track, sound lab and quick-fire console.
- Add one compact, lazy-loaded Three.js stage per active round. React supplies only playback/recording/answer activity; never send answer text to the renderer or infer correctness. Pause offscreen, hidden, during recording, and when motion is disabled. Dispose GPU resources and preserve a lightweight fallback.
- Keep native keyboard/touch answer controls. Add an explicit How to play dialog with Escape, focus containment and focus restoration; no automatic interrupting popup.
- Start with browser regression coverage for distinct modes, actual answer payloads, popup focus, playback gating, recording auto-submit, WebGL fallback, reduced motion and mobile overflow. Keep existing regression flows.
- Verify browser tests, source tests, typecheck, format and production build; inspect screenshots and perform a scoped code review. Do not commit unrelated work.

## Delivery evidence

Implemented all twelve themes and answer surfaces, native instructions dialogs and a lazy Three.js activity scene. Shared GPU cleanup with the lobby. Added short letter/tile/choice effects; animation and pointer tilt honor motion preferences and pause during recording. No assessment, scoring or persistence code changed.

Read-only review caught a timed-game issue: opening instructions during Quick-fire would consume answering time. Instructions are now unavailable while that timer runs; the existing scoring clock is unchanged.

Verified 2026-11-12:
- 589 source tests across 78 files passed (`pnpm test`).
- 14 browser flows passed, including five new studio flows (`playwright.browser-games.config.ts`). All twelve modes, keyboard/focus, phone layout, real WebGL context loss, static rendering when motion is off, payload preservation and microphone auto-submit are covered with isolated fixtures.
- Typecheck, full formatting check, production web build and `git diff --check` passed.
- Inspected actual desktop and mobile renders. Demo screenshots under `docs/images/contextspeak-studio-*.png` use fixture content, not learner records. Test runs write screenshots to their own Playwright artifact directory.
