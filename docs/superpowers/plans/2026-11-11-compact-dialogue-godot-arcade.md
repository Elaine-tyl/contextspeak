# Compact Dialogue and Godot Arcade Plan

> **For agentic workers:** Use superpowers:executing-plans task by task.

**Goal:** Reduce the Unity conversation overlay and restore Godot 2D for the eight learning mini-games, as explicitly requested.

**Architecture:** Unity retains the 3D worlds and voice NPCs. Reuse the existing Godot board and shared website round controller. The host continues to own accounts, recording, audio, grading and progress.

**Spec:** User's two screenshots and request; existing engine-split design applies to the Godot boundary. This supersedes the all-Unity arcade decision, not the completed Unity world framework.

## Constraints and choices

- Preserve current missions, scoring, ten levels, voice controls and save keys.
- Compact cream dialogue: at most 620 CSS/reference pixels wide, reduced typography/spacing, bounded height and scrollable long content. Keep visible typed input and all existing controls.
- `/games` opens Godot rounds; `/games/arcade` stays a compatible alias. `/games/web` provides accessible HTML controls for the same saved run.
- Use the installed pinned Godot 4.7.2 and verified templates; add a reproducible separate setup/build path.
- Keep historical engine source recoverable. No commit or push requested.
- User already delegated routine design choices; implement in this session.

## Review focus

- Long NPC lines/feedback cannot hide recording or Close permanently.
- Same-round status updates cannot erase Godot answers or focus.
- Old Godot clicks cannot submit against a new round.
- Missing export preserves the same account run through HTML fallback.
- Audio quota/errors and uncertain submission retain existing retry behaviour.

## Task 1 — Compact world dialogue

Files: Unity Web template CSS, CafeStyles.uss, native UI checks and world browser tests.

- [x] Add failing real-panel width/height assertions and run RED.
- [x] Reduce overlay widths, padding, type and input height; bound/scroll long content on Web and native.
- [x] Rebuild Web/macOS; verify native layout and browser voice/rehearsal/control journeys.

## Task 2 — Restore Godot mini-games

Files: tools/arcade, games/arcade, GodotGameRoundView, GamesPage routes, bridge tests and arcade journeys.

- [x] Point bridge regression at Godot; reproduce stale-round failure.
- [x] Restore pinned setup/export, preserve explicit round identity, and run headless board checks.
- [x] Restore Godot host/default route and HTML fallback; keep existing shared controller.
- [x] Play all eight exported modes, result/Next/retry, mic cleanup and responsive layouts.
- [x] Run source suite, types, formatting, production build and one scoped final review. Update active engine documentation.
