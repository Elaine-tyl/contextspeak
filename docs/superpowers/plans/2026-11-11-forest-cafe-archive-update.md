# Forest Café Archive Update Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement and verify this supplied update in the existing isolated worktree.

**Goal:** Integrate `ContextSpeak-forest-cafe-slice_1.zip` into the working product and make the updated Unity café and Godot games playable.

**Architecture:** Compare the supplied snapshot against its shared `c6a63f1` base, then apply only its changes to current `196fda8`. Preserve current CI and narrow-screen fixes. Keep the existing companion as the speech/review boundary and deterministic C# rules as the authority for orders, money and progress.

**Tech Stack:** Unity 6000.3.28f1/C#, Godot 4.7.2/GDScript, existing TypeScript companion/API/Next.js website.

**Spec:** Supplied `docs/FOREST-CAFE-SLICE.md` and source/tests inside the uploaded archive. Existing Life world and library remain available.

## Global Constraints

- Preserve existing save keys, account data and microphone recordings.
- Keep credentials on the server; reuse the companion's authenticated endpoints.
- Pronunciation is not scored from text. Forest Café IELTS criteria are practice feedback, not an official test score.
- Import source and required original assets; omit build caches and redundant screenshot exports.
- Preserve the fixed Playwright CI environment and 320px regression coverage.

## Review Focus

- Supplied snapshot predates current fixes: merging must not revert CI or mobile layout.
- Existing Life/café scenes must still load and preserve player controls/save keys.
- Native/browser voice review must not charge money, advance a rehearsal, or accept unrelated corrections.
- Food delivery, seats and fishing areas must remain reachable after real Unity scene generation.
- New Godot modes must share host scoring/progress, handle input correctly and fit phone layouts.

### Task 1: Integrate supplied source and validate web/2D contracts

**Files:** Supplied changes in `packages/`, `apps/`, `games/arcade/`, tools and documentation; retain current `.github/workflows/ci.yml`, `tests/e2e/redesign.spec.ts` and library CSS.

**Interfaces:** Existing game submission/level routes consume new game kinds and picture IDs; Godot and HTML clients consume the same host protocol. The companion gains authenticated `POST /slice/review` with language-only feedback.

- [x] Validate archive paths, select additions/changes against the shared base, record the import manifest, and apply conflict-free changes.
- [x] Run `pnpm typecheck`, `pnpm test`, format checks and the supplied Godot board tests; inspect actual failures before corrections.
- [x] Add regression tests for integration defects found; observe failures, fix narrowly, and rerun affected suites.
- [x] Export Godot games and run browser journeys for new modes and preserved mobile layout.

Expected: supplied modes work with existing accounts/progress; current mobile regressions pass.

### Task 2: Compile and validate the Unity slice

**Files:** `games/unity/Assets/_Game/Slice/`, shared controller/bootstrap, scene builders, menu illustrations, and supplied C# checks.

**Interfaces:** `WorldScenes.Open("ForestSlice")` loads the new addressable scene; runtime services reuse companion session, STT, TTS and review. Core rules remain UnityEngine-free.

- [x] Run the real Editor verification via `pnpm unity:test`; fix compile/runtime/scene defects with focused checks.
- [x] Preserve old Life/café scene regressions; build WebGL and macOS from the pinned Editor.
- [x] Add browser journeys for loading the slice, opening menu/settings, ordering and closing panels without breaking movement.
- [x] Exercise the browser slice and inspect screenshots; verify desktop build/startup, walking and map controls. Final desktop visual automation was limited by a computer-control timeout; a human microphone check remains separate.

Expected: real Editor checks and builds succeed; supplied stub-only compile claims are replaced by verified local results.

### Task 3: Finish integration and user preview

**Files:** Entry links/docs, focused integration regressions, CI only if new portable C# checks can run there without Editor.

**Interfaces:** Website/game preview provides a direct Forest Café entry; source setup commands reproduce the update while current CI checks remain intact.

- [x] Run the complete source suite, meaningful browser suites, engine checks and build checks after fixes.
- [x] Request one fresh review of the final change; fix important findings with regression evidence.
- [x] Restore regular development services and open the updated preview for the user.
- [x] Report verified behavior and material limits, distinguishing fixture audio from a real human microphone test.

Expected: user can access the updated slice; no current fixes or learning data regress.
