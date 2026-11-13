# Applied environment and locomotion corrections implementation plan

> Execute inline with superpowers:executing-plans; TDD; one final fresh reviewer. Existing worktree; preserve unrelated dirty changes.

**Goal:** Apply the 284-source snapshot as contextual scene art and fix female movement/NPC/piano scale.
**Architecture:** Existing hashed importer and bounded exports; semantic environment placements; native supplied clips with bone retargeting and state-based selection.
**Tech Stack:** Unity 6000.3.28f1 C#, Blender local Python, Playwright.
**Spec:** ../specs/2026-11-13-environment-animation-design.md

## Review Focus
- Seated composites distorted by standing-height normalization.
- Retargeted clips moving bones through incorrect rest offsets or losing materials.
- Repeated road/tree instances exceeding memory or obstructing existing mission routes.
- New category names falling through to generic exhibits with wrong scale.
- Existing saved player positions and settings surviving scene rebuilds.

- [x] Import fixed snapshot; add RED new semantic scale/category regressions; prepare all new optimized assets and explicit uses.
- [x] Add RED female clip/state and common-height tests; bind supplied walk/run through matching bone rotations and proportion-safe root motion; normalize character visuals.
- [x] Add RED live environment references/collisions checks; replace tree/ground/road visuals and selected furniture in scene composition. Preserve navmesh/streaming budgets.
- [x] Run all native/source/browser regressions, Web/macOS builds, final fresh review and fix significant findings; show final scene screenshot. No commit/push.
