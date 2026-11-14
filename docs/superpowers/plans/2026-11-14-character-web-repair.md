# Character Pose and Served Build Repair

**Goal:** Fix the actual browser game reported by the user, including character distortion/floating, and serve the current authored Unity scenes.
**Approach:** Diagnose native poses before changing animation code. Validate real baked vertices with one transform application, idle feet and locomotion bounds. Preserve the authored scenes when exporting; never invoke destructive CreateScene/Verify in the current-design export path. Rebuild Web and verify the served build and gameplay in Chrome. Keep saves and all new vehicles/NPC assignments.

- [x] Add and run native pose diagnostics for the current imported characters and player idle/walk/run.
- [x] Reproduce and minimally repair confirmed pose or grounding defects; verify regressions.
- [x] Add a safe authored-scene export path and verify it preserves scene hashes and writes a visible build identity.
- [x] Export the current Web build, replace the served assets and test the actual Chrome game (idle/movement/NPC interaction).
- [x] Record actual results and any remaining limitations; no commit/push.

## Verification so far

- Reproduced seven mid-stride idle failures against the previously generated prefabs.
- Rebuilt all eight animated character wrappers, retaining all 54 supplied clips.
- Corrected walk-only idle selection to prioritize double support; close-foot standing and short airborne running remain valid.
- Isolated native export preflight: all 47 models / 95 vertex samples pass; nine greenhouse visibility checks pass.
- Python build/intake checks: eight pass. Living-world C# checks: 60 pass.
- Export runs in an isolated APFS copy while preserving the open authoring project. Browser verification remains pending.

## Browser loading regression

- First rebuilt browser player reproduced abort(OOM) at the previous 2 GB heap cap; withheld it and restored the preceding browser build while repairing.
- District scenes now pack separately. Imported scene content is still large (ForestSlice bundle about 625 MB), so the desktop-browser build uses a 256 MB starting heap with growth up to 4096 MB.
- Export `20261113.171323` succeeded without shader errors; actual compiled heap cap is 4,278,190,080 bytes. All seven preserved authored scene hashes match the original project after the user-approved restoration of four regenerated scenes. Prior files remain backed up.
- Unity Editor opened the restored ForestSlice; Play mode confirmed brick flooring and a planted, uncrossed female idle pose. Stopped Play mode afterward.
- Browser runtime verification for the final memory build remains pending.

- Final 4 GB browser build entered ForestSlice successfully (new brick floor and planted idle visible). NPC-keyboard test exposed a browser pointer-lock rejection becoming a Unity fatal alert. Added a canvas-scoped wrapper that skips inactive documents and handles only expected permission/focus denials; three Node regressions pass and unexpected errors remain observable. Browser recheck pending.

## Final verification

- Browser build `20261113.171323` is served at http://127.0.0.1:4320/unity/?world=slice with a visible build label and content-hashed player files.
- Actual Chrome test: new greenhouse brick floor, planted female idle, movement/sprint input, counter NPC dialogue and connected voice/feedback status; wardrobe character screen and return to world. No new OOM or pointer-lock errors after the shell fix. This did not include a new human microphone assessment.
- Final browser screenshot: `.local/character-repair/browser-final.png`. Final receipt and scene snapshot retained beside the native pose reports.
- User approved restoring CharacterCreation, ForestCafe, ForestSlice and LifeTown after the original Rebuild cafe scene regenerated them during the session; regenerated files retained in `regenerated-scene-backup`. All seven preserved scene hashes match.
- The browser package remains large; texture and base-player payload optimization remains future work. The separate macOS executable was not rebuilt in this task. No commit/push.
