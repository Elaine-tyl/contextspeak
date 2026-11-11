# Free 3D people and café assets — checked 10 November 2026

Goal: browser + desktop English speaking scenes with voiced NPCs, walking,
seating and a comfortable conversation camera. Prefer creator-confirmed CC0,
lightweight glTF/GLB, and matching animated rigs. “Free” here refers to the exact
free edition, not paid source files or expansion packs.

| Candidate | Primary source / license evidence | Fit and adoption work |
| --- | --- | --- |
| Quaternius Universal Base Characters | [Creator page](https://quaternius.com/packs/universalbasecharacters.html), [free Standard download](https://quaternius.itch.io/universal-base-characters); page labels CC0 and includes glTF. Paid Source has additional content. | Best candidate for more human proportions than the current chibi people. Humanoid rig; about 13k triangles per base. Needs clothing/hair selection, animation retargeting and browser-performance checks. Do not assume every advertised variation is in the free edition. |
| Quaternius Universal Animation Library | [Creator page](https://quaternius.com/packs/universalanimationlibrary.html), CC0, GLB and Godot exports listed. | Useful for walking, sitting and social actions. Check the exact free archive for required clips and match the rig; additional editions exist. |
| Quaternius Animated Man / Woman and Ultimate Modular Men | [Man](https://quaternius.com/packs/animatedman.html), [Woman](https://quaternius.com/packs/animatedwoman.html), [Modular Men](https://quaternius.com/packs/ultimatemodularcharacters.html); creator pages show CC0. | Casual clothed humans with baked animations can be quicker than assembling base bodies. Older FBX/Blend packs need import/animation inspection; no assumption that every clip includes seated conversation. |
| KayKit Restaurant Bits — FREE | [Creator page](https://kaylousberg.itch.io/restaurant-bits); free edition is CC0 and includes low-poly GLTF kitchen/food assets. Extra and Source editions cost money. | Strong coherent café/restaurant scene candidate. A shared atlas keeps materials light. Choose the FREE edition; do not blindly copy a mirror's entire bundle. |
| Kenney Mini Characters, Furniture and Food | [People](https://kenney.nl/assets/mini-characters), [furniture](https://kenney.nl/assets/furniture-kit), [food](https://kenney.nl/assets/food-kit); CC0. | Currently integrated: known working idle/walk/sit clips, small files and tested textures. Reliable fallback while evaluating more human-proportioned alternatives. |

## GitHub sources inspected

- [KayKit props index/mirror](https://github.com/GeorgeQLe/assets-kaykit-3d-props):
  useful for discovery. The creator's FREE archive and each included license must
  determine what gets imported; repository availability alone is not permission.
- [Quaternius rig/animation merge workflow](https://github.com/NafisRayan/Animate-Rigged-Humanoid-No-Blender):
  documents combining compatible base characters and animation libraries into
  GLB. It is a workflow reference, not imported game code or verified characters.
- [Current Kenney mirror](https://github.com/shorepine/kenney): selected files are
  already pinned and checked in [art provenance](../games/cafe/assets/CREDITS.md).

## Decision

Complete and test the continuous counter→table→friend experience with the
currently working animated assets. The next art upgrade should use Quaternius
casual/humanoid characters and the KayKit FREE restaurant kit after testing a
sample with idle, walk and sit animations in both exports. Avoid a large realistic
scene whose textures, draw calls or rig incompatibilities undo browser usability.

No new candidate pack was purchased or silently imported in this research step.
The game currently uses the credited Kenney assets. Imported future files must
retain their original license, exact version/source URL and integrity hash, and
must be tested in the actual game before replacing the working fallback.

## Implementation update — 10 November 2026

The newer base rig remains a candidate. The actual integrated characters use the
creator's **Animated Woman Pack** with its baked Idle/Walking/SitIdle clips and
three free clothing atlases. A shared rig avoids a new retargeting dependency and
keeps the walking/seated journey working. KayKit **Restaurant Bits 1.0 FREE** tables,
chairs, cabinets, sink, plate rack, jars and entry door now appear in the café.
Kenney is retained for selected food, plants and coffee equipment.

This supersedes the research-only decision above: selected files have now been
imported, their free-edition licenses verified, and their hashes recorded in
[art credits](../games/cafe/assets/CREDITS.md). No paid Source/Extra content is used.
See [browser/native playtest](CAFE_FREE_ART_PLAYTEST.md) for actual verification.

## Scene expansion research — 10 November 2026

Checked the creator pages for [Kenney Food Kit](https://kenney.nl/assets/food-kit), [Kenney Nature Kit](https://kenney.nl/assets/nature-kit), and [KayKit Restaurant Bits](https://kaylousberg.itch.io/restaurant-bits). All three identify a CC0 free offering. KayKit's Extra and Source editions remain paid and were not imported. The public [shorepine/kenney GitHub mirror](https://github.com/shorepine/kenney) supplies the already-vendored Kenney files pinned in `games/cafe/assets/sources.json`; it is a mirror, not a separate art author.

The shopping, farm, market, restaurant and amusement-park scenes use existing verified Quaternius animation rig, Kenney plants/food and KayKit furniture. No additional model downloads or packages were needed. Clothing displays, fairground rides, crop beds and vegetables, market stalls and baskets, restaurant dressing and dialogue-result props are original procedural ContextSpeak geometry in `games/cafe/scripts/roleplay_world.gd` and `games/cafe/scripts/roleplay_friend_art.gd`. They do not derive from paid models or paid source code. Nature Kit is a verified future option, not a newly imported dependency.
