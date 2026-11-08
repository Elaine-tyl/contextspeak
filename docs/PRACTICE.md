# Listening and speaking practice

Open **Listening & speaking** in the library sidebar, or choose **Practice listening and speaking** on a vocabulary detail page. The practice list supports text search and translation-language filters, showing up to 50 recent matching vocabulary items. Each link uses the latest successful encounter, so a later failed retry does not hide a usable translation. A successful saved translation is required. Practice uses that encounter's first saved bilingual example, or the selected expression and its meaning when no example is available. Passages over 600 characters require a shorter expression.

## Word, phrase and example pronunciation

Speaker icons in vocabulary notes and translation cards now use [Gemini AI pronunciation](AI_SPEECH.md) with private audio caching. They read the clicked original-language text; Google receives that text and its language. This is separate from the on-device practice flow below and never uploads microphone recordings. Reload extension 0.7.0 and refresh existing webpages to activate the example icons.

## Listening practice (on-device)

Choose **Play audio**, write what it means, then reveal the transcript and meaning. Choose **Comfortable** or **Needs practice** to save your self-check. Playback must finish successfully before saving. Replays and text reveal are tracked; an interrupted or failed playback does not count as completed listening. Each attempt allows up to 20 plays.

Only matching on-device speech voices are used, based on the lookup's detected source language. If no suitable local voice exists, install a device voice or use speaking practice. No remote voice or cloud fallback is selected. Voices and quality vary across operating systems; supported translation languages do not guarantee matching installed speech voices. The browser's [`localService` flag](https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesisVoice/localService) distinguishes local and remote voices.

## Speaking

Choose **Speaking**, record a short sentence using the expression, stop, and replay your recording. **Show example** reveals the saved example and marks text exposure. Replay must finish before saving an honest self-check. New attempts, mode changes and navigation discard the previous local recording.

Microphone capture starts only after a click and browser permission. It requires HTTPS or loopback development. Capture stops at 60 seconds, on tab hiding/navigation, or on session expiry/account changes. A 10 MB local buffer limit rejects oversized recordings. Denied permission, empty audio and recording errors allow retry. A late permission grant after leaving the page immediately releases its tracks.

Audio stays in browser memory. It is never uploaded, transcribed, stored in the database, or sent to Gemini. Object URLs and microphone tracks are released on cleanup. Saving practice stores the optional written response, mode, self-check, playback/reveal counters, replay completion and recording duration, bound to the owned successful lookup with a server-generated task snapshot. Recent practice shows the last ten results for that encounter. A failed save keeps the same event ID and response so retry cannot count twice. You can explicitly start a new attempt instead; the unconfirmed previous result may already be saved.

## Next milestone: everyday conversations

Prioritize everyday conversations for the first novel scenarios: introductions, making plans, asking for help and everyday requests. Reuse saved vocabulary, hide the model response until after the learner answers, and keep assisted attempts separate from independent evidence. Scenario generation and assessment are not implemented in the current guided practice flow.

## What this establishes

This is guided, self-reported practice with known vocabulary and saved examples. Results are separate from FSRS recall and do not update vocabulary ratings or inferred mastery. Speaking has no automated intelligibility, pronunciation, transcription or semantic assessment. Recording duration alone is not evidence of speech quality. Novel scenarios and independently assessed listening/speaking are future work.

The existing session is checked every three seconds and at its known expiry. Server-side revocation is detected at the next successful authentication check; capture is not claimed to stop instantaneously at remote revocation. The on-device practice flow needs no speech provider credentials. AI pronunciation icons use the separately configured server-side Gemini key.

## Development and verification

`pnpm dev` deploys the additive `202611080002_practice_attempts` migration. Production deployment must run `pnpm db:migrate` before serving the new API. Attempts cascade when the lookup/vocabulary/account is deleted.

API integration tests cover owned encounters, detected language, bounds, strict no-audio submissions, concurrent idempotent saves, changed-event conflicts, cascade deletion and no FSRS updates. Browser regressions cover listening/reveal/save/retry, local-only voice selection, microphone denial, late grants, navigation/session cleanup and duration limits. A native Chromium test uses a generated audio stream to verify decodable recordings and the real local save API. These deterministic tests do not establish audible quality on a learner's device; try a local voice and a microphone recording there before a pilot.
