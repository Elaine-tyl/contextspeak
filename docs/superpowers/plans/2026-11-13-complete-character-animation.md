# Complete character animation replacement plan

Execute inline with superpowers:executing-plans and test-driven-development. The requested replacement is fully specified and routine implementation decisions are delegated. One fresh final read-only reviewer.

**Goal:** Apply all43 supplied takes to three existing identities and remove superseded project duplicates safely.
**Spec:** ../specs/2026-11-13-complete-character-animation-design.md
**Review Focus:** wrong FBX companion selection; missing/nonmoving takes; path/bind-pose mismatches; duplication through later import; loss of original user assets; changing profile/NPC IDs; modifying the concurrent city work or unsaved scenes.

- [x] Add RED intake/replacement tests. Extend selective import and replacement registry; prepare the three animated source/model records with reversible backups.
- [x] Export complete take bindings and game copies. Adapt native import/playback and preview enumeration, remove obsolete retargeting and source copies only after validating replacements.
- [x] Verify actual clips, materials, scale, skeleton deformation and existing identities in Unity. Refresh the three stable prefabs without rebuilding or saving open city scenes. Run source/runtime regressions and one fresh final review; record actual checks and remaining limitations.

Verification: all33 Meshy Python regression tests passed. The existing Unity6.3 Editor imported the updated scripts and verified47 supplied clips (43 new plus4 existing NPC clips), actual vertex deformation, textured materials, scale and stable gameplay prefabs. Fresh review found a long-animation preview timer bug; it now uses the selected clip rather than Walk. No open scene was replaced or saved. Browser and desktop distributables were not rebuilt while separate city work continues.

Reproduce with the source packs in Downloads (renamed/nested archives are resolved by exact member):

```sh
python3 -m tools.meshy.animation_upgrade
python3 -m tools.meshy.prepare_motion
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python tools/meshy/convert.py -- .local/meshy/animated-upgrade/normalized Academy Daydream Elliot --skip-preview
```

In Unity, use `ContextSpeak > Meshy > Apply complete character animations` to rebuild and verify the stable prefabs. After a passing report, `python3 -m tools.meshy.animation_upgrade --retire` removes the superseded project copies with recovery backups in `.local/meshy/animated-upgrade/backup`. Original Downloads files remain intact. Native source FBXs and generated prefabs follow the repository's existing ignored-asset policy.
