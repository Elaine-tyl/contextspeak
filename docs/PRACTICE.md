# Listening and speaking practice

Open **Listening & speaking** in the library sidebar or a vocabulary detail page's **Practice listening and speaking** link. Search/filter up to 50 recent matches; the list links to the latest successful encounter, even after a failed retry. Search, translation filters and scenario selection are kept in the URL. Returning from vocabulary/practice restores that library view and scroll position; paginated history reloads the saved pages before restoring scroll, with manual recovery after a failed request. Practice uses its first saved bilingual example, or the selected expression when no example exists. Text must be at most 600 characters.

## AI audio

Generated practice, word and example audio uses the [shared saved voice service](AI_SPEECH.md): local Ollama/Phonon for English, with explicitly configured Gemini fallback for other languages. Keep local voice and Gemini recording-assessment settings separate in ignored `.env.local`. Only configured cloud playback sends played text/language to Google; completed English microphone recordings upload automatically for speaking feedback as described below. Playback starts only on a click, and another selection, account change, hidden tab or navigation cancels it. There is no device-voice fallback.

Preparing and playing audio have accessible status messages. Audio uses the shared per-user cache and provider quotas. Cached audio can still play when new generation reaches its limit. Failed requests show an error and allow retry; they cannot unlock submission. A known supported source language is required. Up to 20 completed plays are allowed per attempt.

## Listening: dictation and marks

1. Choose **Play audio**, then type exactly what you hear.
2. Choose **Submit answer** after at least one complete playback.
3. Read your mark out of 100, the transcript, and the checked answer. Red, text-labelled highlights identify replaced, missing and extra words in sequence, including repeated words. Use **Replay audio**, **Try again** or **Choose next expression** beside the result; returning to the practice list retains its filters.

The transcript and corrections appear in the card only after the server confirms submission. Failed saves keep the typed answer and event ID locked for a safe retry; **Start a new attempt instead** abandons the pending result, which may already be saved. The server stores its own task snapshot and feedback. A retry returns the original result and cannot change the answer or scenario. Old listening self-check receipts remain readable.

Scoring is deterministic word error rate: subtract substitutions, insertions and deletions from the expected word count, convert to a percentage, and clamp to 0–100. Only zero word errors receive 100. Unicode compatibility, case, apostrophes and punctuation are normalized; language-aware word segmentation supports languages without spaces. Accents and lexical differences matter; numbers written as digits versus words are not treated as equivalent. The feedback lists normalized words. No grading LLM or additional model quota is used.

These are guided practice marks, not a proctored test or broad comprehension assessment. Playback counters are client-reported. Task text is already supplied for playback, so UI withholding is not an anti-cheating guarantee. Scores do not update FSRS or inferred mastery.

## Speaking and recordings

Choose **Speaking**, listen to the example/partner as needed, then record an original response using the expression. Feedback is automatic after recording; replay the recording to the end before saving **Comfortable** or **Needs practice**. Showing the saved example or partner turn marks text exposure. Local self-check remains available after replay; it does not start a second upload. Automatic AI assessment adds an estimated rubric score and transcript; it is not a validated pronunciation or intelligibility test.

Recording starts only after a click and microphone permission over HTTPS or loopback. Capture stops at 60 seconds, tab hiding/navigation, expiry or account changes. A 10 MB buffer limit rejects oversized recordings. Permission denial, empty audio and recording errors allow retry; late permission grants after leaving immediately release their tracks. Recordings stay in browser memory and are discarded on mode/scenario changes or navigation. Replay uses the learner's original recording. Self-checks do not upload recordings. Completed English recordings upload automatically for AI feedback; ContextSpeak never saves the recording in its database or a file.

### Automatic AI speaking feedback

Speaking shows a question based on the saved expression; choose **Play audio** to hear it. Record your answer and stop: the completed English recording is automatically submitted for feedback, with no agreement or feedback button. Replay is optional. A compact notice appears before recording. If assessment fails, **Retry AI feedback** explicitly retries the same recording/event; quota errors never trigger an automatic retry loop. Legacy clients can still submit manually. Local self-checks require replay and do not start an additional audio upload.

