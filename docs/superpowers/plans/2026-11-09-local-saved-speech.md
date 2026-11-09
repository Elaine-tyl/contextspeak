# Local and saved AI speech

Implement approved local Phonon plus restart-persistent private audio caching. Keep Prisma/PostgreSQL for metadata, private WAV files under `.local/audio` for local storage. No raw microphone recordings are stored here. Existing Gemini speaking assessment remains enabled independently.

- [x] Download and pin Phonon 31M English by digest; validate local speech capability and real generated audio. No paid/cloud fallback or new API key. English first; non-English voices stay available only through explicitly configured Gemini or previously saved audio.
- [x] Add loopback-only Ollama speech adapter with bounded requests/responses, PCM/WAV validation and model identity. Local generation has a bounded single-worker queue without the cloud six-per-minute cap; cache hits precede provider limits.
- [x] Add SpeechAsset metadata migration and private atomic WAV store. Cache key includes owner, exact text/language, provider/model/voice revision. Persist across restarts; check integrity, purge invalid/expired files and orphaned deleted-account assets. Bound disk to 512 MB globally, 128 MB per account and 30 days since last use. User FK deletion cascades metadata; pending writes recheck owner.
- [x] Reuse providers for all voice buttons and game/practice audio. Add local setup/start script and keep reproduction/config instructions concise. Extend playback request timeout for queued local work; no interface redesign.
- [x] Regression tests: queue >6 local requests, bounded concurrency/overflow, restart reuse without provider, per-user separation, changed-model isolation, corruption recovery, deletion/orphan cleanup, provider MIME/errors/bounds. Run full tests/build/browser checks, review, commit locally and restart.

## Verification and review fixes

173 unit/API tests in 35 files and all 47 browser tests passed. Typecheck, formatting and diff checks passed; extension and production web builds verified before commit. Phonon was downloaded and its digest/capability checked. A live public-text generation produced valid local WAV audio; a second integration check generated 238124 bytes, stored one private file/index entry, and replayed identical audio from a fresh service with no provider configured. No personal microphone audio was used.

Review found two issues, each reproduced by a failing regression then fixed: provider-disabled playback now uses an owner/input hash lookup, while configured changed-model caches stay isolated; local admission now allows only one active plus one queued unique request, with 75-second provider deadlines fitting the 180-second client budget. Failed/overflowed queue work releases correctly. Cloud guards remain unchanged. The store removes deleted-account orphans within startup/five-minute cleanup; it is single-process local storage, not a multi-node object store.

The existing ignored `.env.local` was updated privately for local English playback and retained Gemini non-English voices/recording assessment. The chosen storage is existing Prisma/PostgreSQL metadata plus binary WAV files; no additional database or SDK was installed.
