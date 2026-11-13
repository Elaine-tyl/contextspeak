# Complete FBX import and NPC variety implementation plan

> Use superpowers:executing-plans inline, test-driven-development and verification-before-completion.

**Goal:** All current Downloads FBXs are inspectable Unity assets; every distinct human appears as a protagonist, named NPC or town resident without excessive duplicates.
**Architecture:** Existing safe source catalog → explicit inspected character roster → optimized runtime assets → stable actor cast → actual scene checks.
**Spec:** ../specs/2026-11-13-complete-fbx-cast-design.md

## Tasks
- [x] Add RED coverage for unbranded FBX/ZIP discovery, repeatability and stable paths. Extend source catalog importer and import all current sources; verify native Unity FBX/material imports.
- [x] Inspect character previews, register all distinct human identities and appropriate heights/rig requirements. Convert the scene copies with bounded geometry/textures; preserve existing protagonists and source animation variants.
- [x] Add RED cast completeness and per-scene duplicate checks. Replace repetitive named bodies and place unused identities as reachable residents with existing speech tasks. Keep deterministic identities and mission IDs.
- [x] Run asset, Unity scene/navigation/native/runtime and browser regressions, build Web/macOS and inspect actual cast. Document import counts and model→actor assignments; leave unrelated workspace changes intact.

## Review focus
ZIP traversal, duplicate names and differing content; renamed unbranded assets; missing channels versus embedded textures; protagonist animation variants accidentally used as NPCs; mobile models without usable rigs; unreachable added actors; stable quest state after visual changes.

## Execution evidence

- Discovery RED → GREEN: unbranded `.FBX` and `.ZIP` assets; 189 native FBXs imported, zero import errors.
- Cast RED → GREEN: original duplicate body rejected; rebuilt scenes verify 2 / 4 / 32 unique NPC bodies.
- Resident lesson RED → GREEN: namespaced IDs use the contextual topic, retain independent steps.
- Ruling: bake the two older supplied walking poses into game copies and generate basic metre-scale rigs — their centimetre bind transforms fail native posed-geometry checks; full FBX clips remain in the source library. Costs: runtime uses basic motion, not retargeted original walking clips.
- Python asset/library/cast suite: 18 passed. Unity geometry, profiles, scene navigation, native UI and counter interaction checks passed. Web/macOS builds and browser recording/save regressions subsequently passed.

- Final review identified embedded FBX texture loss in native previews; regression reproduced, extraction/per-slot remapping fixed. Native FBX and prefab checks now pass for all 189 sources. Repeat external-map imports avoid unnecessary FBX reimports.
- Packaged browser: 26 passed, including resident-specific recording/feedback/independent saved progress. Local prepared geometry contact sheet visually inspected; static and posed models documented honestly. Mac build succeeded.

- Web and macOS builds succeeded. Preview Scene view framing now fits the expanded multi-row roster instead of the old eight-character row. Final Editor compile/import check passed.
