# Voice-only NPC speaking coach — V9

## Use

Speak to an existing NPC, listen to the question and use the optional vocabulary chips. Click the microphone to start/stop, or hold **V** in adaptive Café/School/Home practice, proactive encounters or Life stories. Releasing V submits the captured answer automatically. Permission is still granted through the browser or operating system.

The local companion transcribes and reviews the recording automatically. No answer text field or manual assessment-submit button is required. The old Life stories typing rehearsal and the legacy offline typing field have been removed from normal gameplay. Text-based internal test hooks remain without speaking credit.

In Guided practice, genuine errors lead to a corrected sentence and reference audio. Listen, record a natural equivalent, then answer again without the correction, transcript, hints or feedback visible. An incorrect repeat cannot advance. A correct first answer can continue directly; an optional more natural expression does not turn valid everyday language into a grammar error. A single repetition never establishes mastery. Existing V7 delayed reviews and independent evidence are retained.

## Feedback and marks

- Three to six context words/phrases reflect the scenario and practice difficulty. Relevant weak vocabulary is prioritized; Band 8–9 practice offers fewer hints.
- Coherence, vocabulary and grammar can show **0–100 transcript practice estimates**, supplied by the language reviewer with a schema-constrained exact quote from the current reply. Unsupported/out-of-range marks are dropped. These points are not achieved IELTS bands, official test results or calibrated predictions.
- Fluency stays **Not assessed** without audio timing assessment. Pronunciation stays **Not assessed** unless a configured acoustic assessor supplies genuine audio evidence. STT confidence and vocabulary length are not converted into pronunciation marks.
- Communication-goal completion is shown separately. AI output cannot set money, inventory, rewards or quest state.
- Correction preserves intended meaning. One optional natural/advanced alternative is separate from the correction. Beginner suggestions stay simple.
- Feedback expands within a compact bottom panel; the 3D scene stays visible. Completed exam reports render after the assessment, while active exams conceal feedback and hints.

## Reused implementation

`Learning/Core/VoiceCoach.cs` supplies hints, score-evidence checks and a basic audio signal gate. `VoiceCoachPanel` is the shared Unity UI Toolkit presenter; `VoicePushToTalk` supplies optional V input. Slice adaptive dialogue, proactive encounters and Living World stories reuse the existing V7 practice manager, recorder, local companion, TTS and save structures. The Living World save adds an optional learner JSON field without replacing story facts.

`SliceVoice` tracks pending generation as well as active playback. Capture is blocked during NPC TTS or recording replay, and playback controls are blocked during capture. The browser keeps echo cancellation and noise suppression. A small input meter reflects real microphone activity; silence and very weak steady signals are rejected before transcription. This gate does not certify that arbitrary noise is human speech; the subsequent transcription and semantic checks must still succeed.

`packages/world/slice.ts` validates the optional language marks and instructs the local reviewer to distinguish corrections from optional improvements. The companion holds service access; no API key is added to the Unity build. This upgrade does not add RAG or a new paid provider.

## Scope and verification

The reference UI is adapted to existing ContextSpeak scenes and English UI labels. No scene or NPC art is rebuilt. Existing menus, transaction checks, quests, character selection and saves remain owned by their original systems.

Automated checks and exported-build status are recorded below when finished. Real recordings must be checked with a human speaker before treating microphone UX or assessment quality as pilot-verified. Native input-device selection, automatic voice activity detection, a new calibrated acoustic pronunciation service, a full Hotel/Workplace content pack and a new NPC portrait asset are not implemented by this change. Use the system input selector; click/hold is deliberate microphone activation. The existing real-time local speech/AI service is used when available and failures retain progress without fabricated marks.

### Executed checks

- 625 web regressions across 86 files and TypeScript type checking passed.
- V9 pure behavior: 17; existing V7: 36; proactive: 50; Living World: 60. Native recording/bridge/pointer JavaScript checks: 9.
- Unity native checks passed for hint chips, no answer TextField, quote-supported score deserialization, optional alternatives, recall/exam concealment, completed exam reports and saved feedback. Existing native V7 checks include both player characters.
- A real local Qwen request using only the public sample “Can I has a latte?” returned “Can I have a latte?”, a natural alternative and transcript practice marks with the current-answer quote. This tests the language service, not microphone capture or pronunciation accuracy.
- Independent read-only review found two presentation regressions; both were fixed and covered by native checks. Test fixtures do not overwrite authored scenes.

A complete human-microphone Café order/payment/collection run, permission-denial recovery, varied-accent speech and macOS V9 runtime remain manual verification tasks. The downloadable macOS archive is retained from the previous release, not rebuilt here.

### Browser export

Release `20261114.124811` was built successfully and published at `http://127.0.0.1:4320/unity/?world=slice`. All 14 saved authoring-scene hashes matched before publication. Updated V9 runtime sources match the verified isolated build.

Two later edits appeared during export in `Slice/Core/WaiterBrain.cs` and `Slice/Runtime/SliceInterface.cs` (greeting/short-answer presentation and world-travel entry). They remain intact in the workspace and are not included in this frozen export. No later user edits were overwritten. The release receipt and build log are under `.local/v9/`.

Browser verification: the published version loaded, F opened the Home adaptive NPC, contextual vocabulary chips (dinner, cooking, family, chores) rendered, no answer field was present, and Close → F reopened it. Proof: `/private/tmp/contextspeak-v9-voice-panel.png`. One transient Chromium `UnknownError` occurred on the first input; after inspecting the dialog/log and focusing the canvas the dialogue worked. It was not reproduced in the subsequent check; its underlying browser cause is unresolved. This is not counted as a complete microphone playthrough.
