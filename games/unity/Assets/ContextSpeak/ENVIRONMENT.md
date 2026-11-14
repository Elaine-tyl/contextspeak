# Realistic environment & lighting (Unity 6000.3, URP 17.3)

Menus (ContextSpeak > Environment): **Setup Realistic World** (lighting + ground, water, vegetation, living world in every life scene),
**Setup Realistic Lighting** (lighting/post/quality/controllers only), **Build Environment Benchmark**, **Render Lighting Test Shots**;
checks: ContextSpeak > Checks > Environment. In play mode: **F3** developer panel, F2 sky panel.

## Inspection (2026-11-13)
- URP 17.3, forward renderer, HDR, one URP asset (CafePipeline) for every quality level; SSAO only on the forest renderer.
- Pre-upgrade render tests showed: noon sand/pavement blown to white (post-exposure +0.35 EV with near-white albedos), sunrise/sunset
  tinting the whole world pink-orange (orange fog + warm ambient), flat contrast (ambient ≈ half the sun), untextured flat-colour
  materials, a test-render bug that left rain in the "clear night" shot. Nothing is HDRP; the pipeline was not changed.

## What changed
| Area | Change |
|---|---|
| Quality | Env-Performance / Env-High / Env-Ultra URP assets mapped onto Unity's quality levels (Very Low–Medium → Performance, High/Very High → High, Ultra → Ultra). Performance: 0.9× scale, 2048 px shadows, 60 m, 2 cascades, soft-low, SMAA. High: 1.0×, 2048, 100 m, 4 cascades, soft-medium, MSAA 2×, SSAO. Ultra: 1.0×, 4096, 140 m, 4 cascades, soft-high, MSAA 4×, SSAO. One AA method per preset. |
| SSAO | URP Renderer Feature on a separate EnvRenderer (DepthNormals, intensity 0.6, radius 0.3) — not a Volume override; default renderer stays SSAO-free. |
| Exposure | Base exposure 0 EV (was +0.35); night lift 0.45 EV; EnvironmentManager adapts ±EV gradually between indoor LightingZones and outdoors. |
| Sun | Colour from blackbody temperature by elevation (~2000 K horizon → ~6000 K high); haze keeps the horizon's brightness but little of its sunset hue; less saturated golden-hour ambient; moonlight slightly cool, not blue. |
| Post | Shared global Volume per scene: ACES, bloom 0.12 (threshold 1.1), white balance, vignette 0.12, saturation −4. |
| Materials | Generated flat colours capped at 0.86 sRGB albedo; glass smoothness 0.92. Ground: world-projected PBR (base/normal/mask), macro variation, rain darkening, puddles + raindrop rings. Terrain layers get normal + mask maps and height blending. |
| Water | ContextSpeak/Water Surface for pools, rivers, streams, lakes, ponds, waterfalls (WaterSettings presets: absorption tints, flow, turbulence, contact foam, caustic approximation, rain rings, Fresnel reflections). Ocean unchanged (Gerstner) apart from wave-strength tuning. |
| Vegetation | ContextSpeak/Vegetation Lit: textured PBR, thin-leaf translucency, double-sided, wind = quadratic bend from the base + leaf-only flutter (by texture colour) + grass waves + travelling gusts, per-plant colour variation, wetness. Prefabs with 3 LODs; small plants don't cast shadows. |
| Wind / weather / time | GlobalWindController, WeatherManager (Clear, Partly cloudy, Overcast, Light rain, Heavy rain, Windy, Foggy morning, Clear night; smooth blends), DayNightCycle (night, blue hours, sunrise, morning, midday, afternoon, golden hour, sunset). All drive the existing SkySystem. |
| Living world | Birds (procedural flapping, fewer at night/rain), butterflies at flower clusters (fair weather), evening midges. Existing: street/indoor lights by time & weather, NPCs shelter at night/rain, outdoor café seats refused in rain. |
| Lights | Interior lights warmer (≈2700 K) and relatively brighter in the evening; light prefabs (warm pendant, neutral panel, table lamp, counter spot, garden light, shop window) in Data/Environment/Lights. |

## Assets
- `Assets/Art/nature/*.glb` — optimised from the user's Meshy library (`_Game/Art/MeshyLibrary`) with `tools/blender/nature.py`:
  scaled to real size, decimated to 3 LODs (MeshLab quadric collapse keeping UVs; when Meshy's fragmented UV atlas blocks it,
  decimated freely and the colour/roughness re-baked in Cycles), textures 1–2K. Kept: Towering_Pine, Tree_Spruce, Oak_Tree (re-baked),
  Radiant_Blossoms ×5, rock_flower ×4, chrysanthemum, jasmine, irises, crimson bromeliad, peach rose, allium, tropical flowers,
  Vallisneria, Golden_Meadow_Grass, tomato plant, mountain rocks, tundra rock, wide cliff, mushroom, bluebird.
  Rejected: Lavender_Lane (flat strip), alien underwater plant, bellflower (fantasy), calendula (broken), Lanterned tree (fantasy),
  the diorama pieces (beach/river/oasis/koi/pool tiles — water painted into the texture), Emerald_Grass_Patch/Daisy_Meadow (hundreds of
  thousands of separate blades — replaced by generated tufts).
- Generated grass/wildflower tufts (`tools/blender/grass.py`): real curved blades, 8 species, 3 LODs, ~30–700 triangles.
- Generated terracotta pots/jug (`tools/blender/props.py`): 418–444 triangles, one distance-culled LOD; these small props do not need duplicated lower-detail meshes.
- Generated ground textures (`tools/blender/texgen.py`, tileable, plausible albedo). **Drop CC0 photo-scanned sets into
  `Assets/Art/Textures/<Name>/`** (Grass, DrySoil, WetSoil, Mud, Gravel, Stone, Moss, BeachSand, WetBeachSand, Asphalt, Concrete,
  ForestDebris; files named …Color/…Normal(GL)/…Roughness/…AO/…Height) and re-run Setup — they replace the procedural ones.
  Optional HDRI skies: `Assets/Art/HDRI/*day*.hdr`, `*sunset*`, `*night*`.

## Limitations (honest)
- No true dynamic GI: indirect light is the sky's ambient (SH) + realtime reflection probes + SSAO. No baked lightmaps (day/night would make them wrong) and no Adaptive Probe Volumes; light probes are not baked, so characters use the scene's ambient.
- Caustics are an approximation drawn on what is seen through shallow water, not light transport. No volumetric fog/clouds (URP).
- Cloud shadows use the existing cookie textures; no per-cloud shadow casting.
- Meshy vegetation is solid-mesh foliage (no alpha leaf cards); translucency is approximated.
- No Home, School or Shopping Mall scene exists yet, so their interior lighting isn't set up.
- Frame time / draw calls are shown live in the F3 panel; the 60 FPS High target has **not** been profiled yet.
