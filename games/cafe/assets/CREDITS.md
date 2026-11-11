# Café art credits and provenance

Selected art is by **Kenney**, released under CC0 1.0 Universal:

- Mini Characters: https://kenney.nl/assets/mini-characters
- Furniture Kit: https://kenney.nl/assets/furniture-kit
- Food Kit: https://kenney.nl/assets/food-kit
- CC0: https://creativecommons.org/publicdomain/zero/1.0/

The official pages identify these packs as CC0. This repository vendors only the
models and textures used by the café, not the full asset libraries. The original
rooms, placement, interface and task dialogue are ContextSpeak work.

The official ZIP host returned HTTP 403 during development, so these files were
retrieved from the public shorepine/kenney mirror at commit
`3694c6879e487c108f55677be7dd2ca75b07cc3b`. The mirror attributes its art to Kenney
under CC0. `sources.json` records the exact URL and Git blob hash of every file;
`python3 tools/world/verify_assets.py` verifies the vendored bytes offline. Builds
run this verification automatically. Textures remain beside their GLB references.

Mirror and license:
https://github.com/shorepine/kenney/tree/3694c6879e487c108f55677be7dd2ca75b07cc3b
https://github.com/shorepine/kenney/blob/3694c6879e487c108f55677be7dd2ca75b07cc3b/LICENSE.txt

## Integrated free art upgrade — 10 November 2026

**Quaternius Animated Woman Pack (December 2017), CC0:**
https://quaternius.com/packs/animatedwoman.html

The shared rig includes Idle, Walking and SitIdle, so no paid retargeting or base
character Source kit is needed. Original `Animated Woman.fbx` is stored as
`quaternius/woman.fbx`. The original LightSkin.png, DarkSkin1.png and DarkSkin.png
atlases are stored as woman-clothed.png, barista-clothed.png and learner-clothed.png.
They are unmodified bytes from the creator's linked public Google Drive folders.
The same rig is instantiated three times with different clothing; normalization,
clothing assignment and action aliases are ContextSpeak runtime code. This uses
the older animated pack, not Universal Base Characters or its paid Source edition.
`quaternius/License.txt` is the official CC0 legal text, not an invented author grant.

**Kay Lousberg — KayKit Restaurant Bits 1.0 FREE, CC0:**
https://kaylousberg.itch.io/restaurant-bits

Downloaded using “No thanks, just take me to the downloads” → Free, without a
payment/account. Original archive: `KayKit_Restaurant_Bits_1.0_FREE.zip`.
SHA-256: `75f75f0a1ae569f3f2348fa7a04ccd45d2f010d419045755c70ae43289b68e4b`.
Selected tables, chairs, counters, sink, side table, plate rack, jars and door
retain their original glTF/bin/image bytes and creator-issued `kaykit/License.txt`.
Extra and Source editions are not included. The one 1024px restaurant atlas is
shared by the selected furniture. Kenney remains for the coffee machine, food,
plants and other decor not covered by this subset.

Every new file has a SHA-256 in `sources.json`; KayKit entries additionally record
its archive hash and member path. Quaternius entries record exact public download
URLs. Existing Git-mirrored files retain their Git blob hashes. The build verifies
both schemes offline. Source-pack ads/screenshots and entire third-party libraries
are not shipped. Prior Kenney resources remain credited, including legacy art
available in this development repository.


## Campus appearance — original art update, 10 November 2026

The current **visible** faces, hair, varsity jackets/hoodies, trousers, backpacks
and sneakers are original procedural ContextSpeak artwork (`campus_style.gd`).
They follow BoneAttachment3D nodes on the credited free Quaternius Animated Woman
rig; the original source mesh is hidden. This retains the tested idle, walking
and seated clips without claiming the original artist created the new clothes.
The previously credited source meshes/atlases remain in this development repo.

KayKit model/texture bytes remain unchanged. The original ContextSpeak shader
`cafe_palette.gdshader` recolours orange furniture areas to the selected cream,
wood and sage palette. Pendant lamps, slatted timber, rugs and welcome signage are
original scene geometry. No paid pack, additional character license or AI image
asset is introduced by this appearance update.

The official Universal Base Characters **Standard** ZIP was inspected, SHA-256
`fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40`.
Its free Godot/UE base files contain Superhero_Female_FullBody and
Superhero_Male_FullBody; it has no Teen entries. No bytes from that inspected ZIP
are included in the game. The requested teen look is an original stylized design,
not a claim that paid Teen variants were downloaded for free.

## Original roleplay environments

The shopping, farm, market, restaurant and amusement-park layouts, clothing displays, rides, crop and produce models, stalls, dialogue-action props and animations in `scripts/roleplay_world.gd` and `scripts/roleplay_friend_art.gd` were independently authored for ContextSpeak. They reuse only the credited, already-vendored CC0 rig, furniture, food and plants above. No paid KayKit extras or Quaternius source/character editions are included.

The cozy polish adds original filtered oak/plaster surface shaders, rounded
pendants, joinery, restrained foliage animation and revised campus garments.
The user's reference screenshots guided the palette and atmosphere; their
images/models are not bundled. Vendored third-party asset bytes remain unchanged.

## Forest conservatory — original art update, 10 November 2026

The curved tree and planter bench, folded broadleaf geometry, outside garden,
stream, rocks and vines in `forest_garden.gd` are original ContextSpeak geometry.
`forest_architecture.gd` adds original timber glazing, roof framing, brass
lanterns and counter joinery. The forest concept board supplied by the user is
an art-direction reference and is not distributed as a texture, image backdrop
or other game asset. No new third-party download, paid asset or model license
is introduced by this update.
