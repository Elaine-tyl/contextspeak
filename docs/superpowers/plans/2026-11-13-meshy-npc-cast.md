# Meshy NPC Cast and New Downloads

> Execute inline in the current worktree. The user requested replacing all Unity NPC visuals and importing remaining new FBXs, and previously delegated design decisions. Preserve prior uncommitted work. No commit/push requested.

**Goal:** Replace every NPC visual in ForestSlice, LifeTown and ForestCafe with supplied Meshy characters, and import all newly downloaded FBXs into the local native library.

**Architecture:** Keep the complete high-poly native library separate. Export a small, deterministic cast of optimized textured GLBs with floor/height normalization; reuse the existing supplied rigs for mobile actors. One role mapping replaces old procedural/mini/Blender NPC visuals while interaction roots, controllers, colliders, dialogue IDs and saves remain unchanged. Rebuild Web and desktop so the visible game contains the change.

**Scope:** NPC humans including waiter, fishing instructor, district conversation NPCs, chef and ambient customers. Preserve the current player visual. Animals/bridges are imported as assets, not silently added as new gameplay.

## Tasks
- [x] Inventory/import new native FBXs, retain existing canonical paths and verify source materials/animations.
- [x] Add optimized NPC cast asset checks and scene assertions; observe missing new cast RED.
- [x] Convert selected supplied characters, generate role prefabs, replace every NPC visual in all three worlds. Mobile actors use real supplied rigs; do not claim static sources are animated.
- [x] Verify geometry/scale/materials, navigation and scene/runtime interactions. Build Web and macOS.
- [x] Inspect actual new cast/game visually; run regression checks, document mappings and remaining limitations.

## Decisions
- Native assets remain local and ignored by Git; originals stay untouched.
- Reuse existing architecture and voice flows. No RAG, paid conversion service or new game engine.
- Character mapping is an implementation choice within the user's request; use human models for human NPCs, not the new animal downloads.

- Scan/preparation includes 125 unique native FBXs (new downloads continued during discovery); previous canonical source paths were retained. Native Unity material verification is running.
- Optimized cast exports now include Daydream (supplied male walking rig), Autumn (static adult female), Urban (static male). Actual GLB test caught a 4096px normal map despite resize metadata; packing resized pixels fixes exporter reuse of the original PNG. Asset/geometry/material tests now pass.
- Saved-scene NPC regression observed RED on old Maya. Role builder replaced old handmade geometry; original NPC root/controller/IDs remain. Mobile waiter/customers use Academy/Daydream supplied rigs, static sources stay static.

- Final bounded Downloads snapshot contains 169 unique FBXs / 171 source aliases. Importing caught one WebP file mislabeled as PNG; regression reproduced that mismatch, and local-library encoding normalization now preserves original downloads and repeatable import timestamps. Python checks: 18 passing.
- Native interaction teardown exposed a destroyed player reference during save. Added isolated Play Mode regression (observed RED) and a guarded position capture that keeps the last saved coordinates while preserving other progress.
- Source review found no important NPC mapping defects; corrected stale documentation and made kitchen chef presence mandatory in scene verification.

- Native import verification passed for all 169 FBXs, prefabs and URP materials, including all three supplied animated sources; import report contains zero errors. Teardown regression is GREEN; native UI and counter navigation/interaction checks pass without the previous destroyed-player exception.

- Final Web and macOS builds completed. Mac scene visually showed the new Urban NPC; original downloaded assets were not modified.
- Full software-rendered browser run: 16/20 passed; four cases timed out during loading/rendering or waiter delivery while builds were running. A windowed retry was interrupted by closed browser targets. After builds finished, a background Metal-accelerated run passed all seven selected cases, covering every initially failed scenario plus the full ForestSlice suite (1.8 min). Mac GPU flags are now in the normal Playwright config; the config still discovers all 20 tests. No gameplay navigation changes were needed.
- 609 project tests, 18 Python tool/asset tests, native NPC/geometry/navigation/UI/teardown checks passed. Git diff whitespace check is clean.
- Follow-up UI request: removed the full “Your first day” launcher panel and its unused health/state client code. Web TypeScript check and isolated live-page assertions passed; proof saved to docs/images/world-without-first-day.png.
