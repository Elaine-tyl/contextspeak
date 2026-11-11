# Cafe world implementation plan

**Goal:** Deliver the first free local 3D cafe speaking mission on Web and desktop.

**Spec:** `docs/superpowers/specs/2026-11-10-cafe-world-design.md`.

**Architecture:** Keep the existing website/API unchanged except a Games entry and launch page. A standalone Godot project and loopback companion share a bounded protocol; authored mission rules own completion. Existing Phonon playback and local Qwen are reused without loading `.env.local`.

## Tasks

- [x] Pin and install project-local Godot/export tools and whisper.cpp; document repeatable setup without global package changes.
- [x] Add failing mission-state tests, then implement the cafe order rules and validated local progress.
- [x] Add companion regressions for tokens/origins, bounded audio, intent validation, model errors and temporary-file cleanup; implement local transcription, intent understanding and cached speech.
- [x] Build the cafe scene, accessible instructions/dialogue controls, microphone lifecycle and typed fallback. Keep NPC voice separate from recording.
- [x] Export Web/macOS, add the website entry, and verify the mission through browser/native smoke tests and synthetic audio.
- [x] Run existing regressions affected by navigation and type/format/build checks; review the result and document exact prototype limits.

## Review focus

No cloud calls or credentials in the local companion; no arbitrary model URLs/files/commands; transcript/prompt injection cannot grant a pass; invalid saves cannot bypass the order rules; rejected permissions/errors retain the task; late audio/inference callbacks cannot affect a reset or closed conversation. Keep the uncommitted three-colour redesign intact.

## Verified outcome

- 273 existing/new unit and API regressions passed (61 files); nine companion
  checks cover origins/tokens, audio bounds, cancellation and temporary cleanup.
- Godot headless mission and compact-dialogue checks passed, including contracted negatives, JSON
  integer boundaries, restored progress, budget correction and exact audio caps.
- Two exported-WebGL browser regressions passed: full task/save/restore and
  synthetic microphone → automatic submission → next NPC question.
- Local Phonon → Whisper transcribed the public synthetic line correctly;
  strict-schema Qwen interpreted a paraphrase without inventing drink options.
- Web/macOS exports, native headless startup, TypeScript and website build passed.
- Native human microphone input, other browsers/platforms and pronunciation
  accuracy are not established by these checks. README documents these limits.

Review fixes: explicit negation (including contractions) cannot confirm orders;
20-second PCM trimming handles delayed frame timers; disconnected requests abort
provider work; AudioEffectCapture polling replaces the thread-dependent recorder
in the no-thread Web build. Accessible status exposes recording failures.
