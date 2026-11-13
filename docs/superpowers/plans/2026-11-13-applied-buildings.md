# Applied buildings implementation plan

> Execute inline using superpowers:executing-plans and TDD; one fresh final reviewer. Existing delegated design and existing isolated worktree. No commit/push.

**Goal:** Real imported building exteriors with safe cutaway interiors and contextual use for all34 usable architecture files.
**Architecture:** Curated architecture.json → shared contextual GLBs → editor building placement → runtime cutaway/entrance interaction, preserving existing anchors and collisions.
**Stack:** Unity6000.3.28f1/C#, local Blender/Python, Playwright.
**Spec:** ../specs/2026-11-13-applied-buildings-design.md

## Review Focus
- Closed exterior meshes trapping actors or hiding the player while inside.
- Cutaway updates accidentally enabling previously hidden props or disabling gameplay objects.
- Interior replacement colliders obstructing NPC, wardrobe, table and doorway routes.
- Background architecture consuming excessive mesh/texture resources or floating at the terrain edge.
- Saved positions, map teleports and friend representations across building boundaries.

- [x] Add failing catalogue and real-scene skin/entrance/visibility/path checks. Curate all34 inspected assets with unique purposes and dimensions.
- [x] Prepare shared optimized copies, attach seven safe building skins/entrances and replace matching interior props; apply remaining models to streetscape.
- [x] Implement runtime cutaway and E entrance travel preserving NPC/save/voice behavior; cover inside/outside transitions and restored positions.
- [x] Verify native suite, all source tests, browser entrances/conversation/rooms and Web/macOS builds; fresh final review and significant fixes; capture exterior/interior proof.
