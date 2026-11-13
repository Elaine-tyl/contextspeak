# Meshy FBX Library Implementation Plan

> Execute inline in the existing worktree; the user authorized importing every Meshy FBX from Downloads. Preserve prior work and original downloads. No commit/push requested.

**Goal:** Import every unique Meshy FBX in Downloads, including ZIP-contained FBXs, into the current Unity project with textures and usable prefabs.

**Architecture:** A local Python importer safely discovers/copies FBX and accompanying textures, deduplicates by FBX hash, and records provenance. A targeted Unity postprocessor configures texture import; an Editor command generates URP materials, remaps native FBX material slots, creates prefabs and provides a searchable library/one-model preview. Existing gameplay assets remain unchanged.

**Tech Stack:** Python 3, existing Pillow, Unity 6000.3.28f1, existing URP.

## Constraints and review focus
- Preserve native FBX geometry, original animation clips and downloads; no invented dimensions/rigs.
- Validate ZIP paths before extraction; support loose FBX, Chinese archive names and optional texture channels.
- Deduplicate exact models, retain source aliases and support idempotent reruns.
- Large source files stay in a local Unity asset folder excluded from Git; only referenced assets enter builds.
- Do not instantiate the entire high-poly library in gameplay; preview one selection at a time.

## Tasks
- [x] Add discovery/extraction regressions (ZIP + loose duplicates, optional textures, traversal, idempotency) and observe RED; implement `tools/meshy/library.py`; verify GREEN.
- [x] Add Unity verification for native models/materials/prefabs/retained animations; implement `MeshyLibrary.cs`, texture postprocessor and Editor library browser.
- [x] Import all Downloads sources, run Python and Unity asset checks, inspect actual library/preview in Unity; record counts and failures.
- [x] Document use and native import performance/rig limitations; leave gameplay and prior changes intact.

## Execution record
- Downloads inventory: 85 Meshy-named FBX ZIPs, plus Chinese-named table/chair ZIPs and loose FBX files. ~6.4 GB before the extra props. Existing five optimized GLBs remain separate and unchanged.

- Source preparation completed: 99 unique native FBXs, 100 source aliases, 473 PNG textures/maps. All 99 copied FBXs match their original SHA-256. One loose generated table has no external base texture. Three supplied animated variants retain walking/running/vault source clips.
- Five importer regressions pass, including optional maps, unsafe ZIP rejection, map packing, reruns, and canonical-path stability when a later duplicate sorts first. Full project Vitest suite: 84 files / 609 tests passed.
- Review found packed smoothness incorrectly multiplied by 0.5; actual serialized material assertion reproduced RED. Fix/Unity regression pending completion of the first import. Review also reproduced duplicate-alias canonical-path instability; RED-to-GREEN fix now reuses prior manifest paths by SHA.

- First complete Unity import passed: 99 native FBXs, 99 prefabs, zero failures; all 3 supplied animated variants retain clips. Native sources total 107,553,184 triangles, so the browser previews one model at a time and does not instantiate this library in gameplay.
- Smoothness fix sets multiplier to 1 for packed maps; verification now asserts this on every native imported material. Final repair/reverification running. Combined Python regression suite: 17 passed.

- Final Unity import/reverification passed after repair: 99 FBXs / 99 prefabs / 3 supplied animated variants; zero failures. All 84 packed-surface materials preserve unit smoothness. No runtime gameplay code changed. Visual Editor check remains.

- Actual Editor acceptance: searchable library shows 99 models; original undersized window clipped controls, fixed with minimum width and verified by closing/reopening. Forest Haven café and native Elliot previews render with textures. Preview initially produced IMGUI EndLayoutGroup errors on scene replacement; deferred replacement until after the GUI event, then repeated preview switching and checked the new Editor log: no new layout/compile errors. Proof: `docs/images/unity-meshy-fbx-library.png`.
