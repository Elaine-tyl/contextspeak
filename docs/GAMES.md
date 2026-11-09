# Game practice

Open **Game practice** in the library sidebar, or `/games`. Sign in with the existing Google account. Games are English-first and do not require a saved vocabulary item.

## Games and difficulty

There are 27 original versioned challenges across three levels:

- Listening: story, drama, literary passage and novel-excerpt dictation; hear a word/clue and choose an original vector picture; listen to a passage and answer a multiple-choice comprehension question.
- Speaking: explain a riddle, reason through a puzzle, or complete a timed-length speaking task against Echo, a simulated AI training opponent. Every recording is limited to 60 seconds.

PK is not a real player or a live generated opponent response. Echo's visible fixed training targets are 65, 75 and 85. Your assessed game score determines win/draw/loss. A win below 70 still does not count as a progression pass.

Listening and speaking unlock independently. Three **different** challenges passed at 70 or higher unlock the next level. Repeating a passed challenge does not count twice. Level 3 also requires Level 2 to be unlocked; skipping ahead through API calls is rejected. These are game difficulty labels, not CEFR levels or proof of mastery. No game changes FSRS.

The catalog uses short original English texts, including fictional novel excerpts, rather than complete novels or third-party drama scripts. Content is fixed for reproducible practice; new revisions need new IDs so old scores retain their meaning. Higher listening levels have longer/more nuanced passages and descriptive picture clues. Speaking levels increase reasoning and response complexity.

## Flow and feedback

Play the cached Gemini challenge audio and finish playback. Dictation: type what you hear and check your answer. Picture/comprehension: select one answer and check it. Transcript/corrections/solutions appear after the result is confirmed; wrong/missing phrases use the existing red highlights. Picture choices have accessible descriptions.

Speaking: read/listen to the prompt, record and stop. The completed recording automatically goes to Gemini; there is no agreement or feedback button. Replay is available locally. Feedback includes the existing estimated rubric, transcript, grounded corrections and reference IPA guidance. The game mark averages the overall and task-response estimates so relevance matters. Silence/unscorable recordings receive no mark and do not unlock or win. Quota/provider errors show an explicit retry; retry uses the same event and recording rather than generating another attempt. Leaving an unconfirmed round cannot recall audio already sent; the result may still save.

The full catalog includes audio text for playback, so this is a learning game, not an anti-cheat examination. Listening scores use server-owned text/choice answers, never a client-supplied score. Successful playback is client-reported.

## Storage and operation

Run `pnpm db:generate` and deploy the new Prisma migration before serving the API. The local `pnpm dev` launcher deploys migrations automatically. `GameAttempt` stores user/event IDs, immutable round ID, score, bounded submitted text/choice/duration/audio digest, feedback and timestamp. Raw/base64 recordings and Blob URLs are never stored. User deletion cascades to game results; pending AI work rechecks the user before saving. Results are account-specific.

`GET /v1/games` returns the public catalog and owned progress; `POST /v1/games/attempts` validates a bounded answer and returns a durable receipt. Matching in-flight event retries coalesce, completed retries return the saved result, and changed payloads cannot reuse an event. Speaking uploads share the 2 MB decoded/3 MB JSON route limit and signature validation with ordinary practice. Other routes retain the 100 KB JSON limit. Process-local game guards allow two assessment pipelines, six new spoken game requests per user/minute and twelve per process/minute. Existing Gemini project quota is still shared across translation, speech and assessments; no paid fallback is enabled.

Only the server-side existing Gemini key is used. Challenge audio uses the existing per-user speech cache; cached audio remains usable when new generation is unavailable. Recorded audio, prompt and solution criteria go to Google for assessment under the project's existing Gemini setup; unpaid-service data may be used for product improvement. The app stores feedback, not raw recordings. Mode/page changes and account expiry cancel local media and stop microphone tracks.

## Verification

Fixtures use isolated PostgreSQL databases, simulated sign-in, deterministic audio/assessment and generated browser recordings. No personal microphone upload or new API credential is needed for tests. Tests cover immutable content, distinct/skill-separated progression, locked-level rejection, forged scores, answer ownership, idempotent/concurrent submissions, input limits, quota failure, silence and no FSRS changes; browser checks exercise real local API progression, picture selection, dictation, comprehension, automatic speaking, fixed PK results, retry and session-expiry cleanup.
