# Character–water interaction (V6.1)

Unity 6 · URP 17 · forward. Code: `Assets/ContextSpeak/Runtime/Water`, editor tools: `Assets/ContextSpeak/Editor/WaterInteraction*.cs`, Blender assets: `tools/blender/water_test.py`.

## How it works
| Part | File | What it does |
|---|---|---|
| Water volumes | `WaterBody.cs` | Pool / pond / river / stream / lake / ocean. Surface height = top of the volume, or the ocean's Gerstner height (same maths as the ocean shader). |
| Profiles | `WaterInteractionProfile.cs` | ScriptableObject per water kind (speed 1.2 m/s, damping 0.985/step at 60 Hz, ripple radii, splash ranges, cooldowns, bubble lifetime, drag, wall reflection, current advection). Assets in `Data/Water/Profiles`. |
| Ripple simulation | `WaterRippleSimulation.cs`, `Shaders/WaterRippleSim.shader` | GPU damped wave equation on ping-pong RGHalf render textures, 24 m window following the player, 256/512/1024 by graphics preset, automatic sub-steps for stability (Courant < 0.45), absorbing window edges, reflecting pool walls/banks, advection by river current, rain drops and fish rises. Up to 64 disturbances per step. |
| Shader hook | `Shaders/WaterRipple.hlsl` | Read by `WaterSurface.shader` (pools, ponds, rivers) and `Ocean.shader`: ripple normals (distort reflections, sun glints and refraction), foam where the water is energetic, optional vertex displacement on dense meshes (`_RippleDisplace`). The ocean's base waves are untouched. |
| Contact | `WaterContactDetector.cs` | Foot bones (any rig: Mixamo, Meshy, generic names) or height fractions; immersion states Dry → Feet → Knee → Waist → Swimming → Underwater → ExitingWater with 8 cm hysteresis and a 0.2 s dwell. |
| Character | `CharacterWaterInteraction.cs` | Foot-contact ripples and splashes (velocity-aware, cooldown 0.15–0.35 s, none when still), drag (slower as the water deepens), jump impact from the measured entry speed (bigger rings, splash and bubbles for higher jumps), wakes, swim strokes, underwater bubbles, exit, wetness, sounds. Animation events: `OnLeftFootWater`, `OnRightFootWater`, `OnSwimStroke`, `OnDiveEntry`, `OnWaterExit`. |
| Wakes | `WaterWakeController.cs` | Bow wave + trough every 50 ms (becomes a V wake), stroke disturbances while swimming, gentle rings while floating / standing waist-deep. `WaterRippleEmitter` does the same for boats, fish, floating objects, NPCs. |
| Effects | `WaterSplashController.cs` | Pooled particle systems: spray droplets (gravity), mist, surface foam, bubbles, drips; 24 pooled wet footprints. |
| Wetness | `WaterWetnessController.cs` | Darker colours + satin sheen (capped, never a mirror), by immersion level; dries over ~1 minute (faster in sun, not in rain); drips and wet footprints. |
| Sound | `WaterAudio.cs` | Synthesised slosh / splash / plunge / stroke / drip / exit and an underwater bed (noise + Minnaert bubble chirps). Assign recordings on `CharacterWaterInteraction` to replace them. Underwater muffling via a low-pass on the listener. |
| Underwater | `UnderwaterInland` (in `CharacterWaterInteraction.cs`) | Pools/ponds/rivers: tint + absorption fog only below the actual surface, muffled sound. The ocean keeps its own `UnderwaterView`. |
| Swimming | `Environment/PondSwimming.cs` | Swims only where the water is ≥ 1.15 m deep (wade in shallower water), C dive, Space up, stand up in the shallow end, climb out at the bank. |
| Drag hook | `CafePlayer.SpeedScale` | One field + one multiply (added by `tools/water_patch.py`). |

## Menus
* **ContextSpeak > Water > Setup Character Water Interaction** — run in any playable scene (forest café, beach, test scene). Adds bodies, profiles, systems, the player component, emitters and swimming. Non-destructive.
* **ContextSpeak > Water > Build Water Interaction Test Scene** — `Scenes/WaterInteractionTestScene.unity`: pool (shallow + deep end, steps, ladder), forest pond with a drifting boat, river with a current, ocean strip, floating ball.
* **ContextSpeak > Water > Character Interaction Debugger** — live values and sliders (Play mode). In game: **F4**.
* **ContextSpeak > Checks > Water Interaction** — automated EditMode checks (see below).

## Tests
Automated (EditMode, `WaterInteractionChecks.cs`): immersion levels, no flicker at the waterline, exit sequence, no ripples from a still foot, walking contact + cooldown, running > walking, jump > step, cooldown range, profile differences, timestep-correct damping, stability at 256/512/1024, a CPU reference run of the shader's wave equation (ripple expands then dissipates; two sources superpose exactly; walls reflect, open edges absorb).

**Not yet run / needs a Play-mode session with screenshots:** reflections visibly reacting to ripples, shoreline continuity, wetness look on both characters, splash look at each water type, ocean ripples on moving waves, frame time on the target Mac. Record results in DEVELOPMENT_PROGRESS below.

## Known limits
* Wetness applies to whole materials (Meshy characters are one mesh), scaled by how deep you went — no per-height wet line.
* No full fluid simulation (by design): splashes are particles, the surface is a height field.
* The ripple window is 24 m around the player; ripples far away (other NPCs) are not simulated.

## DEVELOPMENT_PROGRESS
| Item | Status |
|---|---|
| Runtime components, shaders, profiles, editor tools, test scene builder | implemented, syntax-checked |
| EditMode checks | implemented — run via the menu |
| Compiled in Unity / Play-mode verification | pending |
| Blender pool / pond / river assets | script ready; box fallbacks used until exported |
