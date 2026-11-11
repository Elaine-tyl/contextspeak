# Cafe conversation world: first playable prototype

## Approved direction

The learner wants a free, single-player 3D world with school, amusement-park and cafe scenes, NPC conversations and ten difficulty levels. Browser and desktop are both required. The learner approved starting with one cafe mission: buy two drinks within $12, speak to the barista, handle follow-ups, receive feedback/rewards and save progress.

## First slice

Build an explicitly labelled local prototype: one small cafe, one NPC, movement/interaction, one complete order mission, record/stop-to-transcribe, local intent understanding, authored NPC speech, a recoverable typed-answer option, objective-based feedback, and guest progress on the device. Export the same GDScript project to Web and macOS, and provide a Windows export preset. Do not claim the Windows build is tested on this Mac.

Use authored dialogue and deterministic order rules. The local model maps a reply to bounded order/temperature/service/confirmation/question intents; it cannot grant rewards or complete the mission. The price list, two-drink requirement and $12 budget are fixed. Accept equivalent expressions, remember order details, support price/repeat questions and allow corrections. Do not score accent, phonemes or pronunciation from a transcript. Feedback is a practice estimate of task completion.

The starting scene is a functional 3D blockout, not final environment art. Use original simple geometry and colours matching ContextSpeak; replaceable scene nodes allow CC0 models later. Keep scripts focused and readable.

## Architecture and free operation

- One Godot Compatibility-renderer project for browser and desktop; no C# or engine plugins.
- A loopback-only local companion serves the Web export and bounded transcription/intent/speech endpoints. It uses whisper.cpp, the installed local Qwen model and pinned English Phonon; no Gemini calls or application secrets.
- Browser and native clients use the same protocol. Require an origin-checked local session token for inference. Bind to 127.0.0.1, reject unexpected origins/hosts, and do not expose arbitrary files, URLs or commands.
- Microphone audio is bounded, held only for transcription, and temporary files are cleaned up. Playback audio can be cached. Pause/close stops capture/playback.
- Device guest progress is separate from ContextSpeak account records, FSRS and proficiency scores. No invented cloud sync or production security claims.
- Add a website entry from Games without replacing the approved redesign or existing games.

## Deferred work

School/amusement-park maps, ten authored difficulty tiers, account-linked world progress, production hosting, voice packaging for every OS, and calibrated pronunciation assessment follow this validated slice. No RAG, paid service, multiplayer, public model endpoint or paid asset is required.

## Alternatives considered

VoxeLibre offers a ready survival world but imposes voxel art and a separate Lua engine/mod integration. A browser-only Three.js game fits the existing TypeScript stack but creates another desktop packaging path. The selected Godot route shares the actual game project and exports while keeping the current learning website intact.

## Acceptance

Headless mission regressions and companion protocol/error tests pass; a scripted synthetic voice sample is transcribed locally; a worded order can finish the mission without a cloud request; Web export loads and renders, native macOS build starts, guest progress survives restart, and source/build/setup commands are documented. Clearly report any unverified microphone permission/hardware or platform behavior.
