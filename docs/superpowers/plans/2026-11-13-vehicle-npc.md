# Downloaded Vehicle and NPC Application Plan

> Execute inline with superpowers:executing-plans; no automatic commit/push.

**Goal:** Apply supplied car and NPC models to live Unity city interactions.
**Architecture:** Reuse imported prefabs, update only visual children in the saved city, and keep deterministic builder assignments aligned.
**Tech Stack:** Unity 6.3, C#, existing Meshy FBX/GLB import pipeline.
**Spec:** ../specs/2026-11-13-vehicle-npc-design.md

## Constraints and review focus
- Preserve existing gameplay roots, NPC IDs, animator references, colliders and driver settings.
- Applying twice must not duplicate models, actors or gameplay components.
- New car skins must sit on the road, use plausible dimensions and retain usable collision/drive controls.
- CafeCharm and Elliot must keep their supplied animation clips; protagonists stay distinct.
- Save only intentional scene changes; retain uploaded greenhouse and city lighting.

## Task 1: Apply existing imported assets
Files: create Assets/Editor/DownloadedCastUpgrade.cs and DownloadedCastChecks.cs; modify CityScene.cs assignments only.
- [x] Add a native baseline check asserting four parked vehicle identities and new city NPC skins; run and observe the missing-assignment failure.
- [x] Implement a scene-local visual upgrade and align city builder car/NPC choices.
- [x] Apply in the real Unity Editor, verify root references and repeated-apply idempotence, save the scene.
- [x] Inspect fresh car/NPC renders and verify animation roles, scale, environment and gameplay regression results.
- [x] Complete one fresh read-only review and record the actual result. No full rebuild or player export.

## Verification so far
- Key downloaded animated character archives and Hummer match the existing imported SHA-256 identities; no duplicate raw sources copied.
- Native Unity RED observed for missing car placement; final GREEN verifies four distinct car skins, road clearance, grounded scale, acceleration/stop, solid-obstacle stopping, three NPC appearances and supplied clip availability.
- Applying twice preserves actor/driver roots, mission IDs and conversation Animator references without duplicating renderers.
- Independent scoped review found the edit-mode Awake validation gap; a native cube-barrier regression reproduced it, and the editor test now initializes the existing driver lifecycle. Horizontal collision is tested separately from vertical road-following.
- 10 library and 4 character-asset Python regressions passed.
- Fresh native city renders generated for daylight, sunset, night and rain. Final parking overview confirms the four existing bays are on the mall east side, outside roads/pavements.

## Final result
- Saved MetroCity now uses Packard, 1930s sedan, 1957 Chevy and Hummer in its four borrowable parking spaces; existing traffic retains its other downloaded vehicle models.
- City barista Maya uses CafeCharm, clothing adviser Ellie uses BraidedSchoolgirl, and Alex retains the full animated Elliot source. Protagonist choices and other NPC identities were not changed.
- A native regression exposed removed headlight children during a visual swap. Replacement now targets only imported scenery-mesh branches; existing light/accessory children survive. Missing lights are repaired with the existing SkyInstaller spotlight settings. Native headlight checks and repeated-apply light-count checks pass; final night renders visibly show four working beams.
- Final saved-scene assignment, movement/obstacle, animation availability and environment checks passed in the real Unity Editor. Preview rendering produced 36 fresh images and restored the saved scene afterward.
- Library and character-asset tests: 14 passed. No raw source duplicates imported, no runtime gameplay logic changed, no commit/push or Web/Mac player export performed.
