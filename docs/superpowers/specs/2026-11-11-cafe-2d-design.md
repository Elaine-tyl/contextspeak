# Playable 2D café preview

The user asks to see a 2D version of the existing 3D café game. Build a cozy
top-down/three-quarter illustrated café in the existing Godot project, opened
with `?view=2d` in the local browser (desktop: `-- --2d`). Keep English UI.

Reuse the existing mission, friend conversation, roleplay, microphone and local
AI controllers. Add a 2D renderer/world adapter: hand-drawn native shapes,
rounded youth characters, wood furniture, plants, window light and cup steam.
Walking and collisions retain the existing logical coordinates. No imported
3D meshes are needed in this mode; invisible existing physics coordinates are
only the movement contract. Original vector art needs no external assets.

The original controller only changes how it constructs a world and selects a
save path. The first preview supports the café, Everyday and its US/Canada
challenge, plus existing practice-stage previews. Other 3D maps remain in 3D.
Preserve original default rendering and saves. Use separate 2D guest/scene save
keys and native files; retain existing bounded/atomic persistence behavior.

Approach chosen under standing routine-design delegation: native Godot 2D
drawing reuses controller behavior. A new Canvas/React game would duplicate
mission and recording logic; an orthographic 3D camera would not demonstrate
a distinct 2D art direction. This is a playable preview, not a new game engine.

Verify: renderer contains no imported 3D meshes; movement/reach/collisions and
seating work; rewards remain idempotent; café challenge/stage changes remain 2D;
2D saves cannot overwrite 3D; narrow framing contains the room. Run native
regressions, full source suite, Web/macOS exports and browser old/new journeys.
Show a screenshot and leave the 2D game open. No commit/push or new dependencies.
