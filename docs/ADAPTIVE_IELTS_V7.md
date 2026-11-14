# Adaptive IELTS learning — V7

## Use it

In **Forest Café**, open the phone (M → Home), then **Learning**. The same page is available from Settings → Learning modes & progress.

- **Free life:** corrections are optional and follow-up conversation keeps running. Asking to talk about travel, music, books, food, work, study, family, sports or films changes the topic. These are curated conversation branches, not an unrestricted chatbot.
- **Guided:** answer independently. If language needs correction, read the focused feedback, choose **Practise correction**, then record a natural corrected response. A successful repeat opens **independent recall** with the correction, transcript, history and model replay hidden. A successful recall advances. Closing/reloading preserves a pending guided gate.
- **Mock exam:** choose **Exam centre · School**. Record and stop each answer when finished. Three practice parts defer all corrections until the final response. Capture supports up to two minutes per answer. This is a short three-part formative rehearsal, not a full timed 11–14-minute IELTS examination or an official result.

Use **Café · Maya**, **School · Ms Taylor**, and **Home · Alex** to visit existing NPCs. These are the existing locations in the ForestSlice district; this update does not create/rebuild separate world scenes. Existing character selection, shop transactions, quests, rewards and saved inventory remain under their existing systems. Other districts retain their previous dialogue implementations.

**Review due expressions** chooses a scheduled item and a different location. **Daily spoken journal** starts a personal reflection at Home. Inspecting a recognized book/cup/table/counter/chair/menu/kitchen/timetable scenery object adds vocabulary exposure, not mastery.

## Learning and evidence policy

The additive `SliceSave.Learner` field stores expression/vocabulary records, independent and repeated attempts, scenario history, wording error observations, provider-supported pronunciation observations, due dates, pending guided sessions and completed mock feedback. Existing save fields and version remain compatible. Writes use a temporary file and atomic replacement with a previous `.bak`; unreadable primary saves try the backup. Browser storage still uses the existing filesystem-sync callback. Last-recording replay stays in memory and is discarded when leaving the dialogue; no recording is written into the learner JSON.

All six item states are represented: NEW, LEARNING, REVIEW_DUE, PRACTISING, MASTERED, NEEDS_REVIEW. Due state is also calculated for display without requiring a background process.

- Default review intervals: **1, 3, 7, 14 UTC days**, configurable per `LearningScenarioAsset`. Accelerated game time cannot make a delayed review count early.
- Failed independent retrieval resets the interval and marks NEEDS_REVIEW. A repeat counts only as repetition practice.
- At most one independent success per item per UTC date contributes to retention. Mastery requires at least three successful dates, at least two contexts, a delayed successful transfer between contexts and at least three elapsed days. Same-day location hopping cannot manufacture delayed transfer.
- Vocabulary is credited only when it occurs in an assessed spoken answer. Viewing objects, typed input, unavailable services, and model-repetition alone cannot establish mastery.
- A bounded recent independent history recommends one target-band step easier/harder within 3–9. This is task adaptation, not an inferred official proficiency score. There is no official IELTS vocabulary list in this implementation.

## Assessment boundary

