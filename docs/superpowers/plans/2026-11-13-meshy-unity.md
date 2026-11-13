# Meshy Unity Character Import Plan

> Use superpowers:executing-plans in this existing worktree. The user has delegated remaining design choices; implement directly and preserve unrelated work.

**Goal:** Make the five supplied Meshy characters usable in the existing Unity game and easy to inspect in the Editor.

**Architecture:** A repeatable local Blender conversion reads the supplied archives, records input hashes, bounds and rig status, and exports optimized GLB assets. Unity creates URP-compatible character prefabs at metre scale and an Editor preview scene. Existing NPC gameplay components remain on their existing roots; replace visual children only when the model supports the required role. Keep the existing playable rig where a supplied static/T-pose model cannot safely replace it.

**Tech:** Installed Blender 5.3, pinned Unity 6000.3.28f1, existing glTFast/URP. No cloud upload, paid conversion or new runtime dependency.

## Constraints

- All five characters receive named Unity prefabs; the vault clip is not used as idle/walk.
- Keep original archives intact. Record user-supplied provenance without inventing a redistribution license.
- Reuse existing dialogue, voice, navigation and progress systems.
- Static FBX files must not be described as rigged/animated. Preview them honestly; no fake limb animation.
- Cap exported triangle/texture sizes to keep browser builds practical. Validate materials, orientation, foot placement and bounds in Unity.
- Café/props are inspected but the deliverable prioritizes the requested people.

## Tasks

- [x] Inspect all supplied archives for actual skeletons, animations, bounds and geometry complexity.
- [x] Write meaningful conversion/asset checks; observe failure before implementation.
- [x] Add repeatable local character conversion and input provenance manifest.
- [x] Generate Unity character prefabs and an easy-to-open preview scene; connect suitable visuals to NPCs.
- [x] Run Unity import/scene/gameplay checks and desktop/browser builds as appropriate.
- [x] Visually verify the real Unity models; document use, supported animation and limitations.

## Execution record

- Original archives inspected without modification. Academy: 10,152 triangles, 92 bones and one vault action. Four other character inputs: static meshes with 466k–1.07m triangles; reduced to fewer than 60k each. GLBs embed all used PBR channels at up to 2048px and each is below 20 MB.
- Safe archive extraction and real GLB provenance/geometry/rig checks observed RED before implementation, then six checks passed; combined Unity build-tool and Blender checks: 12 passed.
- Unity prefab regression observed missing prefabs; fixed through generated prefab builder and preview scene. Actual posed vertex bounds establish natural height; skinned-renderer AABBs include animation padding.
- Player visual: Academy; stationary home/park NPC: Elliot; stationary shopping NPC: Rose. Archeologist and Schoolgirl remain preview-only static source poses. Existing speech, dialogue, gameplay roots and save IDs are preserved.
- Read-only review found a clean-import material issue. A cold-import test reproduced wrong-pipeline materials. BuildAssets now establishes URP, explicitly replaces the pinned glTFast 6.20 material generator cache, force-reimports all five GLBs and validates every saved material's serialized UniversalPipeline shader tag. Cold-import regression now passes in the same Editor session.
- The explicit generator selection uses one checked private field in the pinned glTFast package because both generator and pipeline detection are statically cached; a package upgrade produces an actionable error rather than silently making pink models.

- Final Unity scene/rules/native-UI/interaction suite passed after the material fix. Browser and macOS builds succeeded.
- The Unity Hub project was opened in the pinned Editor, the preview menu invoked, and all five real textured models visually checked in Game view. Preview-camera lighting and labels were adjusted and checked again through the UI. Screenshot: `docs/images/unity-meshy-characters.png`.
- Full Unity browser regression run finished: 19 passed, one legacy navigation case timed out; all six forest-slice cases passed. The legacy `joining_a_friend_routes_around_the_bar_from_the_players_actual_position` case timed out waiting for its HTML interaction button and also timed out on an isolated rerun; this navigation-test failure remains unresolved and is not claimed as passing. Its cause is not established. Logs and trace: `.local/meshy/route-retry/`. No gameplay logic changed for this task.
