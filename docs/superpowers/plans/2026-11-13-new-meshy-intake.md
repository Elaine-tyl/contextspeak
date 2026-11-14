# New Meshy download intake

Continue the approved asset-import workflow using the existing Unity/Blender pipeline. No new gameplay systems, engine changes, commits, or publishing. Preserve concurrent city edits and all open scenes. Original Downloads remain untouched.

- [x] Make incremental intake preserve existing source records. Skip unrelated invalid ZIPs and project backups; keep explicit unsafe-member validation and retired-source filtering. Regression tests cover missing old downloads and real archives.
- [x] Import the nine distinct new FBXs and inspect real geometry, textures, bones and takes in Blender. Register the two humans and their actual animations using stable new IDs; prepare bounded scene/game copies and additive cast/catalog entries.
- [x] Refresh and verify native Unity assets without rebuilding/saving the open scene. Run all Meshy regressions and one fresh read-only review. Report imported counts and distinguish project assets from distributable builds.

Review focus: old sources dropped by incremental imports; companion/static duplicates; destructive scene rebuild; unidentified animation clips; material loss; false claims of in-scene use; effects on existing actor identities.

Verification completed:36 Meshy Python regressions passed. Native Unity report verifies9 new imports,290 active source models with URP materials/prefabs,54 supplied motion clips (including7 CafeCharm takes), character deformation and prepared game geometry. BraidedSchoolgirl reduced from3,078,174 to29,900 triangles; new scenery game copies remain bounded. Existing retired-source hashes remain absent. Fresh read-only review found no blocking issue.

The intake prepared47 character assets and registered two additional residents (Mira/CafeCharm and June/BraidedSchoolgirl). Seven scenery entries received collision-checked locations and addressable prefabs. No existing scene was rebuilt or saved, so the two residents require the next world rebuild to appear in an existing scene. Browser/Mac distributables were not rebuilt. Original Downloads remain unchanged; no commit/push requested.
