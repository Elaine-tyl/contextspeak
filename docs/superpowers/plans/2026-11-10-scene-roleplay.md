# Scene Roleplay Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans with independent backend and scenery tasks delegated under dispatching-parallel-agents. Track steps below.

**Goal:** Play four role-based scenes with automatic answer checking, correction readback and visible task effects.

**Architecture:** Shared authored catalog; bounded local AI assessment; pure Godot lesson state; independent scene renderer and controller using existing recording/voice components.

**Tech Stack:** Godot 4.7.2, TypeScript, Zod, Qwen, Whisper, Phonon, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-10-scene-roleplay-design.md`

## Global Constraints

- Preserve existing cafe flow, saves and test previews.
- Four scenes, two roles each, three authored goals per role; English UI.
- Local-only AI and audio; existing CC0 assets and original geometry.
- A correction requires readback; text matching is not pronunciation assessment.
- Switches cancel all old work; prototype results stay outside account scores.

## Review Focus

- Irrelevant or instructional learner input must not award progress.
- Pending correction survives switching/reload and cannot be skipped.
- A late AI/voice result must not affect the newly selected role.
- Completed roles disable submissions and restore each visual reward only once.
- Menu/dialogue keyboard focus must not accidentally move or submit the player.

### Task 1: Catalog and assessment

Files: `games/cafe/data/roleplay.json`, `packages/world/roleplay.ts`, providers/server and regression tests.
Interface: POST `/roleplay` input `{scene,role,turn,text,history:string[]}`; result `{accepted,correction,feedback,acknowledgement}`. Catalog scene `{id,title,roles:[{id,label,npc,turns:[{prompt,goal,sample,action}]}]}`.

- [x] Write failing schema/assessment/API tests, implement catalog and local provider, run targeted Vitest.

### Task 2: Lesson state and selection

Files: `roleplay.gd`, `roleplay_controller.gd`, `scene_menu.gd`, cafe/interface integration and Godot tests.
Interface: state `start(scene,role)`, `respond(text,review)`, `repeat_correction(text)`, `restore(value)`. Advances return authored action IDs only. Controller owns per-role local save and request epoch checks.

- [x] Write failing progression/restore tests; implement and verify.
- [x] Add scene and role menu, separate saves, automatic voice/assessment and readable correction UI.
- [x] Test switching, cafe compatibility, completed controls and cancellation.

### Task 3: Immersive scenes

Files: `roleplay_world.gd`, placement tests and provenance documentation.
Interface: `configure(scene,role_label,npc_name)`, `move_player(delta,paused)`, `near_npc()`, `walk_to_npc()`, `apply_action(id)`, `reset_actions()`.

- [x] Write failing scene/action tests; create four distinct environments and idempotent actions; verify headless.
- [x] Search and document primary free resource sources and actual reused assets.

### Task 4: Integration and delivery

- [x] Add browser scene/role, correction and stale-response regressions; exercise recording path and local AI.
- [x] Run TypeScript suite, typecheck, formatting, Godot suite, Web/macOS exports and native startup.
- [x] Inspect browser screenshots; request fresh code review, fix material findings, update playtest notes.

## Execution record

- Existing isolated linked worktree verified; all prior uncommitted work retained.
- Builder choice: three-turn lessons and two roles bound this first expansion to complete, testable tasks.
- Backend and scenery delegated to non-overlapping files; root owns state, UI and integration. No commit/push requested for this expansion.

- Final review: three material findings fixed with failing-then-passing regressions: modal keyboard focus, equivalent currency/contraction readbacks, and authored apple quantities.
- Verification: 328 TypeScript tests; typecheck and formatting; all 11 Godot scripts (four-scene physics script now runs to its own completion); Web/macOS exports and native startup; real local Qwen cases and synthetic Phonon/Whisper/Qwen pipeline. Browser suite final result is recorded in the playtest report.
- Final browser regression: 10/10 passed (4.9m). Local game companion remains running at http://127.0.0.1:4320. Native startup exit 0; human-microphone and full packaged desktop GUI checks are documented as pilot follow-up, not claimed complete.
