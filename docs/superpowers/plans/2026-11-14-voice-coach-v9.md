# Voice-only speaking coach V9

Goal: make NPC practice voice-only with contextual vocabulary chips, honest practice marks, correction and optional natural alternatives; preserve scenes, saves, gameplay and V7 repetition/independent recall.

Approach: extend existing V7/V7.1 speech and UI adapters, rather than add another conversation engine. Local companion reviews transcript-supported coherence, lexical choice and grammar with explicit evidence; fluency/pronunciation remain unassessed without acoustic evidence. Improvements are optional and never turned into a mandatory grammar correction.

- [x] Regression tests: contextual hints, hidden recall, evidence-supported score validation, alternatives, no text-only acoustic scores, silence rejection and voice capture/playback exclusion.
- [x] Shared coaching presentation and backward-compatible assessment fields; keep persisted learning evidence.
- [x] Integrate Slice adaptive and proactive dialogues; remove Living World typing rehearsal, retain separate internal test hook.
- [x] Automatic correction audio and repeat preparation; capture only after playback ends; no autoplay microphone. Preserve hidden recall and examination feedback deferral.
- [x] Unity native compilation/UI tests, existing suites, isolated Web build and browser check; record real/mocked/incomplete features.

Defaults: existing local speech provider, no paid dependencies or credentials; target difficulty remains Band 3–9 practice, not claimed achievement. Push-to-talk button/shortcut remains deliberate user input. No compulsory new RAG or guessed band score.

## Verification ledger

- RED/GREEN core and schema logs retained in `.local/v9`: missing coach/scoring API, optional live-score schema, then passing behavior.
- Review caught duplicate feedback foldout lookup leaking a legacy feedback surface during recall, plus missing completed proactive exam reports. Replaced duplicate presenters with one shared panel; native checks verify concealed content and completed reports without per-turn assessment.
- Initial isolated native checks passed, but export stopped on an unnamed dirty test scene. Export wrapper now starts from a named saved scene and never saves fixture mutations.
- 625 web tests / 86 files passed, TypeScript passed, V9 17 behavioral checks, existing V7 36 / proactive 50 / Living World 60; recording bridge tests 9.
- Real local Qwen HTTP sample returned a correction for “Can I has a latte?”, an optional natural alternative, transcript practice marks and goal completion. No personal audio used. A first live check exposed omitted score evidence; the live constrained schema now requires current-answer evidence while the response schema accepts older clients.
- Unity native V9 + V7 checks passed in the isolated export. Final publication/browser proof to be recorded when export finishes. No source scene rebuild or commit requested.

- Successful isolated Web release `20261114.124811` published; 14 authoring-scene hashes unchanged. Two concurrent post-snapshot greeting/travel-menu edits in WaiterBrain.cs / SliceInterface.cs retained but not silently included or overwritten. Receipt records these exceptions; V9 runtime changes themselves matched the snapshot.

- Published browser loaded and showed contextual word chips/voice controls with no answer field. Close/F reopening worked. First IAB input produced a transient Chromium UnknownError; cause unresolved, subsequent focused input succeeded. Real microphone full loop remains explicitly unverified.
- Existing Slice behavioral suite also passed (335). No commit or push performed.