Gemini receives the submitted audio plus the saved expression and exercise prompt. ContextSpeak stores the transcript, five rubric estimates, feedback, suggested response, model/rubric version, submission metadata and an audio digest; raw/base64 audio is held only in request memory and is not persisted. Google processes input under the [Gemini API terms](https://ai.google.dev/gemini-api/terms); unpaid-service data may be used for product improvement. Do not assume provider-side deletion or zero retention.

When Gemini identifies grammar or vocabulary changes, exact unique phrases in the transcript are highlighted in red with suggested replacements and reasons. Ambiguous, overlapping or invented spans are discarded. A different valid reply is not wrong because it differs from the suggested response. Check speech-recognition accuracy against the recording; highlights do not diagnose mispronunciation.

Pronunciation guidance includes up to three grounded words, a target/reference IPA form, stress explanation and articulation tip. IPA is AI-generated guidance, not verified dictionary data or a measurement of the learner’s exact phonemes. Unscorable audio has no pronunciation items.

The result gives clarity, fluency, grammar, vocabulary and task response marks, with an equally weighted server-computed overall mark out of 100, strengths, specific improvements and a suggested English response. Clarity means estimated intelligibility, not similarity to a native accent or phoneme-level pronunciation measurement. Silence/unscorable audio gets no mark. These are approximate practice scores, not exam grades, CEFR levels or independently validated mastery evidence; neither AI results nor self-checks alter FSRS.

Uploads accept WebM, MP4 audio, WAV and Ogg up to 2 MB decoded, with media-signature checks and a route-specific 3 MB JSON limit. The browser stops recording at 60 seconds; submitted duration is client-reported, not independently decoded server-side. Oversized recordings can still be replayed/self-checked locally but need a shorter capture for AI upload.

At most two assessment submissions proceed concurrently, three new single-response assessments per learner/minute (six for conversation turns) and six new assessments per process/minute. All modes share the same per-learner request history. These guards are process-local, not deployment-wide guarantees. Results share existing Gemini project quota with other model calls. Completed retries return the saved receipt before rate/provider checks; in-flight matching event IDs share one model call. A changed recording or metadata cannot reuse that event ID. Provider errors save no assessment and retain the frozen recording/event for explicit retry; no automatic provider retries or paid fallbacks are used.

Navigation/account expiry cancels the browser request and releases local media. Once submitted, cancelling the browser cannot recall audio already sent to Google; a request may finish and be saved even if its response was not received. Vocabulary deletion is rechecked before persistence so late completions cannot recreate deleted learning data.

Saved attempts contain the written answer, mode, optional scenario, dictation mark, AI speaking assessment or self-check, playback/reveal metadata, recording duration and server task snapshot. Recent practice shows the last ten encounter results. Account checks run every three seconds and at known expiry; remote revocation is detected at the next successful check. Lookup/vocabulary/account deletion cascades to attempts.

## Everyday role-plays

Choose **Introductions**, **Making plans** or **Asking for help**, then saved English vocabulary with a known English source language. Translation-language settings remain unchanged. Each original fixed partner turn has a response goal: transcribe what the partner says, then switch to speaking and reply using the chosen expression. If that expression does not naturally fit, choose another word. These curated opening turns also seed the **Conversation** mode.

Scenario switches update the URL, clear local answers/recordings and restart listening. Refresh and Back/Forward retain the requested scenario. An unconfirmed save locks the selector until a retry succeeds or the attempt is explicitly abandoned. Real-time streaming conversation and independently validated skill assessment remain future work.

### Guided AI conversation

Select a scenario, then **Conversation**. Complete the AI partner's audio before recording a reply. Record and stop to receive automatic AI feedback; replay is optional. **Continue conversation** plays an AI follow-up based on your answer; complete four scored turns to see the mean turn estimate and review all turn feedback. Silent/unscorable audio does not advance the turn. Use **Record another response** to retry it. **New attempt** starts again. Feedback and transcripts are saved per turn; active dialogue navigation is held in this tab, so refresh resets the active conversation while recent turn results remain saved.

Later submissions send earlier dialogue text alongside the new recording, saved expression and scenario to Google. A compact disclosure appears before recording. The server derives prior context from owned, saved turns for the same encounter/scenario; client-authored prompts/history are not accepted. Each saved turn contains its bounded dialogue history and AI follow-up in the existing JSON snapshot; no extra migration or raw audio storage. Completed-event retries do not generate another follow-up. Provider quota failures preserve the same recording for retry and never switch to a paid provider.

## Verification

No new migration is needed: dictation and AI speaking scores use the existing `PracticeAttempt.task` JSON snapshot. The local launcher uses the same server-side Gemini key when Gemini translation or speech is enabled; simulated development uses an explicitly selected deterministic assessment provider. Deploy the existing practice migration before serving the API. Tests use isolated databases, simulated sign-in and deterministic audio; no Gemini credentials or live quota are needed. Regression coverage includes word alignment/normalization, owned snapshots, idempotency, no FSRS changes, AI-only playback, quota recovery, cancellation, submit-before-reveal and local microphone/recording cleanup. A native Chromium test verifies decodable recordings and actual local API grading and automatic media upload with deterministic assessment feedback. A public synthetic English sample was also transcribed and assessed by the live Gemini model; this is an integration smoke test, not validation across learner voices. The inline request follows the [Gemini audio guide](https://ai.google.dev/gemini-api/docs/audio). Live voice quality and microphone use still need a learner-device smoke check.
