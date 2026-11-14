# Luxury Villa — development progress

| Date | Change | Verified how |
|---|---|---|
| 2026-11-14 | V1 villa: Blender generator, 4 GLB/FBX parts, runtime (doors, fixtures, lights, save, family, 8 IELTS topics), builder, checks, play test | Cloud compile; Unity compile (Editor.log, no CS errors) |
| 2026-11-14 | 3rd floor (cinema, gym, sky lounge, powder room), roof terraces, U-stairs ×2, passenger lift, umbrellas / parasols | Cloud + Unity compile; build menu ran (LuxuryVillaBuild.md) |
| 2026-11-14 | Build report showed every room unreachable → cause found: stone base course across the entrance + garage door | Fixed in Blender; needs rebuild + Checks |
| 2026-11-14 | Player walked backwards in all scenes → DressPlayer kept spawn yaw on the visual; runtime PlayerFacingFix + tools/facing_patch.py | Needs Play mode confirmation |
| 2026-11-14 | V9 visual upgrade: trim/sills/frames/flashing, upholstered furniture, detailed kitchen, real vegetation, surroundings, glass material, shared HDRI/SSAO lighting stack, light switches, wind, capture tool | Blender previews only; Unity shots pending |