Feedback uses the four headings from the [official IELTS Speaking descriptors](https://ielts.org/cdn/ielts-guides/ielts-speaking-band-descriptors.pdf): Fluency and Coherence, Lexical Resource, Grammatical Range and Accuracy, and Pronunciation. The content is original practice material; it does not reproduce exam questions or band-descriptor text.

Current live companion feedback is **transcript-based formative review**, supplemented by limited deterministic grammar rules. It can assess relevance and language choices. It cannot measure actual pacing, hesitation timing, stress, intonation, phoneme accuracy or connected speech from text. It does not issue numeric predicted IELTS bands. Gameplay communication success is stated separately.

`IPronunciationAssessor` remains the existing acoustic-provider boundary. The default provider is unavailable, so pronunciation is explicitly **not assessed**. A finite 0–100 acoustic value with an identified configured provider may be displayed as that provider's practice score, never as an IELTS band. The reference coaching text includes stress/intonation guidance and /kəd jə/ as an example; it is teaching material, not a diagnosis of the recording or a penalty for an understandable accent.

Existing local STT/TTS/review services are reused. `SliceReviewRequest` adds bounded conversation history and a same-meaning reference for corrected repetition. The server treats these as untrusted data. Schema output contains no world commands, rewards, wallet values or scene transitions. C# selects follow-up intents, advances the learning policy and keeps transactional gameplay under the original parsers. Epoch checks discard stale responses after closing/changing NPCs.

The local model uses a separate, focused semantic goal check after generating language feedback. Its evidence must quote the current answer, rather than conversation history; unsupported evidence is unassessed. A concrete drink-choice guard prevents a changed order from passing a repetition merely because the model calls it equivalent. This adds a second model request for goal-bearing answers. It reduces the observed small-model failures but is not a calibrated assessment guarantee.

If speech/AI is missing or fails, the UI says **MOCK / unassessed**, preserves the task and awards no evidence. No silent simulated pronunciation result or fabricated success is substituted.

## Modules and content

Pure C# policies live in `games/unity/Assets/_Game/Learning/Core/`:

| Module | Responsibility |
| --- | --- |
| AdaptiveDialogueManager | Answer-linked examples, clarification, comparisons, topic changes and bounded difficulty recommendation |
| IELTSPracticeManager | Guided/free five-stage policy and pending-session persistence |
| SpeakingAssessmentService | Evidence validation and formative criterion feedback |
| CorrectiveFeedbackManager | One focused correction/next-action message |
| RepeatVerificationManager | Spoken, relevant, grammatical semantic-alternative gate |
| IndependentRecallManager | Separate hidden-cue recall gate |
| LearningMemoryService | Exposure and assessed-evidence records |
| SpacedReviewScheduler | Real-date intervals and mastery requirements |
| ScenarioTransferManager | Aligned review prompt/goal/model in another location |
| IELTSMockExamManager | Whole-response submission and deferred practice feedback |
| LearningDashboardController | History, due/mastered items, recurring errors, daily evidence trend and exam feedback |

`LearningScenarioAsset` is an editable ScriptableObject in `Runtime/`. Nine checked-in assets in `Assets/Resources/LearningV7/` cover Café, School and Home at target ranges 3–4, 5–6 and 7–9. `SliceGame.Learning.cs` and `SliceInterface.Learning.cs` attach the policies to the existing game and UI; no NPC prefab or scene migration is required.

## Verification

```sh
.local/living-world/dotnet/dotnet run --project tools/unity/adaptive-checks
.local/living-world/dotnet/dotnet run --project tools/unity/slice-checks
.local/living-world/dotnet/dotnet run --project tools/unity/legacy-checks
.local/living-world/dotnet/dotnet run --project tools/unity/living-world-checks
node --test tools/unity/test_v7_recording.mjs tools/unity/test_native_ui_bridge.mjs tools/unity/test_pointer_lock.mjs
pnpm test
pnpm typecheck
```

Unity: **ContextSpeak → Checks → Adaptive IELTS V7**. It checks actual imported assets, JsonUtility compatibility, both character choices, hidden recall controls, deferred exam UI and the phone's three-location route without issuing gameplay commands.

Verified at implementation time: 36 V7 behavioral checks; 335 existing slice checks; legacy dialogue/modal runner; 60 living-world checks; eight JS recording/bridge/pointer checks; **621 Vitest tests in 85 files**; TypeScript compilation. The native Unity V7 fixture also passed during the isolated final export (see `.local/v7/final-build.log`). An earlier concurrent test/build run exhausted disk space; after deleting the disposable verification copy, the complete application suite passed on rerun.

Published browser build: **20261114.101239**. All 14 saved scene hashes were verified unchanged against the authoring project before publication. Browser checks confirmed character creation, the Learning phone app, readable dashboard text and the School/Home dialogue entries. The preceding V7 export also verified the Café entry. No original scene was regenerated or replaced.

Five real local AI checks passed: Café grammar correction, School clarification, Home request, accepting an equivalent corrected expression and rejecting a changed drink choice. The optional public-text smoke runner is `node tools/unity/check_v7_companion.mjs`; it requires the running local companion/model and is separate from deterministic CI. Evidence is in `.local/v7/live-companion.json`. A public synthetic TTS → STT round trip also returned the expected sentence (`.local/v7/live-audio.json`); this is not a real human microphone or acoustic-validation test.

One broader existing UI-migration test currently fails: `tools.unity.test_legacy_toolkit.LegacyToolkitMigration.test_no_player_surface_relies_on_imgui_events` finds `Assets/ContextSpeak/Runtime/Villa/VillaHome.cs`. That untracked, unrelated villa UI uses IMGUI and was not rewritten by this learning change. The other nine tests in that three-module Python run passed.

## Mocked, incomplete and limits

- Automated evidence/clock tests use synthetic utterances and dates; they do not establish real learner mastery or validate speech recognition accuracy.
- No calibrated acoustic assessor has been configured or empirically evaluated. Pronunciation is not scored by default. Transcript feedback is not a substitute for trained IELTS examiners.
- Difficulty decisions use a small bounded rule, and follow-ups use authored intents/anchors. Rich unrestricted NPC dialogue, a full timed mock examination, and longitudinal calibration are future work.
- Existing life events and multi-NPC locations are reused. No new relationship economy, world generation, or cross-device/cloud learning sync was added.
- This export does not update the downloadable macOS executable. Real human microphone, macOS playback and multi-day retention require pilot validation; automated fixtures do not claim them.
- Browser exploration exposed existing world presentation issues: the fresh character initially appeared partly in water, and the camera can be occluded near the café. Learning navigation reached the classroom and Home. World grounding/camera repair is outside this non-rebuild learning update and remains a separate pilot issue.
