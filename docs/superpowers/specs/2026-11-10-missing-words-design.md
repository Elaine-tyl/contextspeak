# Missing words listening game

Add a **Missing words** tile under Listening games. Learners hear a complete
AI-voiced sentence and type the missing words into inline blanks. Submission
reveals a mark, the complete sentence and each incorrect answer alongside its
correction. Use red/green styling plus text, so colour is not the only signal.

Reuse the current account, cached AI audio, server-owned scoring and level-run
flow. Three original sentences per level, ten levels, with Café, Making plans
and Travel themes. Start with one blank; introduce two at level 4, three at
level 7 and four at level 9. Longer sentences provide the harder context.
Passing at 70/100 unlocks only this mode's next level; Retry and Next preserve
their existing behavior. Dashboard game progress includes the new mode.

Approach: extend the existing Games flow. A separate page would duplicate audio,
replay-safe submission and progression; a 3D lesson would obscure this short
listening task. This is an implementation choice under the user's standing
delegation to proceed with routine design decisions.

Contract: `gap-fill` kind; public `gapText: string[]` contains the text before,
between and after blanks. Private `gapAnswers: string[]` remains in the authored
catalog. A submission contains ordered `gapAnswers` with one answer slot per
blank and requires a level session. Grade blanks equally, ignoring case and punctuation with the existing
dictation normalizer. Do not credit visible sentence words or accept a submitted
score. Extra words in a slot count as an incorrect answer and receive a correction,
like spelling mistakes; they do not prevent feedback. Feedback has a score and
ordered `{answer, expected, correct}` words.

Inputs and Check answer require a completed audio playback; empty blanks cannot
submit. Network uncertainty freezes the original answer and retries the same
event, preserving existing idempotency. Feedback survives level reload/review.
Keep the transcript out of the rendered exercise until checking; `audioText`
still reaches the client for AI playback, as in existing listening modes.

No dependencies, database migration, Supabase, 3D changes, commit or push.
Keep existing content IDs and gameplay behavior. English UI and readable code.

Verify: catalog coverage/blank reconstruction; per-blank partial marks and word
normalization; malformed submissions; owned/replay-safe persisted results;
independent level unlocking; browser playback gate, feedback, retry, reload,
next level and narrow layout. Run the full source suite and web build.
