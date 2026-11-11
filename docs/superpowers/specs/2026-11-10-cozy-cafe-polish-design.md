# Cozy café visual polish

## Intent and chosen direction

Upgrade the existing playable Godot game using the user's café references:
warm cream plaster, oak, sage and terracotta, rounded furniture and expressive
campus characters. Keep ContextSpeak branding and every existing interaction.
The user has delegated routine design choices and asked for implementation.

The supplied screenshots are art-direction references, not assets to copy. The
pasted Three.js/25-question brief is reference material; the direct request to
preserve the existing game takes priority. No engine migration or new quiz rules.

## Approach

Refine the current vendored CC0 furniture/rigs and original geometry. Compose
around the coffee counter, a clear entrance-to-counter route and the friend
table. Improve silhouettes, surface response and lighting before adding details.
Keep GL Compatibility for Web/macOS parity. One shadow-casting sunlight; limited
unshadowed warm fills. Avoid unsupported screen-space effects or cloud assets.

A replacement commercial environment would risk license and interaction drift;
a new renderer would abandon working desktop, speech and save behavior. Both
are outside this update.

## Visual decisions

- Human-scale counter and seating, open paths, framed windows, warm plaster and
  subtle wood grain. A few intentional props, not more furniture everywhere.
- Cohesive stylized head/body proportions, tidy layers, recognisable learner,
  friend and barista outfits on the existing idle/walk/sit rig.
- Warm neutral light without yellow clipping; readable shadows, material
  roughness and ambient fill. Reuse materials and geometry where practical.
- Smooth camera position/aim transitions and coherent animated silhouettes.
  Preserve movement timing, destinations, keys, collision boundaries, proximity
  and pause rules.
- Cream/coffee/sage dialogue and HUD with clear hierarchy, comfortable spacing,
  contrast, hover/focus/disabled states and unchanged controls/actions.
- Small bounded ambient animation that stops for reduced motion. No effects
  obscuring speech, faces, paths or text.

## Acceptance and boundaries

Keep all lesson content, evaluation, recording, NPC sequencing, progress keys,
stage previews and save/cancel rules. Verify original café, all role scenes,
keyboard navigation, recorded correction and reload. Compare before/after
captures in matching states; inspect WebGL shaders and console errors. Export
Web/macOS, check native startup and report measured performance limits honestly.
No downloads, dependencies, paid services, commit or push needed for this task.
