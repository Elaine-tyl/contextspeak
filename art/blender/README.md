# ContextSpeak editable Blender art

These are editable reference-led assets for the existing Unity life world.
They borrow broad exploration/mission-board conventions; no LifeAfter models, textures,
characters, map layouts or source code are included.

- `SpeakingLodge.blend`: timber expedition lodge, open front, counter, planted entry and mission board.
- `TrailGuide.blend`: jacket, cargo trousers, boots and backpack; separate head/arm/leg pivots.
- `ReferencePlayer.blend`: connected anatomical skin, cream hoodie, striped tee, cuffed jeans,
  canvas shoes and backpack; a 53-bone skinned mesh.
- `ReferenceBarista.blend`: cream shirt, rolled cuffs and brown bib apron using the same compatible rig.
- Reference characters use CC0 MakeHuman graphical data plus project-authored clothing and hair.
  Exact provenance, checksums and licenses: [source record](../sources/makehuman/README.md).
- Unity imports the committed GLB exports in `games/unity/Assets/_Game/Art/Blender`.
  Building the game does not require Blender. The normal Unity-generated art folder remains ignored.

## Rebuild

Verified with Blender **5.3.0** and Unity **6000.3.28f1**.

```sh
pnpm art:blender
pnpm art:test
pnpm unity:test
pnpm unity:build:web
pnpm unity:build:mac
pnpm world:dev
```

`art:blender` locates Blender on PATH or in the standard macOS application location.
Set `BLENDER_BINARY` to an alternative executable if needed. No Python package installation,
API key or online asset download is needed. The scripts regenerate all four `.blend` sources
and GLB exports. Lodge/guide renders are in `.local/art-previews`; player/barista review renders
and geometry reports are in `.local/art-candidates`. Character source checksums and exported
skin weights/textures are verified before copying candidates into Unity.

Open a `.blend` file to edit the meshes, materials, light rig and camera. Re-export GLB after
manual editing; rerunning the generator deliberately replaces generated models. Duplicate
customised files first. The guide uses an object hierarchy; the player/barista use skinned
meshes. `CozyAvatar` preserves each joint's authored bind axes. Motion is still a procedural
fallback, without a finished walk cycle, facial expression, IK or lip-sync system.

## Speaking trail

Open `http://127.0.0.1:4320/unity/?world=slice` after building/starting the companion.
Use **Esc** for IELTS-inspired targets **3–9**, then **M** for the route planner.

Enter optional comma-separated vocabulary, e.g. `ticket, queue`, and choose **Plan route**.
Choose an everyday, dining, shopping or travel practice need to change the route defaults.
The planner selects three real scenes/NPCs from the authored curriculum, prioritising
vocabulary matches and changing defaults by target. The map shows each stop's completion;
**Next stop** travels to the next unfinished conversation. Walk up and use **F** to speak.
**Visit the speaking lodge**, then **E**, opens the physical mission board's same planner.
The highlighted mini-map target follows the next route NPC.

NPC questions, goals and vocabulary depend on the selected target. Live local AI checks the
reply against its current question; important corrections use the existing spoken retry flow.
Typed rehearsals do not bypass a spoken task while voice is connected. Settings still offer
Practice/Guided/Challenge, and assisted model answers remain labelled as assisted practice.
The route retains existing lessons and only counts completions at the selected target.

This is AI-assisted procedural Blender modelling plus the existing local AI conversation
review. A separate text-to-3D foundation-model service is not configured. Scenes are selected
and composed from available authored content, not arbitrary geometry fabricated live by an LLM.
IELTS targets/feedback are original practice material, not certified scores; pronunciation
requires acoustic evidence from a dedicated assessor and is not guessed from the transcript.

## Rendering benchmark

The desktop forest slice uses URP17, ACES, HDR, SMAA, soft cascaded shadows, restrained
half-resolution contact occlusion and an authored sky/ground reflection approximation.
The browser automatically uses 85% render scale, two 1024px shadow cascades and FXAA with
contact occlusion disabled. Leaving the scene restores its previous rendering settings.
The pond is a single
animated transparent pass with world-space ripples, Fresnel reflection and depth-based shore
fading. It does not mirror the forest, use SSR, or compute volumetric light. Existing day/night,
rain, wildlife, café transactions and navigation remain active.

The player/barista are a first step toward natural proportions, not a finished commercial art
set. Supporting characters and several district props remain simplified. Fine cloth texture
in Blender previews is not baked into the game export; gameplay screenshots are the actual
visual acceptance evidence. Stable 60 FPS is a performance target, not yet a measured guarantee.
