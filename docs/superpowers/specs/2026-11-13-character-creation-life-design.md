# Character creation and the first life loop

Source: the user's attached ContextSpeak Life upgrade requirements and CharacterCreation Unity6 ZIP. Implement immediately, reuse the existing project, prioritize a working vertical slice over empty districts.

## Decisions
- Integrate the archive's profile/name/gender/Band ideas into the current UI Toolkit + Input System + Addressables architecture. Do not copy its placeholder-body generator, legacy input module, destructive save replacement or independent scene-list override.
- Academy and Daydream remain the only protagonists. Other models remain NPC-only.
- First launch opens CharacterCreation before the requested world. Later launches continue normally. A wardrobe at home and fitting point at the mall reopen it; cancelling an edit keeps the saved appearance.
- The real 3D model can rotate 360 degrees; name, protagonist, height and build are editable and persistent. Bound proportions to the existing model's supported range. Keep physical player dimensions consistent.
- Appearance is a separate versioned JSON preference; preserve existing quest/learning saves and migrate the earlier gender-only preference. Persist before leaving creation. Report storage errors rather than claiming success.
- The existing single-mesh, single-atlas models have no separate face/hair/clothing parts or facial blend shapes. Display their authored default appearance honestly. Do not advertise unsupported face morphs, independent skin/eye/hair recolouring, wardrobe inventory or lip sync as complete.
- Add one accessible classroom with a teacher, timetable and shared Band 3–9 conversation task to the existing walkable town. Keep the full campus/Part 1–3 exam simulation as a later content milestone.
- Keep the existing home/market/café/order/fishing loop and unified STT/feedback/repeat checks. No new cloud service, keys or database.

## Approaches considered
1. Import the ZIP unchanged: fast standalone demo, but primitive previews, old input, fragile save and incompatible loading. Rejected.
2. Adapt its useful profile concepts into existing systems: selected to keep the real protagonists, working speech loop and cross-platform controls.
3. Rebuild a whole modular avatar pipeline first: needed later for true hairstyle/clothes/face changes; blocked by missing modular art, unnecessary for a usable creation-to-world flow.

## Completion evidence
Native compilation/UI/scene checks; profile round trip, corrupt data, invalid enum and migration tests; first run and reload; cancel/edit/continue; both protagonists; position/quest preservation; wardrobe interaction; teacher navigation/dialogue; browser and macOS builds. Keep a feature matrix separating real prototype interactions, mocked content and unimplemented later phases.
