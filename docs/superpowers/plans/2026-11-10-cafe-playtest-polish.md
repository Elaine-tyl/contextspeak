# Café playtest and polish implementation plan

**Goal:** Make the café's recording-to-task loop reliable and its scene inviting.
**Architecture:** Existing Godot client + loopback Whisper/Qwen/Phonon companion.
**Spec:** ../specs/2026-11-10-cafe-playtest-polish.md

- [x] Capture the current arrival, dialogue and follow-up states; run baseline tests.
- [x] Reproduce completion/option-editing/silence bugs with failing mission/audio
  tests, implement bounded fixes, and verify them in Godot.
- [x] Add a live local microphone regression using public synthetic audio through
  the actual game and Whisper; include follow-ups, completion and recoverable failure.
- [x] Replace placeholder characters/props with attributed CC0 models; tune
  lighting, camera, NPC animation and interface hierarchy. Keep the code focused.
- [x] Verify browser/native exports, full regressions, types/format and fresh
  screenshots, then record exact evidence and limits in the audit report.

Review focus: no guessed agreement, no stale completion controls after closing,
no silent-recording advancement, model/permission failures preserve orders,
character assets and camera do not obscure interaction or break movement.

## Outcome

Completed in the existing worktree. Actual browser AudioWorklet capture replaced
render-dependent Web recording after the live test exposed lost samples.
Verified all four spoken replies through real local Whisper and Phonon, plus
three ordinary browser regressions, Godot mission/UI checks, 276 unit/API tests,
types, formatting, asset hashes and Web/macOS exports. Fresh evidence and limits:
`docs/CAFE_PLAYTEST.md`. No commit, push or account-data changes were performed.

Ruling: use selected pinned CC0 Kenney models instead of generated concept art;
they provide actual lightweight 3D geometry/animation in the existing engine.
Ruling: browser audio owns its PCM capture in an AudioWorklet; native capture
continues in Godot. Both submit bounded WAV to the same local companion.
