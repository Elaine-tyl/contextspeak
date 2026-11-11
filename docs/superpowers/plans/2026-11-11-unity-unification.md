# Unity Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Execute inline with one final fresh-context review.

**Goal:** Replace active Godot game runtimes with a reusable Unity LTS game project without losing existing play or progress.

**Architecture:** The local Unity world uses the existing loopback AI companion. The Unity arcade is a renderer embedded by the authenticated website, which retains audio, recording and trusted scoring. Shared mission data and current saves survive migration.

**Tech Stack:** Unity 6000.3.28f1 LTS, C#, URP, UI Toolkit, Input System, Cinemachine, AI Navigation; existing TypeScript backend.

**Spec:** `docs/superpowers/specs/2026-11-11-unity-unification-design.md`

## Global constraints

- No secret in the client; no public arbitrary-score endpoint.
- Preserve all cafe/Life rules, ten arcade levels, keyboard controls and accessible fallback.
- Keep practice separate from earned progress; retain current local save keys.
- No automatic commit/push in this turn.
- Future districts and native account arcade pairing are explicitly pending.

## Review focus

- Old frame/round messages must not submit answers to the new round.
- Closing/loading scenes while permission or AI work is pending must not resurrect a conversation.
- Input fields must retain focus and typed text during periodic snapshots.
- NavMesh routes must respect furniture and interaction-point clearance.
- Missing engine exports or unavailable local AI must preserve a recovery path and earned data.

### Task 1: LTS project and build path

Files: `games/unity`, `tools/unity/build.py`, package commands, ignore rules.

- [x] Archive old sources; migrate to a clean project/cache with pinned compatible packages.
- [x] Prepare LTS, import verified art, run existing mission/scene/native UI checks.
      Expected: existing cafe and Life checks pass in the LTS Editor. Do not claim LTS verification if installation is blocked.

### Task 2: Reusable Unity world framework

Files: `_Game/{Player,NPC,AI,Core,Data}`, Editor scene/check builders.

- [x] Add failing lifecycle/control/data regression cases.
- [x] Use shared input, camera and navigation adapters; NPC definition/state assets; shared cancellable voice session; safe scene transitions.
- [x] Re-run C# checks and full cafe/Life Web journeys.
      Expected: no task/save behavior changes, no stale voice after close, existing controls work.

### Task 3: Unity arcade renderer

Files: `_Game/Arcade`, WebGL arcade template/plugin, website `UnityGameRoundView`, bridge tests and exported-board journeys.

- [x] Write failing bridge/current-round/focus/result-state regression cases.
- [x] Implement UI Toolkit board for dictation, missing words, pictures, questions, speaking, puzzle, riddle and labelled AI PK using existing host actions.
- [x] Export the arcade, run its browser journeys, keep web fallback.
      Expected: host owns audio/assessment; pending retries do not duplicate scores; question/answer state survives host updates.

### Task 4: Remove active Godot dependency

Files: world/arcade launchers, setup script, `/world`, engine docs, README.

- [x] Point canonical root/download/build commands to Unity; separate Whisper setup from Godot.
- [x] Preserve historical source in a local archive and shared content/licenses in the repository.
- [x] Verify root routes, download integrity and missing-export recovery.
      Expected: normal setup/build/play needs no Godot executable.

### Task 5: Verify and review

- [x] Source/database suite, typecheck, formatting, production website build.
- [x] LTS C# and native UI checks; Web and macOS exports; browser cafe/Life/arcade journeys.
- [x] Fresh read-only review; fix important findings with failing regression then green suite.
- [x] Record build checks separately from human native microphone/visual QA.

Automated implementation/build/browser verification is complete. Human native microphone verification remains a separate pilot gate until an actual recorded answer is observed.

### Native microphone follow-up

- [x] Reproduce the hung authorization request and add permission timeout/cancel regressions (RED→GREEN).
- [x] Replace unsupported macOS Unity authorization with AVFoundation; keep the OS sheet from closing dialogue.
- [ ] Verify the permission library is included in the desktop export and complete a real spoken reply.
- [ ] Refresh browser recording checks and record the final human QA result.
