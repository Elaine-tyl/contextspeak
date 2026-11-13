# Downloaded FBX gameplay assets

The completed-download snapshot contains **289 file entries, 284 distinct FBX hashes**. Files downloaded after this snapshot are not part of this build. The gameplay catalogue assigns each distinct hash to a character, a supplied motion, or a placed scenery object. Repeated archive aliases do not create duplicate NPCs.

- 45 character identities: two protagonists and 43 distinct talking residents.
- One additional FBX supplies Daydream's running action.
- 238 static model variants: architecture, furniture, wardrobe displays, vehicles, market displays, waterside objects, animals and botanical/craft trails.

PBR exports retain supplied emission and intentional base-color cutouts. Standing character copies share a 1.70-metre base height; the seated pianist and mounted rider keep proportionate seated bounds. Player profile height/build adjustments remain available.

Original downloads and the full-resolution `MeshyLibrary` remain unchanged. The game uses optimized copies. Static uniform variants are wardrobe exhibits rather than duplicate talking protagonists.

## Try it

Open the Forest Café world. In **Map → Explore downloaded scenery**, choose an area and model to visit its safe approach; press **E** to inspect it. The objects also appear while walking through the town.

**Settings → Supplied animations** temporarily previews all seven supplied clips without changing the saved character or controlling a friend:

| Character | Supplied action | Normal use |
|---|---|---|
| Daydream | Casual walk | Walking movement |
| Daydream | Fast run | Running movement |
| Academy | Unarmed vault | Explicit in-place preview |
| TrenchElder | Walking | Movement playback and explicit preview at the resident |
| OldCardigan | Walking | Movement playback and explicit preview at the resident |
| OfficerMason | Walking | Movement playback and explicit preview at the resident |
| OfficerTaylor | Walking | Movement playback and explicit preview at the resident |

Vault previews do not move the gameplay controller through obstacles. Idle uses a sampled pose. Academy's walk/run reuse the downloaded Daydream Mixamo actions through explicit skin-bind-pose retargeting; these two adapted clips are not claimed as additional original downloads. Normal movement walks; Shift runs. Motion selection follows the controller state, including returning to walk after releasing Shift. Other characters retain their supplied static pose or existing basic rig; the importer does not invent full animation sets.

## Reproduce locally

Use the project's pinned Unity Editor and installed Blender, plus Python with Pillow. Keep the original downloaded archives/FBXs available.

```sh
python3 tools/meshy/library.py
python3 tools/meshy/prepare.py
# MeshyLibrary's embedded character textures are extracted by its Unity importer.
python3 tools/meshy/prepare_motion.py
python3 tools/meshy/prepare_world.py
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python tools/meshy/context_export.py
python3 tools/unity/build.py test
python3 -m unittest discover -s tools/meshy -p 'test_*.py'
python3 tools/unity/build.py web
python3 tools/unity/build.py mac
```

The Unity library importer must run before preparing native motion when starting from empty generated assets; use **ContextSpeak → Meshy → Build and verify FBX library** for the source library. The game build generates prefab assets, validates existing motion rigs and creates reproducible scenery placements against the current town's navigation mesh. `catalog.json` records source hashes, aliases, purposes, budgets and those positions.

## Resource limits

Each scenery prefab has its own Addressables bundle. The runtime loads at most **20 nearby objects**, at most **two concurrently**, within a 22-metre radius, with a combined **400,000-triangle scenery budget**. It releases distant instances and late loads after travel or scene exit. Pending requests reserve their budgets until completion. Saved player positions recover to connected navigable ground if new scenery occupies the old spot.

Most static exports target 16,000 triangles and 512-pixel textures. Complex disconnected source geometry may retain up to 180,000 triangles to preserve recognizable surfaces and UVs; the aggregate streaming budget still applies. Animated native FBXs target 30,000 triangles and 1,024-pixel textures. Full-resolution library files are not player build dependencies.

Validation checks every distinct hash, actual rendered geometry/texture budgets, metric scale, all scenery approach paths, and vertex deformation for every supplied clip. Browser tests cover real streaming/map travel and automatic walk/run/explicit vault playback alongside existing game/room/save regressions.

## Remaining polish

Some variants share a visible map label although their source IDs and models are distinct. Collision proxies are conservative, so a few narrow props keep the player farther away than their visible edge. Repeating a vault preview extends its timer instead of restarting the one-shot. Not every building is enterable or display item purchasable. Low-memory device performance and individual prop art fidelity still need pilot testing.

Embedded-browser note: automated canvas focus in the ChatGPT in-app browser can raise a pointer-lock WrongDocumentError; dismissing it still allowed map navigation. The Chrome regression runner did not reproduce this. Use Chrome for the pilot until this host-specific input issue is resolved.

## Applied environment

The Forest Café scene now uses imported spruce/pine trees plus one detailed indoor oak, meadow surface textures and plant patches, downloaded asphalt over existing village roads, and downloaded café tables/chairs. The collision ground, seats and mission identities remain in place. These contextual copies share mesh/material assets and use LOD culling for trees/plants; native verification caps their combined pre-cull geometry at two million triangles and estimates at most one million in representative café, road and forest camera frustums; the streamed catalogue remains available to inspect every source. The two piano composites use a 1.35m total visual height relative to 1.70m standing characters, within their 3m plot envelope; their source proportions are preserved. Aircraft are scaled airport displays rather than flyable vehicles.

The 400k streaming limit applies to nearby catalogue exhibits, not to the permanent environment and character meshes. Desktop/browser profiling on low-memory devices remains pilot work.

## Latest validation

The applied-environment revision passed Unity scene, UI and interaction checks; 27 source/material tests; 616 web unit tests; and 13 browser regressions covering source use, both protagonists’ walk/run/Ctrl/crouch controls, recording/correction/orders/delivery, friend rooms and persistence. Web/macOS builds succeeded. The 30-frame Chrome sample on this Mac had median frame intervals around16.6–16.7ms (p9518.3–18.4ms); this does not establish VRAM or low-memory-device performance. Native desktop-window microphone replay was not repeated in this revision.


## Applied architecture

The frozen November 13 intake contains no new distinct FBXs beyond the existing 284. All 34 inspected architecture/interior models now have explicit contextual assignments in `architecture.json`: seven functional building exteriors, ten interior compositions and seventeen outer-district landmarks. Their separate shared-mesh budget is 600k triangles before culling. Original downloads and full library models remain unchanged.

Closed source buildings use a life-sim cutaway: outside shows the authored exterior; entering the footprint reveals the functional room. E at an entrance enters, E at an exit leaves, and F talks to the NPC. Existing room/NPC IDs, voice, quests, inventory and save coordinates remain authoritative. Exteriors keep uniform proportions and cover the room footprint; interiors retain the original gameplay layout plus the assigned imported furnishings. A closed FBX is not presented as an authored walkable interior.

The outer district includes housing and city landmarks. These are scenery, not newly implemented airport, hospital or cathedral missions. Imported interiors have static mesh collisions; generation validates NPC/service paths and both entrance/exit clearance. Catalogue exhibit approaches are repaired against the final navigation bake, including interaction distance, so an added object cannot silently strand the inspection point.

Architecture validation: full Unity scene verification, native UI and interaction checks,28 source tests,616 web unit tests and TypeScript checks passed. The new seven-building browser regression passed after waiting for explicit Save/flush on reload;13 existing browser regressions also passed. WebGL and macOS builds succeeded. Actual browser exterior/interior screenshots are in `docs/images/building-exterior.jpg` and `building-interior.jpg`. This revision did not repeat a human-microphone test in the desktop app.
