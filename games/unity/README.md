# ContextSpeak Unity

Unity 6000.3.28f1 LTS runs the Life world and original 3D cafe. The active web learning games use React/Three.js; the earlier engine-based arcade experiments are retained as historical source. Follow [engine setup and controls](../../docs/GAME-ENGINES.md).

Runtime code lives in `Assets/_Game`: Core, Player, NPC, AI, Data, World, UI and
Arcade. Editor builders generate scenes, capability/quest assets, navigation,
Addressables bundles and free-art imports. Art provenance remains under
`games/cafe/assets`; it is shared source material, not a Godot runtime dependency.

Preserve mission save keys and the host-owned arcade scoring boundary when
adding districts or activities. Do not put AI keys, account tokens or learner
recordings into this project or its exported assets.


## Character creation and the life loop

First launch now opens creation with the actual two protagonists, a local saved
profile and a rotating preview. Home wardrobes and the mall fitting point reopen
it without resetting mission progress. The classroom adds a teacher and timetable.
See [implemented features, model limitations and remaining world work](../../docs/CHARACTER_CREATION.md).

## Supplied characters and varied town cast

Use **ContextSpeak → Meshy → Open character preview** to rebuild and inspect all
34 textured, metre-scaled game characters in `Assets/Generated/Meshy/CharacterPreview.unity`.
The two navy-uniform protagonists, **Academy** and **Daydream**, remain player-only.
The Forest Café district uses all 32 other human identities: no body model repeats
within that scene. The original café and Life town also use distinct bodies for
all their named actors. Gameplay IDs and existing saves are preserved.

The explicit assignments and resident positions live in
`Assets/_Game/Art/Meshy/cast.json`. Residents have distinct saved conversation IDs,
while their topic prefix selects the existing IELTS 3–9 curriculum. Walk up to a
resident and press **F** to talk. Their voice, correction/repeat gate and automatic
feedback use the existing conversation system. Seated people, skateboarders and
the pianist keep their authored poses and props rather than receiving a walking
animation that does not fit their model.

Moving customers and the waiter have weighted basic locomotion rigs. Most other
new bodies remain stationary. T-pose arm relaxation affects only prepared game
copies. TrenchElder and OldCardigan bake their supplied walking pose into the
prepared mesh, then receive a simple metre-scale rig; their original centimetre
rigs and full animation clips remain intact in the native FBX library. These are
not facial rigs or complete animation retargeting. Academy and Daydream retain
their supplied skeletons/clips; imported controllers stay disabled so a vault
cannot play as idle.

Prepared GLBs live in `Assets/_Game/Art/Meshy`; normal Unity builds do not need
Downloads or Blender. Existing game copies stay below 60k triangles / 2048px;
new additions use 30k triangles / 1024px. Rebuild locally with:

```sh
python3 tools/meshy/library.py --downloads "$HOME/Downloads"
python3 tools/meshy/prepare.py --downloads "$HOME/Downloads" --skip-preview
python3 -m unittest discover -s tools/meshy
pnpm unity:test
```

Use `--only TrenchElder OldCardigan` for an incremental rebuild. The prepared
manifest records source hashes, texture channels, rig provenance and retained
source animation names. All conversion is local; original downloads are untouched.
These user-supplied assets are not asserted to be CC0 or freely redistributable.
See [the complete cast and import record](../../docs/FBX_CAST.md).

Unity's import/animation reference: [rig types](https://docs.unity.com/en-us/engine/6000.6/manual/assets-and-media/asset-types/models/importing/fbximporter-rig),
[glTFast features](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/features.md).

## Complete native Meshy FBX library

Import every FBX from Downloads, including unbranded ZIPs, renamed archives and loose `.FBX` files:

```sh
python3 tools/meshy/library.py --downloads "$HOME/Downloads"
python3 -m unittest discover -s tools/meshy
```

Then use **ContextSpeak → Meshy → Build and verify FBX library** once in Unity.
**ContextSpeak → Meshy → Open FBX library** provides search, FBX selection, prefab
selection and a single-model preview. Drag a `Model.prefab` into the scene to use it.
The native files, textures, URP materials, prefabs and import report live under
`Assets/_Game/Art/MeshyLibrary`.

The importer preserves source geometry, source scale, rigs and supplied animation
clips. Preview fitting changes only the preview instance. Imported animators are
disabled on wrapper prefabs until a suitable controller is assigned. Static meshes
still need rigging before they can walk. One source FBX has no accompanying texture;
it receives a neutral material rather than an invented texture.

This source library is separate from the 34 optimized gameplay characters above.
Many native sources have
very high polygon counts: select and optimize assets for an actual scene before
shipping them, rather than loading the entire source library into gameplay.

The multi-GB user-provided library is intentionally ignored by Git and rebuilt from
local Downloads; original ZIPs/FBXs are untouched. `manifest.json` records SHA-256
hashes, texture paths and duplicate source aliases. Exact duplicate FBXs are stored
once. WebP images mislabeled as PNG/JPEG are converted in the local library so Unity
can import them; original downloads remain unchanged. Roughness/metallic maps are packed into URP's R/alpha layout; imported textures
are limited to 2048px. Pillow is required for packing (`python3 -m pip install Pillow`
if it is not already available). No conversion service or cloud upload is used.

## Sound and friend rooms

**Settings → Sound** remembers master mute/volume across restarts and districts.
The HUD adds quick Sound and Friends controls. Four-player private rooms share
avatars and NPC practice status while personal saves and recordings stay local.
`pnpm world:dev` starts the separate local room service automatically. Different
computers need the same reachable room endpoint; no public service is deployed.
See [controls, self-hosting and networking boundaries](../../docs/FRIEND_ROOMS.md).
