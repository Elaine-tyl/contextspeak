# Unity cafe vertical slice

The user's engine choice is Unity for 3D and Godot for mini-games. Godot Web
arcade is implemented; Unity is now installed and a batch-created probe exited0.
Pin the available Editor6000.6.8f1, Web support and macOS support. It is the current
6.6 release, not described as LTS. Implement a verified cafe slice before replacing
the old world; existing other scenes remain available in the old client.

Create games/unity-cafe with C#, URP17.6.0 and Unity glTFast6.20.0 from the official
Unity registry, exact package lock and Editor version. Use the already verified
CC0 KayKit/Quaternius/mini-character art in games/cafe/assets; build preparation
copies only needed files after provenance verification into ignored generated
Unity art (never download replacement packs or copy game code). Native procedural
architecture unifies scale, cream/wood/forest colors, windows, lighting, chairs,
props, stylized youthful NPCs and smooth third-person walking/camera.

Gameplay: start at entrance, WASD/arrows to walk, E nearby Maya to order two drinks
within $12, temperature and service follow-ups, explicit confirmation, visible
cups reward, walk to Alex and sit, three reviewed conversational replies, corrections
retry without advancing, completion. Keep local progress in a separate validated
Unity save; do not silently replace the existing Godot progress/account data.
Testing stage picker can jump between counter and friend with a clear practice
label; such jumps never fabricate completed account scores. Free roaming remains
available while dialogue is closed. Preserve the functioning old world link.

Reuse the existing local guest companion4320: session bearer, /interpret for
unrecognized short order replies, /transcribe for bounded PCM WAV, /speech for
local AI NPC audio, /conversation with exact prior answer history. Do not widen
host/origin trust, add public AI endpoints or expose account/extension credentials.
Deterministic local order parsing/validation protects budget, quantities, negation
and final confirmation. AI cannot turn an ambiguous confirmation into success.

Native Unity mic captures at most20seconds and sends valid16-bit mono WAV. Web
uses the existing owned microphone/worklet JS via a small Unity plugin/template;
late permissions/callbacks and closed/changed dialogue never upload stale audio.
Stop recording submits automatically. NPC speech plays automatically on opening
or advancing; Hear retries errors. No device speech fallback, no cloud key added.
Use generation/cancellation guards for every delayed response, stop audio and mic
on close/focus loss/unload; allow retry after companion failure without advancing.

Runtime Unity UI: visible brand, objective, budget/order, nearby NPC prompt, dialogue
with NPC name/text, input, record/stop, send and Hear. Warm readable cream/green
panels; keyboard input cannot move the player while typing. Canvas support needs
accessibility: the Web template may expose a synchronized semantic dialogue form
for screen readers, but Unity still owns the world and state, not a Godot iframe.

Exports: reproducible Web to .local/world-builds/web/unity (served by existing
companion, same-origin), macOS to .local/unity-builds. Existing builds untouched.
Build/test scripts prepare art, check versions, set URP, create/save scene and
compile both targets; generated caches/builds ignored. Never claim a build works
without launching the actual export. No push/commit of this migration requested.

Tests: failing-then-passing C# editor regression checks for ordering, exact-budget,
negation/confirmation, restore corruption, friend retry/history/complete, stage
practice isolation and scene colliders/waypoints. Actual WebGL journeys exercise
walking/E and typed ordering/friend replies via fixture companion, microphone
permission cancellation and actual recording with known audio where possible.
Test public real local AI voice/transcription separately, never invent a user mark.
Launch macOS export to check rendering/input/mic and report any platform check not
completed. Existing world/Godot/website tests must remain green.
