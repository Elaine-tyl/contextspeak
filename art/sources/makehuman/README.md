# Reference player source provenance

The character is built by `tools/blender/reference_character.py` without installing or importing MakeHuman or MPFB. The script loads graphical data only. It is reproducible offline using the files in this folder.

## License

The MakeHuman mesh and morph targets are CC0-1.0. The base OBJ and individual targets carry their explicit September 2020 CC0 declaration. `LICENSE.md` explains the split license; `LICENSE.ASSETS.md` contains CC0. MakeHuman program code is AGPL, but no MakeHuman program code is included in this builder.

The official MPFB `LICENSE.md` in `mpfb2/` explicitly includes rigs and JSON mesh information in its CC0 asset definition. MPFB program code is GPL; no MPFB program code is included in this builder. Only the graphical rig/weight/mesh metadata JSON files are present.

The selected skin, eye, eyebrow, and eyelash files are from the official **MakeHuman system assets** pack, individually listed as CC0 at <https://static.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html>. They were extracted using HTTP byte ranges, ZIP CRC verification, and SHA256 checks rather than downloading the complete 267 MB pack.

## Exact versions and integrity

`provenance.json` records the full source URL, pinned Git revision where applicable, archive entry/ETag for pack assets, and SHA256 of each file. The builder verifies all recorded checksums before creating geometry.

- MakeHuman revision: `a8bc2d54ff0ac92e78ff71431b1023eda42bf482`
- MPFB revision: `6ff1eb358f6c1959ced77e79e6dd2406d9a71f49`
- Official system pack: <https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip>
- System pack HTTP ETag: `10bbb7ea-6160e7c0d7996`

## Geometry and original project work

The hm08 OBJ has 19,158 vertices / 18,486 faces including helpers. Only `body` (13,378 faces) contributes to anatomical render geometry. Helpers and joint cubes are used to fit the morphs, body-part proxies and skeleton, and are excluded from exported geometry. The complete morphed anatomical mesh remains hidden and editable in the `.blend`. The exported skin omits occluded torso polygons to prevent garment clipping.

The female young macro blend and small facial morph values are recorded in the script and root object's `morph_recipe`. These are artistic settings, not a claim of measured age or identity.

Garments, open cream hoodie, stripes, cuffs, canvas shoes, leather details, backpack, hair shell, swept locks, coiled bun and accessories are original project geometry authored by the build script. The scalp shell is ray-fitted to the anatomical head. Hair and garments remain separate editable meshes with deform weights.

The skeleton has 53 deform bones, including fingers. Exported required nodes are `Head`, `LeftArm`, `RightArm`, `LeftLeg`, and `RightLeg`. Bone rest positions are derived from morphed helper coordinates; original compatible CC0 weight data is preserved, limited to four normalized influences for game use. Garments use nearest-body weight interpolation; extreme animation poses remain unvalidated.

Microscopic procedural cloth bump is retained in Blender for the review render; it is not baked into exported PBR textures. GLB includes skin/eye/eyebrow/eyelash textures and ordinary matte PBR garment materials. Facial animation shape keys are not included in this candidate.

## Offline regeneration and review

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/reference_character.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python tools/blender/reference_character.py -- --barista
```

The player variant writes `art/blender/ReferencePlayer.blend` and `.local/art-candidates/ReferencePlayer.glb`, `.png`, `-face.png`, and `.qa.json`. The barista option writes the same artifacts with `ReferenceBarista` names. It uses a cream cotton shirt with rolled sleeves, a brown bib apron with pocket and straps, and no backpack; it does not reuse the player's hoodie as a uniform.

Runtime GLB geometry is joined into one skinned mesh with material primitives; individual editable parts remain separate in the `.blend`. Every exported PNG texture is capped at 1024 pixels (source files remain untouched). The built-in artifact validator checks buffer bounds, finite positions, triangle indices, normalized weights, 53 joints/inverse binds, required named joints, excluded source/helper geometry, and embedded texture dimensions. `.qa.json` reports the actual GLB triangle, mesh, primitive, material, texture and byte counts.

The validator also runs without Blender:

```sh
python3 tools/blender/reference_character.py --validate-glb .local/art-candidates/ReferencePlayer.glb
python3 tools/blender/reference_character.py --validate-glb .local/art-candidates/ReferenceBarista.glb
```

The model faces Blender local `-Y`. Y-up glTF export maps that to local `+Z`; glTFast's X reflection leaves it facing Unity local `+Z`, so no corrective yaw is required. The exported bone rest transforms must be preserved when applying the simple Unity animation driver.
