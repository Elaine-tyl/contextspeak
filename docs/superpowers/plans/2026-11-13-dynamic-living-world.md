# Dynamic Living World Implementation Plan

> Execute inline with superpowers:executing-plans. One fresh final read-only review. No automatic commit or push.

**Goal:** Deliver a playable, persistent three-location speaking story and reusable contextual activity framework.
**Architecture:** Pure C# state/template engine with explicit validated actions; a separate Unity director owns native UI, proximity invitations and existing speech adapters. Reuse current scenes/prefabs, avoid rebuilding the open scene.
**Tech Stack:** Unity6.3/C#, UI Toolkit, existing local companion; .NET8 regression runner.
**Spec:** ../specs/2026-11-13-dynamic-living-world-design.md

## Review Focus
- Stale speech callbacks, changed choices or scene transitions must never progress the wrong stage.
- Corrections, typed rehearsals and provider failures must not bypass spoken verification.
- Saved relationships, promises and outcomes must remain consistent without duplicate rewards.
- NPC invitations must not interrupt recording, existing dialogue, menus or character creation.
- Deferred/unavailable destinations must remain visible and recoverable instead of silently failing.

### Task1: Story rules and authored content
- [x] Write failing C# regressions for the six-turn main chain, polite declines/details, repeat verification, stale callbacks, replay prevention, time/weather eligibility, save recovery and invalid action templates.
- [x] Implement LivingWorldState, NPCMemorySystem, BranchingQuestSystem, ScenarioGenerator and original validated content. Run the .NET regressions.

### Task2: Unity integration and interaction spaces
- [x] Add ScriptableObject catalog, standalone DynamicLifeEventManager, compact dialogue/journal UI and existing speech adapters. Suspend ordinary controls only while the story UI owns them; persist at each change.
- [x] Prepare compact reusable activity/NPC prefabs from current assets. Add routing to existing Airport and proximity invitations. Expose next target and memories, but never auto-record.
- [x] Verify native UI, generated assets and runtime recovery without saving/replacing the artist's open scene.

### Task3: End-to-end verification and delivery
- [ ] Run C# regression suite, relevant existing tests and native play checks; test the complete chain with explicitly labelled test inputs. Check real local AI assessment with public sample text where available.
- [x] Perform fresh read-only review, fix substantive findings with regressions, and document completed/limited features. Distinguish native/source verification from distributable/browser verification.

## Verification handoff — 2026-11-13

60 pure story regressions, 335 existing café checks and 7 companion API tests pass. Real local AI assessment and generated TTS→STT sample passed. Native catalog/district preparation succeeded. Runtime/Editor sources were compiled separately using installed Unity compiler/references, without replacing the running Editor's assemblies. Read-only review findings on completion guards, scene inclusion, stale Addressables handles, profile isolation and test timeout were addressed.

Native play/UI/microphone traversal and final browser/Mac export remain pending: computer-use reports the Mac locked. The user was asked to unlock; no confirmation arrived. No queued test is left to unexpectedly enter Play Mode later. See `docs/LIVING_WORLD.md` for accurate scope, setup and verification limits. No commit or push performed.

## Resumed native verification — 2026-11-13

The user unlocked the Mac. Native execution exposed the Editor-only MonoBehaviour runner placement; the runner now lives outside Editor under UNITY_EDITOR and reports errors through an Editor callback. The isolated integration check passed in actual Unity. Extended the check to traverse ForestSlice → LivingWorldDistrict → Airport → ForestSlice with six synthetic assessed turns and retained outcomes. Native UI inspection exposed ordinary HUD overlap; an assertion failed before the fix (`red-hud-overlap.json`), then passed after modal ownership hides/restores existing HUD roots and suspends LifeUI. Empty correction labels are hidden.

Actual native JSON save/reload, correction gate, natural paraphrase progression, completion rehearsal guard, active-scene player rebinding, HUD/input restoration and cross-scene traversal now pass. Original authoring scene and saves are restored after checks. The remaining human microphone check is open in an isolated live-provider preview; user input is requested. Browser/Mac exports remain unbuilt in this task.
