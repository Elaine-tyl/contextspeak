# Cozy Café Polish Implementation Plan

> **For agentic workers:** Use scoped parallel implementation and one integration/review pass. Preserve gameplay; root owns exports and browser verification.

**Goal:** A cohesive cozy café presentation with the current game fully playable.

**Architecture:** Refine the Godot visual layer, existing CC0 assets and shared
avatar/UI modules. Keep lesson/state/provider code unchanged. Rendering stays
GL Compatibility for Web and macOS.

**Spec:** `docs/superpowers/specs/2026-11-10-cozy-cafe-polish-design.md`

## Constraints and review focus

No rule, control, NPC-count or save changes. No new dependency or downloaded art.
Keep the counter/friend walking routes collision-safe. Pause movement and cancel
work as before. Reduced motion must suppress decorative animation. Verify focus,
disabled controls, long feedback and narrow layouts. Check shader compilation in
the browser; headless geometry tests do not establish visual correctness/FPS.

## Tasks

- [x] Capture baseline overview, counter and seated dialogue; save and inspect
  screenshots, original gameplay-content hashes and rendering budgets.
- [x] Environment/movement: refine `world.gd`, `cafe_decor.gd` and small visual
  helpers; keep public API/destinations. Add meaningful route/pause/reduced-motion
  and resource-budget regressions; run Godot checks before integration.
- [x] Characters: refine `campus_style.gd` and `avatar.gd` while preserving
  rig scale and animation API. Extend avatar checks for standing/seated geometry
  and bounded shared render resources.
- [x] Interface: refine `interface.gd`/scene-menu styling. Preserve names,
  actions, focus order and layout contracts. Test readable focus/disabled states,
  dialogue bounds and responsive controls.
- [x] Integrate shared art direction into role scenes without changing their
  routes, identities or effects. Run typecheck/format, Godot and relevant browser
  regressions; review source independently and fix material findings.
- [x] Export Web/macOS, inspect matching before/after views and reference images,
  check console/performance, verify native startup; save `games/cafe/design-qa.md`
  and concise playtest notes. Leave the local game running for inspection.
