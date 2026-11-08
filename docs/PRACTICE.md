# Listening and speaking practice

Open **Listening & speaking** in the library sidebar or a vocabulary detail page's **Practice listening and speaking** link. Search/filter up to 50 recent matches; the list links to the latest successful encounter, even after a failed retry. Search, translation filters and scenario selection are kept in the URL. Returning from vocabulary/practice restores that library view and scroll position; paginated history reloads the saved pages before restoring scroll, with manual recovery after a failed request. Practice uses its first saved bilingual example, or the selected expression when no example exists. Text must be at most 600 characters.

## AI audio

All generated practice, word and example audio uses the existing [cached Gemini AI voice](AI_SPEECH.md). Keep `SPEECH_PROVIDER=gemini` and the server-side key in ignored `.env.local`. Google receives the played text and language; microphone recordings are never sent. Playback starts only on a click, and another selection, account change, hidden tab or navigation cancels it. There is no device-voice fallback.

Preparing and playing audio have accessible status messages. Audio uses the shared per-user cache and provider quotas. Cached audio can still play when new generation reaches its limit. Failed requests show an error and allow retry; they cannot unlock submission. A known supported source language is required. Up to 20 completed plays are allowed per attempt.

## Listening: dictation and marks

1. Choose **Play audio**, then type exactly what you hear.
2. Choose **Submit answer** after at least one complete playback.
3. Read your mark out of 100, the transcript, and missing/extra/replaced-word feedback. Use **Replay audio**, **Try again** or **Choose next expression** beside the result; returning to the practice list retains its filters.

The transcript and corrections appear in the card only after the server confirms submission. Failed saves keep the typed answer and event ID locked for a safe retry; **Start a new attempt instead** abandons the pending result, which may already be saved. The server stores its own task snapshot and feedback. A retry returns the original result and cannot change the answer or scenario. Old listening self-check receipts remain readable.

Scoring is deterministic word error rate: subtract substitutions, insertions and deletions from the expected word count, convert to a percentage, and clamp to 0–100. Only zero word errors receive 100. Unicode compatibility, case, apostrophes and punctuation are normalized; language-aware word segmentation supports languages without spaces. Accents and lexical differences matter; numbers written as digits versus words are not treated as equivalent. The feedback lists normalized words. No grading LLM or additional model quota is used.

These are guided practice marks, not a proctored test or broad comprehension assessment. Playback counters are client-reported. Task text is already supplied for playback, so UI withholding is not an anti-cheating guarantee. Scores do not update FSRS or inferred mastery.

## Speaking and recordings

Choose **Speaking**, listen to the example/partner as needed, then record an original response using the expression. Stop and replay the recording to the end before saving **Comfortable** or **Needs practice**. Showing the saved example or partner turn marks text exposure. Speaking remains a self-check with no transcription, pronunciation score or automated intelligibility assessment.

Recording starts only after a click and microphone permission over HTTPS or loopback. Capture stops at 60 seconds, tab hiding/navigation, expiry or account changes. A 10 MB buffer limit rejects oversized recordings. Permission denial, empty audio and recording errors allow retry; late permission grants after leaving immediately release their tracks. Recordings stay in browser memory and are discarded on mode/scenario changes or navigation. Replay uses the learner's original recording. No recorded audio is uploaded or saved in the database.

Saved attempts contain the written answer, mode, optional scenario, mark or self-check, playback/reveal metadata, recording duration and server task snapshot. Recent practice shows the last ten encounter results. Account checks run every three seconds and at known expiry; remote revocation is detected at the next successful check. Lookup/vocabulary/account deletion cascades to attempts.

## Everyday role-plays

Choose **Introductions**, **Making plans** or **Asking for help**, then saved English vocabulary with a known English source language. Translation-language settings remain unchanged. Each original fixed partner turn has a response goal: transcribe what the partner says, then switch to speaking and reply using the chosen expression. If that expression does not naturally fit, choose another word. These are curated role-plays, not dynamically generated or independently assessed transfer tasks.

Scenario switches update the URL, clear local answers/recordings and restart listening. Refresh and Back/Forward retain the requested scenario. An unconfirmed save locks the selector until a retry succeeds or the attempt is explicitly abandoned. Dynamic scenario generation, speech recognition and independently validated skill assessment remain future work.

## Verification

No new migration is needed for dictation: scores and feedback use the existing `PracticeAttempt.task` JSON snapshot. Deploy the existing practice migration before serving the API. Tests use isolated databases, simulated sign-in and deterministic audio; no Gemini credentials or live quota are needed. Regression coverage includes word alignment/normalization, owned snapshots, idempotency, no FSRS changes, AI-only playback, quota recovery, cancellation, submit-before-reveal and local microphone/recording cleanup. A native Chromium test verifies decodable recordings and actual local API grading. Live voice quality and microphone use still need a learner-device smoke check.
