# Speaking Exploration Trail Implementation Plan

> For agentic workers: use superpowers:executing-plans inline, following the user's existing delegation of implementation choices.

**Goal:** Add a reproducible Blender-to-Unity expedition lodge, a need/vocabulary-directed NPC speaking trail, and the user-requested player/café/pond visual benchmark to the existing game.
**Architecture:** Pure C# route selection uses authored scene metadata and same-band saved lessons. Existing local voice review and transaction authority remain unchanged. Blender exports one original GLB loaded by the installed glTF importer.
**Tech stack:** Unity 6000.3.28f1, C#, UI Toolkit, Blender 5.3/Python, local AI companion.
**Spec:** docs/superpowers/specs/2026-11-12-speaking-trail-design.md

## Constraints and review focus

- Keep numeric bands3–9, old save data, cafés and 2D games intact; no reset or new cloud dependency.
- Unknown or hostile focus text cannot select invalid scenes or become executable/prompt instructions.
- Three distinct stops; only completed lessons at the selected band count. Null old-save fields are safe.
- A new plan/travel action cannot bypass an in-flight recording or required correction.
- The lodge and arrival must remain walkable; original material/asset attribution is explicit.

## Task 1: route generation and persistence

- [x] Add failing Core checks: concrete beginner defaults, expert defaults, ticket vocabulary routes to park, distinct valid destinations, same-band completion, null/bounded focus sanitization.
- [x] Implement `SpeakingTrail.Create(SliceSave)` and `SpeakingTrail.Focus(string)`, plus saved `FocusVocabulary`.
- [x] Run all pure Core checks.

## Task 2: in-game route and board

- [x] Add runtime checks for busy plan/travel protection; UI checks for visible route controls.
- [x] Add route summary/focus input to Map, next-step recommendation to completed lesson, map target marker, guarded plan action.
- [x] Wire physical board E interaction to map, preserving existing object actions.

## Task 3: original editable Blender art

- [x] Add Blender-side export checks: lodge collection, metre scale, mission board, material slots, mesh budget; run RED before model.
- [x] Write concise `tools/blender/speaking_lodge.py`, save `art/blender/SpeakingLodge.blend`, export GLB into Unity Art, render actual model preview.
- [x] Integrate at a free café entrance location with simple explicit colliders; verify scene navigation.

## Task 4: verification and delivery

- [x] Run source suite, pure Core, Unity scene/UI/runtime checks and Blender export check; build WebGL/Mac.
- [x] Exercise route planning, target-specific saved completion and existing recording/payment paths in browser.
- [x] Inspect actual render, report reproducible commands and honest limits. No automatic commit/push of the dirty baseline.

## Visual follow-up: reference-led benchmark

- [x] Add checks that reject block proxies for the player/barista, malformed skin/texture exports and broken imported bind rotations.
- [x] Rebuild editable player/barista sources offline from pinned CC0 graphical data plus original clothing; validate and integrate versioned GLBs.
- [x] Configure URP soft cascades/contact occlusion/HDR/SMAA/sky; replace flat pond material with depth-faded animated water. Verify real shader compilation and scene navigation.
- [ ] Exercise the actual browser build and Mac window. Keep supporting character, cloth, facial animation, reflection and performance limits explicit.

Browser visual/gameplay checks are complete. The Mac build and archive pass; its real-window inspection is pending because the Mac is locked. Detailed verification and remaining art limits are recorded in `.superpowers/sdd/2026-11-12-speaking-trail/progress.md`.
