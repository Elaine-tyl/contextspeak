# ContextSpeak speaking exploration slice

Build on the existing Unity life world: third-person life exploration, an original timber expedition lodge, a visible speaking itinerary, and live NPC voice practice. The user selected life exploration with NPC speaking tasks and delegated remaining design choices. Preserve the existing world.

The first playable addition is a three-stop speaking trail selected from existing authored scenes using the chosen IELTS-inspired target (3–9), practice need (everyday, dining, shopping or travel), and optional focus vocabulary. The route directs the learner to existing NPCs; it never invents an unreachable location. Completion derives from saved scene lessons at the same target. Failed replies and required corrections retain the current question. Money, inventory and task completion stay deterministic.

Keep the current game/voice engine. Alternatives considered: rebuild as a survival game (premature theme change), or add a separate demo (duplicates working systems). Chosen implementation: extend the existing slice and reuse its tested local STT/TTS/AI review.

Blender supplies an editable original lodge/mission-board source and a lightweight GLB export for Unity. The reproducible script is AI-assisted modelling, not a claim of a separate text-to-3D foundation model. No external 3D credential/service is configured. Existing characters remain editable; this increment does not promise a commercial game's character fidelity.

The user's visual follow-up establishes the player, forest café and pond as the rendering benchmark. Keep Unity 6000.3.28f1 and URP17. Add soft cascaded shadows, restrained contact occlusion, HDR/SMAA, a procedural sky and lightweight authored sky reflection data. One water pass adds world-space ripples, Fresnel reflection and depth-based shore fading; no planar reflection, SSR or volumetric-light claim. Day/night and weather continue to control visibility.

Replace the player and café learning barista with connected, skinned Blender models using verified CC0 MakeHuman graphical data and original clothing/accessories. Keep pinned source checksums and licenses with the source. Preserve authored skeleton rest rotations in the procedural animation driver. Other supporting NPCs remain simplified. These are working reference-led assets, not a faithful reconstruction of every detail or a finished facial/lip-sync animation system. Ordinary game builds consume checked-in GLBs without Blender or network downloads.

The route UI offers a vocabulary input, saved focus, three destinations/progress and a next-stop travel action. A physical mission board opens that same map. Existing maps, café ordering, recording/retry and save keys remain intact. No original LifeAfter art, names, maps or code is included.

IELTS targets follow original exercises informed by public speaking descriptors; scores are practice feedback, not official predictions. Pronunciation cannot be assessed from transcripts. Sources: https://www.lifeafter.game/Season3/ and https://ielts.org/cdn/ielts-guides/ielts-speaking-band-descriptors.pdf.
