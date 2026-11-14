# Complete character animation replacement

Replace Academy (female protagonist), Daydream (male protagonist) and Elliot with the supplied All_animation archives. Inspection found15/19/9 authored takes respectively. Each archive also contains an unanimated character_mixamo companion; select only its All_animation FBX as the canonical source. Stable character IDs, prefab paths, appearance profiles, role assignments and quest identities remain unchanged.

Keep one canonical animated source per identity in the native library. Retire the six previous family hashes (three static and three single-action variants) through an explicit replacement registry so future imports cannot resurrect them. Move obsolete project copies and matching meta files into the ignored local upgrade backup before removal; retain Downloads originals. Replace old GLB contents under stable filenames with the animated version for the inspected-source catalogue.

Store each complete native FBX once, bind every real take explicitly, use each model's own Walk/Run and Academy's own Vault, and retain all other authored takes as selectable previews. Preserve in-place gameplay physics, textured materials and1.7m standing scale. Remove the now-unneeded cross-character Academy retarget fallback. No invented source idle: the existing neutral sampled idle remains labelled separately.

Regressions cover selective archive intake, retired-source filtering, actual exported take counts, native bone/vertex deformation and playback mappings. Unity is already open with separate in-progress city edits: apply only character prefabs through an editor refresh request, preserve open scenes, and validate in a separate native probe if needed. Do not commit/push or delete Downloads archives in this task.
