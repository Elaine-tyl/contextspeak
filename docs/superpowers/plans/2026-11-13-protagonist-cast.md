# Protagonist cast implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline. Keep unrelated work intact.

**Goal:** Reserve the supplied Academy girl and Daydream boy for the player; use distinct people for every NPC and ambient customer.

**Architecture:** Keep actor roots, navigation and quest components unchanged. A small runtime appearance component switches two child visuals and remembers the choice as a local preference. Other Meshy models fill NPC roles; Urban and Autumn receive explicitly generated basic leg/head rigs so mobile customers retain deforming locomotion without borrowing protagonist meshes.

**Tech stack:** Unity 6 LTS, C#, Blender, existing local FBX/GLB pipeline.

**Spec:** User's two protagonist reference images and request in this chat. Existing dialogue, recording, correction and order flow must remain intact.

## Constraints
- No source FBX edits, external uploads or new dependencies.
- At most 60,000 triangles and 2,048px textures per prepared character.
- Distinguish supplied rigs from locally generated basic rigs; no claim of full facial animation.
- No automatic commit: current request is role allocation.

## Review focus
- Main characters must never spawn as NPCs or ambient customers.
- Switching appearance must retain player position, collider, hand props and mission state.
- Invalid/missing stored choices must select the girl safely.
- Both selectable visuals must preserve their real skeleton and texture.
- Basic NPC skin weights must deform actual mesh vertices and not create navigation blockers.

## Tasks
- [x] Add failing GLB checks for real weighted Urban/Autumn rigs; implement deterministic Blender rig preparation and verify geometry/material budgets.
- [x] Add failing saved-scene cast checks. Assign adult NPCs/customers separately, add player visual switch and local preference with invalid-choice tests, and keep gameplay roots unchanged.
- [x] Build and verify Unity scenes, browser and desktop. Exercise character switching/reload, movement, dialogue, correction, ordering/delivery. Record limitations and provide a screenshot.

## Verification notes
- RED: the existing Autumn GLB had no skin; current Maya reused Academy; current player had no appearance switch.
- GREEN: Urban/Autumn have seven actual weighted joints, no invented source clips, and remain under 60k triangles/2048px textures. Unity bake/deformation checks prove leg rotations move real vertices.
- Export fix: glTF required a clean mesh transform parented under its armature. Baking the prepared mesh transform corrected Unity's measured scale without modifying source FBXs.
- All three worlds pass player-selection and NPC-exclusion checks (23 NPCs total). Native UI, navigation/interaction checks pass. Python Meshy tests: 12; Unity build-tool tests: 3; project suite: 609.
- Focused independent read-only review found no important actionable defects.
- Decision: local appearance preference is separate from quest saves, shared across the three worlds; selection remains in Forest Café Settings. No full character-creation system or facial rig added.

- Final Web/macOS builds succeeded. All seven selected browser regressions passed, including choice/reload, movement, recorded correction, single-charge order delivery, adaptive conversations and vocabulary trail persistence.
- Desktop window visibly showed Academy and separate Urban/Autumn NPCs plus both settings buttons. Standard Chrome screenshot proof is under `docs/images/unity-protagonist-settings.png`.
- Embedded automation key input hit a pointer-lock WrongDocumentError (known browser-control incompatibility); dismissed its alert and closed the disposable tab. Standard Chrome engine and native UI checks passed; no gameplay control changes were made to work around automation.
