# All FBX gameplay and supplied animation implementation plan

> Use superpowers:executing-plans inline; TDD; one final fresh review.

**Goal:** All 189 source hashes have functional scene/character/motion use, with five supplied clips active rather than disabled.
**Architecture:** Source inventory → optimized static/animated exports → explicit usage/placement catalog → per-object Addressables streaming + native FBX motion player.
**Stack:** Blender 5.3 local Python, Unity 6000.3.28f1 C#, existing UI/room/voice code.
**Spec:** ../specs/2026-11-13-all-fbx-gameplay-design.md

## Review focus
Missing source aliases/variants; unsafe geometry scales and building footprints; texture loss; root-motion drift or bone-path mismatch; source versus generated clips; streamed handle cleanup/stale loads; new collision proxies blocking NPC paths; room/player animation regressions; unbounded build/GPU memory.

- [x] Add RED exhaustive coverage/budget tests. Classify all remaining FBXs and export bounded geometry/textures with reproducible source hashes.
- [x] Add RED actual motion checks. Prepare native animated FBXs, preserve five source clips, build safe in-place motion states/action preview. Replace fake/generated movement only where real clips exist.
- [x] Build static prefabs/Addressables and explicit placements, stream by distance, provide scenery navigation/inspection; keep paths and NPC interactions safe. Verify every hash has live use.
- [x] Verify geometry, clip deformation, resource cleanup, navigation, old/new browser flows, friend rooms, Web/macOS builds; final review and documentation. No commit/push requested.

## Final completed-download snapshot extension

The user downloaded 82 additional entries while the first build ran. Freeze this intake at 273 completed entries / 268 distinct hashes; later/in-progress downloads are a separate batch. Apply the same pipeline to eleven inspected new resident models (including a rider), two additional supplied walking clips, and the remaining scenery. Derive validation counts from the frozen manifest rather than hardcoding 189/154/5. Preserve original voices, rooms and saves. Reuse passed 31 browser gameplay regressions; rerun expanded native/hash/material/motion checks and the two asset/motion browser cases after updated Web/macOS builds.
