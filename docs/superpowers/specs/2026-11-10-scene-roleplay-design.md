# Scene and role practice

Extend the existing local Godot game with school, farm, market and restaurant practice. Keep the cafe counter and friend conversation intact. Each new scene has two learner roles and three authored communication goals. The builder selects the exact roles, questions and visual composition within the requested cute campus style.

## Approach

Reuse the current Godot, Whisper, Qwen and Phonon runtime. A shared JSON catalog defines questions, goals, examples and permitted visual actions. Local AI checks meaning and wording; it cannot choose game actions or award progress. A separate client state machine controls correction and progression. This keeps the eight short lessons testable. An unconstrained AI sandbox would make repeated questions and impossible tasks harder to prevent; a new engine would discard the working microphone and desktop exports.

## Flow

Choose scene and role, then enter the scene and speak to its NPC. Recording stops into automatic local transcription and assessment; typing remains available. A suitable answer with no correction advances one goal. An unsuitable answer, or a suitable answer needing a correction, shows a model sentence and a short reason. The learner must repeat that sentence before advancing. Normalize case, punctuation, common contractions and spoken numbers for this readback; do not claim this text check measures pronunciation. Assisted completions are labelled separately.

Approved transitions alone trigger authored props, action cues and small animations: books, timetables, watering, harvesting, shopping baskets, payments, meals and tables. Scene/role switches cancel recording, voice and in-flight assessment. Each role's local guest progress, including pending corrections, is retained separately from cafe saves and test previews. These prototype results do not enter the account Dashboard.

## Assets and scope

Use the existing verified CC0 KayKit, Kenney and Quaternius files, plus independently authored procedural scenery. Search primary creator pages and GitHub for additional usable free assets. Record actual provenance; paid extras are not imported. Deliver four compact playable environments, not farming simulation, multiplayer, free-roaming campaigns or certified speech assessment. UI and code remain English and functions readable.

## Verification

Test every scene/role catalog, correction readback, repeat failure, duplicate completion, malformed restores, action idempotence, switching and stale requests. Run existing cafe regressions, browser recording/transcription fixtures and a real local AI smoke check; export Web and macOS and run native headless checks. Report GUI/microphone coverage precisely.
