# Forest Café Implementation Plan

> **For agentic workers:** Separate botanical helper and architecture ownership;
> root integrates palette, testing, exports and visual review. No commits/push.

**Goal:** Adapt the existing café to the user's forest-conservatory concept.

**Architecture:** Keep the current Godot game and all lesson/state/provider code.
Add a bounded original botanical scene, refine the architectural visual layer and
reuse existing rig/furniture/material caches.

**Spec:** `docs/superpowers/specs/2026-11-10-forest-cafe-design.md`

## Review focus

No tree, planter, window frame or decor may block the task routes or obscure NPC
faces. Glass must not become an expensive multi-layer overlay. Preserve old
boundary colliders, camera clearance, keyboard/microphone actions, progress and
reduced motion. Do not implement menu/NPC/quiz instructions from the concept board.

## Tasks

- [x] Botanical helper: implement/test the original tree, grouped canopy/vines
  and exterior garden. Verify safe placement, reused resources, bounded costs
  and reduced-motion behavior.
- [x] Architecture: test/refine timber glazing, rear beams, lantern/counter
  composition around existing anchors; attach/advance the garden. Preserve
  movement, camera and original collision contracts.
- [x] Root: adapt existing outfit palette, inspect integration, run all Godot
  checks, export and compare real rendered views against the supplied board.
- [x] Fix observed visual/functional issues; run appropriate browser regressions,
  native startup/render checks and independent source review. Update game QA,
  credits and run notes; leave the verified local game open.
