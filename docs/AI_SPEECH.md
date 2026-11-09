# AI voices and reusable audio

Word/example speaker icons and practice/game playback use the same authenticated `/v1/speech` service. English can now run locally through Ollama + Phonon. Successful generated voices are saved as private WAV files with PostgreSQL metadata, so they can be replayed after an app restart without another generation request.

## Local setup

Install Ollama 0.40.9+ on an Apple Silicon Mac, then run:

```sh
pnpm speech:setup
pnpm db:generate
pnpm dev
```

The setup downloads `phonon:31m-en` (about 104 MB) and verifies the pinned digest `6667dd188d2e30e1448b0476ccbd36bdc8802225a848541d5049f450e2a983e0`. The launcher starts a loopback Ollama server if needed and verifies the model before startup. It never passes Google credentials into the model server. The adapter rechecks the digest for each new generation. A changed/missing model fails rather than silently upgrading. Phonon produces PCM locally; the adapter bounds and validates it, then writes an exact-length 24 kHz, mono, 16-bit WAV header. See [Ollama's speech guide](https://docs.ollama.com/capabilities/speech) and [Phonon model details](https://ollama.com/library/phonon). The selected 31M English checkpoint uses Apache 2.0; larger multilingual variants have different terms and are not enabled here.

Keep server-only settings in ignored `.env.local`:

```dotenv
SPEECH_PROVIDER=ollama
SPEECH_NON_ENGLISH_PROVIDER=gemini
SPEAKING_PROVIDER=gemini
GEMINI_API_KEY=your_existing_server_side_key
```

English playback uses Phonon without cloud quotas or the app's cloud six-per-minute generation cap. It still needs local CPU/memory and has one active generation plus one queued unique request. Each local provider call has a 75-second deadline; two serial calls fit inside the three-minute client timeout. Queue overflow returns a recoverable busy message. Failed jobs release the queue. The browser/extension request timeout is three minutes; typical short local generation is much faster. This is not a promise of unlimited hardware throughput.

Phonon 31M is English-only. `SPEECH_NON_ENGLISH_PROVIDER=gemini` explicitly retains existing Gemini voices for the other supported languages, with their quotas and cloud text processing. Omit that setting to disable cloud fallback. There is no silent paid fallback. Local translation/text models and recorded-speaking assessment are separate; the existing Gemini assessment still needs its key and quota. `SPEECH_PROVIDER=gemini` remains supported for all voices. The model/voice is `gemini-3.8-flash-lite-tts` / Kore; existing cloud guards allow two generations concurrently and six new requests per process/minute.

Restart `pnpm dev` and refresh the website. Rebuild/reload the extension if using its voice buttons, because local queueing needs the longer timeout. The extension's existing offscreen audio permission does not enable microphone recording.

## Storage choice

Reuse the project's Prisma + PostgreSQL connection for `SpeechAsset` metadata: owner, cache key, byte size, integrity digest and last use. Store audio bytes as private binary `.local/audio/<hash>.wav` files, not Base64 database values. This keeps the database small and supports a later S3-compatible private object store behind the same boundary. There is no new database service, storage SDK or dependency for local use.

Deploy migration `202611090002_saved_speech` and regenerate Prisma when deploying manually. The local launcher deploys migrations on startup; run `pnpm db:generate` after schema updates. Pass a durable `speechCacheDir` to `createApp` outside the launcher; fixtures omit it unless specifically testing disk storage. One local deployment owns one cache directory/index; multiple processes sharing this local folder are not supported.

Files are created atomically with mode 0600 under a private directory. No static file route serves them. Cache keys include account ID, exact text/language and provider/model/voice revision. Other accounts cannot fetch a file by guessing its name; the API accepts only bounded text/language and resolves ownership server-side. Metadata contains hashes rather than the original text. Cached entries are checked before model availability, rate limits and queue admission. If no provider is configured for a language, an owner/input hash finds the most recently saved matching audio without generating anything. A configured changed model still uses its own isolated cache. Duplicate in-flight requests, including disk misses, share one generation.

A 24-hour bounded memory cache accelerates playback. The disk cache survives restarts and is limited to 512 MB total, 128 MB per account and 30 days since persisted last use; least-recently-used assets are evicted first. Disk reads verify byte count, SHA-256 and WAV structure. Corrupted/missing/expired assets are discarded and regenerated when possible. User deletion cascades metadata; startup and five-minute cleanup remove orphan WAVs and temporary files. Pending writes recheck the owner under a user-row lock before indexing the audio. Files may remain until cleanup after account deletion; they are not accessible through the API after their index is removed. Backups must follow the deployment's deletion policy.

Only generated playback audio is cached. Raw microphone recordings are not saved here. Voice generation receives only requested text/language, not full pages, URLs or user IDs. English requests stay on the local machine. Configured Gemini requests follow the existing [Gemini API terms](https://ai.google.dev/gemini-api/terms), including unpaid-service data handling. Generated speech/IPA is not a validated pronunciation assessment.

## Practice and games

The [practice page](PRACTICE.md) and [games](GAMES.md) reuse the same saved audio and providers. Dictation grading needs no model call. Completed English recordings are separately submitted to the existing Gemini assessment; switching playback to Phonon does not make recording assessment local. Voice clicks do not create practice attempts or change FSRS. Cached vocabulary and saved feedback remain available independently of new voice generation.

## Verification

Tests use bounded PCM/WAV fixtures, isolated PostgreSQL and private temporary cache roots. Coverage includes local serial generation beyond six calls/minute, bounded queues and failure release, provider/digest/language validation, restart reuse without a working provider, changed-model and per-account isolation, corruption/expiry recovery, per-account/global budgets and account-deletion cleanup. Live checks use public synthetic text only; no personal microphone recording is uploaded.
