# 3D Game Lobby Implementation Plan

**Goal:** Replace flat game-mode cards with an actual interactive 3D selection world, keeping existing exercises and saved learner data.

**Approach:** Lazy-load Three.js only for the lobby. Build original low-poly floating practice islands using geometry, with projected native HTML selection buttons for accessible names, keyboard and touch use. Raycasting selects the same modes from the world. Orbit controls support inspection; a plain selector remains when WebGL fails. The existing sound and motion settings apply.

**Constraints:** No Unity/Godot dependency for browser mini-games. No external models, paid assets or API calls. Reached levels still come from the existing catalog. Backend scoring, auth, exercise state and resume links stay unchanged. Dispose GPU resources/listeners on leaving; pause continuous rendering when hidden and respect reduced-motion preference.

## Tasks

- [x] Pin runtime/types dependencies; write failing browser coverage for real canvas selection, fallback and existing mode flow.
- [x] Build a focused Three.js scene module and React lobby wrapper; replace the card grid while preserving mode selection callbacks.
- [x] Verify screenshots, touch-sized controls, reduced motion, fallback, scoring/resume, typecheck, source tests and production build.

**Design choice:** A 3D level-selection world rather than hover-tilted cards. This addresses the supplied screenshot; individual answer controls remain native and stable.

## Verification and review

- Pinned Three.js and its declarations to 0.187.0; reformatted the lockfile to meet existing CI formatting rules.
- Final source suite: 589 tests, 78 files passed. All 9 browser fixture flows passed; typecheck, format and production build passed.
- Browser checks cover actual WebGL rendering, direct geometry raycasting, keyboard and emulated touch selection, OS/device motion preferences, pixel-stable paused rendering, camera drag and exact reset after hover transitions, no-WebGL fallback, and unchanged scoring/resume/library flows. Screenshots use fixture levels rather than real learner records.
- Fixed a track/list-view switch that reused a disposed context by keying the canvas and effect to the semantic scene configuration. Regression asserts the new context remains live.
- Read-only review found right/middle clicks could select a level. Both right-click and cancelled gesture cases were reproduced before fixing primary-pointer ID handling and cancellation cleanup. Reviewer confirmed the guard; no remaining material issue was reported.
- Corrected sticky marker highlighting on mouse leave/blur/reset. Paused-render comparison waits for actual CSS hover transitions instead of an arbitrary delay.
