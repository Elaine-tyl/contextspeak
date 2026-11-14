# Supplied Player Locomotion Repair

**Goal:** Use the exact 女主.zip FBX for walking, running and jumping, and validate camera-relative WASD against the animated character's facing.
**Architecture:** Keep CharacterController as the only physics movement owner; sample the supplied skeleton in place. Authored animations must not receive the procedural squash/bob fallback. Preserve uploaded art, patched code, scene composition, dialogue and saves.
**Scope:** The current request prioritizes protagonist locomotion and ZIP integration status. The attached V6 world/transport/water requirements must remain explicitly tracked; they are not completed by this repair.

- [x] Compare both uploaded ZIPs and female animation source hashes; record retained patches.
- [x] Run native diagnostics and failing checks for direction, jump selection and procedural deformation.
- [x] Fix confirmed locomotion and jump ownership defects using supplied clips; add regressions.
- [x] Verify native Unity and export the current authored browser scene without regeneration; test real controls.
- [x] Record exact verification, ZIP status and outstanding V6 subsystems. No commit/push requested.

## Verification ledger

- ZIP audit: 81/81 art files identical; 122/122 code files present, 7 intentionally retain later corrections. Female FBX SHA-256 matches the archive exactly.
- Native RED: four-direction walk facing passed; procedural deformation, reversal-facing, missing jump and jump/fall Idle selection failed.
- Native GREEN: derived in-place jump from the supplied longest leap segment; phase follows physical vertical velocity. Preserved 54 original clips, 95 vertex pose samples and 9 uploaded-design checks.
- Fresh scoped review found loss of crouch and seat lowering. Reproduced both failures, restored rigid seat offset and extracted a grounded static crouch without body scaling. Native GREEN passed again.
- Gameplay suites: 335 slice checks and 60 living-world checks passed; 8 Python build/import tests passed.
- Decision: preserve the existing camera-relative third-person controls and configured gait speeds; fix momentum-facing rather than reverse WASD without evidence. The tests do not substitute for the pending browser input test.
- Decision: leave the uploaded design intact and export saved scenes from an isolated COW copy. The full V6 transport/water specification remains outside this focused repair.

- Browser test caught an unnatural one-legged crouch that passed height checks. A new both-feet-below-hips assertion failed on the old pose, then passed after requiring two grounded feet and an upright torso. Final native checks also validate jump torso orientation.
- Final browser export: `20261113.181345` (UTC). Verified four-direction movement, Shift running, Space jumping, grounded crouch, and opening Rowan's dialogue; retained screenshots in `.local/locomotion/`.
- Saved scene hashes match the original authoring project for all seven exported districts. Original working browser build retained in `.local/world-builds/previous-locomotion-20261113.181345`.
- Remaining limitation: existing roof beams/foliage can obscure the following camera, particularly during jumps. This was observed in browser and is not claimed fixed by locomotion changes.
- Desktop window could not be inspected because macOS is locked. Unity source/prefabs were updated; the standalone Mac package was not rebuilt.
