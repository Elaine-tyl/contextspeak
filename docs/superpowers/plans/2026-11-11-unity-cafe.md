# Unity Cafe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** A native Unity cafe vertical slice with walking, spoken ordering and reviewed friend conversation on Web and macOS.

**Architecture:** C# mission model, procedural/imported URP scene, Unity runtime UI and bounded local-companion client. Shared companion retains AI; existing clients remain available.

**Tech Stack:** Unity6000.6.8f1, URP17.6.0, Unity glTFast6.20.0, C#, existing local AI companion and Web microphone bridge.

**Spec:** docs/superpowers/specs/2026-11-11-unity-cafe-design.md

## Global Constraints

- No cloud keys, login changes, public inference API or copied game code.
- Verify existing CC0 art before copying it to ignored generated Unity assets.
- Separate validated local Unity progress; no false account score or overwritten Godot save.
- Compile and launch actual exports before claiming platform completion; no automatic commit/push.

## Review Focus

- Ambiguous/negative confirmation cannot complete an order (Task1 regression).
- Corrupt/partial saves cannot skip prerequisites (Task1 regression).
- Closing or changing dialogue cancels late microphone/network callbacks (Task2 browser test).
- Long replies/voice failures remain retryable without advancing (Task2 browser test).
- Browser/native exports load actual art/materials and walking paths remain clear (Task3 geometry and real export checks).

### Task 1: Project and mission model

**Files:** games/unity-cafe/{Packages,ProjectSettings,Assets/Scripts/CafeMission.cs,Assets/Editor/CafeChecks.cs}; tools/unity/build.py.
**Interfaces:** CafeMission holds validated order/friend state; Parse/Apply return deterministic NPC prompts; ReviewFriend(review,text) only advances accepted turns; Restore validates persisted JSON.

- [x] Add Editor regression assertions for quantities/budget/negation/confirmation/history/restore.
- [x] Observe failed compile/check before model exists; implement readable C# model and checks.
- [x] Pin editor/packages, prepare verified art and pass batch checks.

### Task 2: Runtime world and local AI

**Files:** Assets/Scripts/{CafeGame,CafeClient,CafeRecorder,CafeInterface,CafePlayer}.cs; Assets/Plugins/WebGL; Assets/WebGLTemplates/ContextSpeak.
**Interfaces:** CafeClient only loopback4320, bounded payloads and cancellation generation; native/Web recorder yields PCM WAV <=20s; CafeGame owns state/dialogue and automatic NPC speech/recorded answer submission.

- [x] Add targeted C# guards and Web plugin regression for stale/cancelled callbacks.
- [x] Implement smooth walking/camera, nearby E interaction, cafe UI, local AI calls and separate save.
- [x] Test actual Web flow using the existing fixture companion and microphone cancellation; test actual public local voice.

### Task 3: Scene, exports and verification

**Files:** Assets/Editor/CafeBuild.cs; tools/unity/build.py; games/unity-cafe/tests; docs/GAME-ENGINES.md.
**Interfaces:** CafeBuild.BuildWeb/BuildMac/Verify create the same saved scene, URP settings and imported free art; scripts emit ignored exports and copy shared microphone scripts.

- [x] Build a cozy cafe with coherent scale, clear counter/table paths, furniture, windows, plants and stylized NPCs.
- [x] Check scene bounds, colliders, player controls and task waypoints in the Editor regression.
- [x] Compile Web/macOS; launch the actual Web export and verify ordering/friend conversation, practice isolation and recording lifecycle.
- [ ] Finish manual macOS rendering/input/microphone QA once the Mac is unlocked.
- [x] Fresh scoped code review, fix findings with regressions; finish website checks and restore ordinary dev servers.
