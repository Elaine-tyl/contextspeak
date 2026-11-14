# Luxury Villa — Visual Quality Report (V9)

Status: **code and art deployed; the scene has to be rebuilt and captured in Unity before any result
counts.** Nothing below is a measured Unity result unless it says so. Blender previews are made with Eevee
and a different sky, so their exposure and colour do not match Unity.

## 1. Inspection (before the upgrade)

* **Pipeline:** Unity 6000.3.28f1, URP 17, forward.
* **Quality presets:** the project already has Performance / High / Ultra (`Assets/Generated/Env-*.asset`), and High/Ultra use `EnvRenderer` with SSAO.
* **The villa scene did not use that stack.** It had:
  * LifeBuild daylight: a procedural skybox and one soft-shadow sun
  * a basic ACES volume
  * realtime point lights
  * 3 realtime reflection probes that refresh once
  * light probes that were never baked
  * no SSAO, HDRI or weather

Defects, highest priority first:

1. **The front door and the garage could not be entered.** A 0.6 m stone base course ran across both openings. This also cut the NavMesh: the build report said no room was reachable.
2. **The player walked backwards in every scene.** `CozyCharacters.DressPlayer` kept the spawn yaw on the visual (see §8).
3. The **lighting was flat and washed out** (user screenshot, 17:30 start): there was no SSAO and no HDRI, and the ambient light was uniform.
4. **Glass** was a glTF alpha-blend material: no preserved specular, so no Fresnel reflections, and grey.
5. **Furniture** was plain boxes: sofa, loungers, counters, fridge.
6. **Vegetation** was spheres on sticks; the hedges were spheres.
7. The **surroundings were a void** beyond the boundary walls, apart from a grey street box.
8. **Architecture** had no skirting, sills, frames, thresholds, flashing, wall lights or downpipes.

## 2. What changed

### Architecture (Blender, `tools/blender/luxury_villa.py`)

* **Stone plinth:** now travertine, split around the garage door and the entrance (fixes defect 1).
* **New details:**
  * a natural-stone accent wall by the entrance
  * skirting boards on every interior wall face (generated per wall piece, stopping at door openings)
  * stone window sills on all clear windows
  * door frames and thresholds, plus a light switch plate beside each door
* **Exterior:**
  * aluminium flashing on the parapets
  * 4 downpipes
  * 6 exterior wall lights
  * an LED line under the balcony soffit
  * a gravel drip strip along the foundation

### Materials (procedural PBR, `ArtSource/LuxuryVilla/Textures`)

* **New sets:** travertine, asphalt, gravel, linen, cushion fabric, sheer curtain, clipped hedge, rock.
* **Alpha-cutout cards:** leaf twig, large tropical leaf, flowers (glTF MASK, double-sided).
* **Glass:** in Unity it is replaced by `Art/Villa/Materials/Villa_ArchitecturalGlass.mat`:
  * URP Lit, transparent
  * preserve-specular, so reflections stay visible at grazing angles
  * smoothness 0.96, faint green-blue tint
* **Stair tower:** `Villa_GoldGlass.mat`.
* **Not done:** per-material ambient-occlusion maps. Contact AO comes from SSAO instead.

### Furniture

* **Sofas:** an upholstered frame and separate rounded seat and back cushions, throw pillows, legs.
* **Living room:** 2 accent armchairs, a travertine coffee table with books and a bowl, 2 side tables with lamps, pleated sheer curtains on a track at the glass wall.
* **Kitchen:**
  * base and wall cabinets with door gaps and bar handles, a stone worktop, a marble backsplash and an under-cabinet LED strip
  * a tall unit with an oven and microwave
  * an induction cooktop with an extractor hood, and an undermount sink with a faucet
  * a French-door fridge
  * a waterfall island and upholstered bar stools
* **Loungers:** teak frames with cushions and head pillows.
* **Kept separate:** every interactive item is still its own object, so seats, fixtures and anchors are unchanged.

### Vegetation

* **Trees:** in Unity they are replaced by the project's realistic tree prefab (`EnvironmentSetup` vegetation, with wind shader). Where none exists, the Blender trees (branches plus leaf-twig cards) are kept.
* **Flower prefabs** fill the beds.
* **Bird-of-paradise style paddle-leaf clumps** are planted along the pool garden, the entrance and indoors.
* **Shrubs** with flowers.
* **Hedges:** 2.35 m clipped hedge walls round the pool garden and a front hedge.
* **Rocks** in the beds.
* **Wind:** `VillaWind` sways the Blender plants from `SkyState` wind.

### Surroundings (`LuxuryVilla_Surroundings.glb`)

* **Street:** a 10 m asphalt road with lane markings, kerbs and pavements on both sides, 22 street lights (on at night) and street trees.
* **Neighbours:** 7 neighbouring houses (2–3 storeys) with window bands and opposite hedges.
* **Distance:** a distant tree ring and ground to 90 m.
* **Colliders:** the road and pavements have colliders; trees have trunk capsules only.

### Lighting and post

* **Shared stack:** the builder calls `EnvironmentSetup.ConfigureCurrentScene(false)`, the same stack as the other locations. It adds:
  * the HDRI sky
  * the SSAO renderer (High/Ultra)
  * the ACES volume
  * day/night, weather and wind
  * quality presets (F3 environment panel)
* **Start time:** 10:30, so the sun comes through the glass façade and casts window shadows on the living-room floor.
* **Villa gameplay grade:** bloom 0.22, white balance +3, contrast +8. No DOF or blur in play.
* **Cinematic profile:** saved separately and off by default.
* **Interior lights:**
  * follow daylight, room occupancy and a per-room switch (E: auto → on → off, saved)
  * garden, pool, street and wall lights come on at night
  * realtime reflection probes re-render every half hour of game time
* **GI:** no GI is baked. Light probes are placed, but indirect light comes from the HDRI ambient and the realtime probes. Baked GI or Adaptive Probe Volumes with day/night lighting scenarios is a next step (§7). We do not claim realtime GI.

## 3. Validation tools

* `ContextSpeak > Villa > Capture Visual Validation Shots` renders 7 fixed views through the scene's real URP renderer, volumes and sky into `Logs/VillaShots/<time>/`:
  1. front
  2. living room
  3. kitchen and dining
  4. garden and pool
  5. upper landing
  6. night exterior
  7. pool deck (the user's screenshot)
  * Run it before and after any change: same cameras, same hours.
* `Checks > Luxury Villa` (static) and `Checks > Luxury Villa Play Mode Tests`: doors, NavMesh reachability (including the stairs to floor 3), lift ride, umbrella, seats, pool and family.

## 4. Results

*To be filled from the captured shots and the Play-mode report after the rebuild.*

## 5. Performance

* Not profiled yet, and no FPS target is claimed.
* The F3 environment panel switches Performance / High / Ultra: shadow distance 60/100/140 m, MSAA, SSAO on High/Ultra, grass density, LOD bias.
* Measure with F7 (crowd and performance overlay) and the Profiler.

## 6. Known limits

* **Characters:** no special eye or hair shaders were added. Characters get the HDRI ambient, SSAO and sun shadows, and no light-probe GI until a bake.
* **Neighbouring houses:** simple massing. They are not hero assets.
* **Water:** the pool uses the existing water system unchanged (ripples, swimming).

## 7. Next steps

1. Rebuild, then capture the shots, then iterate on the real Unity images.
2. Bake GI: an APV day and night scenario, plus mixed lighting for the interior.
3. Per-material AO maps and texture LODs.
4. Rigged animations from Blender: lying in bed, cooking, typing, cleaning, a cat walk cycle.
