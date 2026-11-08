# Natural AI pronunciation

Speaker icons beside words, original-language phrases and example sentences use Gemini AI speech. Click an icon to listen; audio never starts automatically and no playing text is displayed. Another speaker replaces the current audio. Closing/navigating the card or leaving its page stops pending and playing speech.

## Setup

Keep these server-only settings in ignored `.env.local`:

```dotenv
SPEECH_PROVIDER=gemini
GEMINI_API_KEY=your_existing_server_side_key
```

Restart `pnpm dev`, reload extension **0.7.0** and refresh open webpages. The existing extension folder/pairing can be retained. Chrome 116+ is required. Its `offscreen` permission enables audio inside an extension document, avoiding webpage media-policy restrictions; it does not enable microphone recording.

The fixed model is `gemini-3.8-flash-lite-tts`, voice **Kore**. The Developer API currently lists a [free tier](https://ai.google.dev/gemini-api/docs/pricing#gemini-3.8-flash-lite-tts), subject to project eligibility and quotas. This code does not enable billing or switch to paid models. Configure/check project billing separately if deployment policy changes. See the [official speech API](https://ai.google.dev/gemini-api/docs/generate-content/speech-generation) for the transcript/style format and WAV output.

## Privacy, cache and limits

Only clicked text (up to 600 Unicode characters), its source language and a delivery-style instruction are sent to Google. No full page, URL, user recording or API key is sent to the browser. The six translation languages are accepted; generated pronunciation is not a validated language assessment.

Successful audio is cached privately by authenticated user, exact text and language. The process cache expires after 24 hours and evicts least-recently-used entries beyond 100 entries or 32 MB of encoded audio. Concurrent duplicates share one generation. Restarts/eviction discard cached audio, not saved vocabulary. HTTP responses remain private/no-store.

The prototype allows at most two concurrent generations and six new generations per minute across the server process. These are application guards, not promises of Google quota. Cache hits work before these limits and after provider quota errors. Errors/malformed audio are not cached; an error is shown without silently switching to a device voice. Cancellation prevents later playback but cannot undo an already issued provider request.

Keys stay server-side. Website requests use the existing cookie/CSRF session and recheck it before playback. Extension requests use the paired identity, revalidated before playback; audio is played in an offscreen extension document. Card/page/tab cleanup and disconnect abort stale requests and stop audio.

## Practice remains separate

The dedicated listening/speaking practice page still uses on-device playback and browser-only recording, as described in [practice](PRACTICE.md). AI word/example pronunciation does not create a practice attempt or change FSRS ratings. Recorded user audio is never uploaded.

## Verification

Default tests use deterministic speech fixtures without Google credentials. API tests cover cache reuse/coalescing, user isolation, quota recovery, expiry/eviction, request bounds and invalid WAV rejection. Browser tests cover phrase/example controls, account/card cleanup, and a real loaded extension's offscreen player. A public-text live smoke test verified one real model request followed by a cache hit. Device playback quality remains a learner smoke check.
