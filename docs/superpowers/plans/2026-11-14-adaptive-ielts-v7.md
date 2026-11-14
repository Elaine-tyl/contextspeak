# Adaptive IELTS V7 Implementation Plan

> Execute inline using superpowers:executing-plans. The user supplied the V7 specification and explicitly requested implementation, without a planning stop.

**Goal:** A working evidence-based five-stage Café → School → Home speaking loop using the existing world and companion.
**Architecture:** Pure C# learning decisions with a small SliceGame adapter, ScriptableObject content and native phone/dialogue UI. Learning evidence is additive to SliceSave; no scene regeneration, reward changes or new API keys.
**Tech stack:** Unity 6 / UI Toolkit / C#, existing STT/TTS and structured local AI review; .NET behavioral regressions.

## Constraints and decisions
- Free life prioritizes conversation; Guided blocks mandatory correction and requires hidden recall; Exam defers feedback until all parts are submitted. No mid-recording actions.
- Repetition never equals mastery. Three independent successes, multiple contexts and delayed transfer are required; failed recall reduces intervals.
- Use real UTC dates (1/3/7/14-day configurable schedule), not accelerated world time.
- Text supports formative coherence/lexical/grammar observations only. No acoustic score or measured fluency without audio evidence; no official band prediction.
- Existing menu/order/quest parsers own world state. Structured AI review can assess language, never issue rewards or location commands.
- Original authored scenarios are curriculum practice, not official IELTS material. Official criterion reference: https://ielts.org/cdn/ielts-guides/ielts-speaking-band-descriptors.pdf
- Keep existing save fields and back up/atomically replace saves. Missing services show MOCK / unassessed; mock turns earn no learning evidence.

## Tasks
- [x] Add behavioral tests for gates, valid alternatives, follow-up relevance, recall vs repeat, scheduling, save/reload, exam interruption, service failures and adaptation.
- [x] Implement learning data, memory, review scheduling, assessment boundary and five-stage state machine; run RED → GREEN.
- [x] Add original ScriptableObject scenario content for Café, School and Home plus mock exam parts and pronunciation reference coaching.
- [x] Integrate resident dialogue, mode selection, journal, review routing, object vocabulary and dashboard into current native UI; preserve transactional dialogue.
- [x] Verify Unity compilation/native UI, existing logic suites and browser delivery if build resources permit. Document implemented, verified, mocked and incomplete separately.

## Review focus
Stale AI replies after closing/change of NPC; save corruption/old saves; content-invalid paraphrases; unsupported pronunciation claims; repeated same-session answers incorrectly producing mastery. Pin each with regression checks.

## Execution evidence / review
- RED → GREEN: missing learning policy; persistent correction gate; vocabulary exposure; off-topic correction targets; whole-utterance clarification; dated cross-context mastery; aligned transfer tasks; requested topic changes. 36 behavioral assertions now pass.
- Review fixes: goal-incompatible correction targets now use an appropriate authored model; all nine fallback models match their prompts; only standalone meta clarification is intercepted; contexts count only on accepted independent dates and require delayed transfer.
- The initial HTTP test run was sandbox-blocked; the unrestricted local-server rerun passed. Missing .NET restore artifacts were restored and legacy/living-world runners passed.
- A broader migration test finds unrelated VillaHome IMGUI; kept out of this feature's changes and documented by name.
- Unity export uses an isolated APFS copy because the active Mac is locked; no authoring scenes are regenerated or copied back. No commit/push requested.
- Final export 20261114.101239 passed native checks and browser delivery; all 14 authoring scene hashes match. The temporary verification project was removed after publication. Full Vitest rerun passed 621 tests; TypeScript passed; live local AI passed five public-sample cases and synthetic reference TTS → STT passed.
- Browser verified Learning, School and Home; Café was verified in the preceding V7 export. Final dashboard contrast was corrected. Existing water spawn/camera occlusion remains documented, as do missing calibrated acoustic assessment and the short mock-exam format.
