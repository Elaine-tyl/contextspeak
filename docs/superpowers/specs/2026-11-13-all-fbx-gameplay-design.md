# Apply every FBX and supplied animation in gameplay

The user explicitly expands the previous import-only scope: every Downloads FBX must have a real game use, including supplied human animation. Routine art/layout decisions remain delegated.

The current inventory is 189 unique FBXs / 191 aliases. Thirty-four human identities are already in the cast, but most props and the five supplied clips are not used in play. Preserve source downloads, existing player roots, speech/correction, quests, saves, sound settings and friend rooms.

## Approach

- Generate an exhaustive source-hash usage catalog. Each unique source maps to a live character, bound animation, or optimized placed world object. Animation variants of the same identity are clips, not duplicate NPC bodies. Static protagonist variants can be clothing/display figures, never extra talking NPCs.
- Optimize every remaining static FBX locally in Blender. Keep geometry/UVs, original maps and source identity; cap triangles/textures, normalize by inspected purpose. Place buildings along village edges; home/café/market furniture, nature, animals and water props in matching districts. Unknown sculptural/composite assets get an explicit display purpose, not silent omission.
- Addressable per-object prefabs load near the player and release when distant. Generated placement bounds/proxies keep navigation safe; streaming must handle failures and stale loads without leaking handles. An in-game scenery/map list makes all placements reachable for inspection. No multi-GB raw library in the player build.
- Preserve supplied bones and clips in optimized FBX animation copies, using Unity's native importer to avoid the earlier glTF centimetre-bind problem. Blend supplied walking/running states where applicable. Idle is a sampled/generated pose, clearly distinct from source motion. Vault is an explicit action/preview, not blindly played as idle. Static assets without a skeleton/clip keep honest static/basic-motion status.
- Keep local and remote avatars on the same visual-motion pipeline; movement/controller authority stays local.

## Verification

Fail on any missing source hash, orphan address, missing texture, absent rig/clip, unmatched animation binding, unreachable object arrival or budget overflow. Measure actual animated vertex deformation, not just clip names. Browser tests verify nearby optimized props really load, source walk/run states change, vault action plays, and existing speech/room/save flows remain. Build Web/macOS and record limitations truthfully.
