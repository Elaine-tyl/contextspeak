# Café free art implementation plan

**Goal:** Integrate free Quaternius people and KayKit restaurant art without
breaking the voiced café journey; repair repeated weekend questions.
**Architecture:** Existing Godot scene + local companion. Vendor selected licensed
assets with hashes, normalize geometry, adapt the small Avatar animation wrapper.
**Spec:** ../specs/2026-11-10-cafe-free-art-design.md

- [x] Reproduce screenshot text with rejecting model output, add regression, fix
  inflected activity recognition and explicit repair feedback. Verify state advance.
- [x] Download free creator archives, inspect license and formats, select clothed
  animated characters and a small café prop set; record source/archive/file hashes.
- [x] Add missing import/animation/placement regression checks; integrate assets
  and adjust characters, seating, café props and camera to the rendered result.
- [x] Run Godot tests, Web/macOS exports, browser ordering→seating→friend chat,
  synthetic local microphone, types/regressions and native rendered/startup checks.
- [x] Record screenshots, updated provenance, reproduction commands and limits.

Review focus: free vs paid edition; missing textures; incompatible idle/walk/sit;
old saves; movement/collider changes; voice/record callbacks during scene transitions.

Ruling: Proceed within the user's explicit implementation request and prior chosen
Godot/local-AI architecture. Use inline art integration and an independent dialogue
bug task, whose files do not overlap. Preserve all pre-existing uncommitted work.

Verification: 294 Vitest tests / 64 files; types/Prettier; Python integrity regression;
Godot mission/UI/friend/placement/avatar checks; 4 browser gameplay regressions;
real local models with five synthetic microphone recordings and the exact Batman
answer; native UI recording/silence/typed order/seating/chat to 3/3; Web/macOS builds.
Evidence: docs/CAFE_FREE_ART_PLAYTEST.md. No commit or push in this task.

Ruling: Reuse three clothed variants of the seated-capable Animated Woman rig,
instead of adding paid base-character content or a new retargeting dependency.
The tradeoff is less avatar-body variety in this first art upgrade.
