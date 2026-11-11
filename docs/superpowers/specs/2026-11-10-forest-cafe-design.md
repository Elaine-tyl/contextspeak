# Forest café art direction

The user supplied `森林咖啡馆游戏场景概念板.png` after reviewing the cozy café.
Apply its forest-conservatory mood to the existing playable Godot game. This
continues the authorized visual upgrade; task logic, controls, progress and NPC
interactions remain the authority over text embedded in reference images.

## Composition and constraints

- Retain the 15×12m café floor, original counter/friend anchors and existing
  collision boundaries. Keep the two tested walking routes clear.
- Make the back/window architecture a timber conservatory: tall framed glazing,
  restrained rear roof beams, warm lanterns and timber counter detail. Cut away
  camera-facing roof/walls where needed, keeping full original boundary collision.
- Place a hero tree near `(-5.8,0,-3.6)`, away from the task paths. Its high canopy
  and a few trailing vines frame the room. A small trunk/planter collider may be
  added there; it must not block the entrance/counter/friend route.
- Build the visible outside garden as real 3D forest/landscape with no new
  accessible area or tasks. Existing NPC faces must remain clear in every camera.
- Reuse existing CC0 assets and original cached geometry/materials. No new
  download, paid asset or raster replacement for the playable world. The concept
  board itself is not bundled as scenery or a game asset.
- Use opaque grouped foliage, a small number of glass surfaces and one shadow
  light. Keep geometry/material/light costs bounded and measure the real renderer.
- Retain the tested responsive interface. Earthier outfits may adapt the current
  characters to the forest palette; their rig, scale and actions remain unchanged.
- Reframe exploration closer to the concept's within-room third-person view,
  with gentle positional following. Preserve counter/friend close-ups, movement
  timing, controls and pause. Check player framing and camera clearance at the
  room corners as well as at both task stops.

## Interfaces

`forest_garden.gd` extends Node3D with `build()` and
`advance(delta:float,reduced:bool)`. Its local coordinates are café coordinates;
it owns the hero tree and outside botanical decor. `world.gd` owns attachment,
architecture, existing furnishings and movement/cameras. The garden helper uses
the shared `Decor.finish/surface/box_mesh` resources, not gameplay state.

## Acceptance

Inspect real overview, counter and friend captures alongside the board. NPCs,
routes, replies, recordings, corrections, saved progress and all role scenes must
still work. Test reduced motion and geometry budgets, export Web/macOS, inspect
shader/page errors and native render/startup. Record measured limits honestly.
