# AI audio and scored dictation

Use the existing server-side cached Gemini speech service for all generated practice audio, with no device-voice fallback. User recording replay remains the learner's own audio. Practice listening becomes transcription: type what was said, submit once, then display the server's transcript, score and word corrections. Speaking remains local recording/replay and self-check; no speech-recognition or speaking score is claimed.

- [x] Test and implement bounded Unicode word alignment with case/punctuation normalization and explicit missing/extra/replacement feedback.
- [x] Validate dictation submissions, compute/store feedback with the server task snapshot and return the same result on idempotent retries. Retain old self-check receipts; keep FSRS unchanged.
- [x] Reuse AI audio playback lifecycle, count only completed playback for submission readiness, and remove unused device speech code. Handle quota/failure/cancellation without enabling submission.
- [x] Replace listening reveal/self-check with Submit answer and marks/feedback after successful submission. Freeze retry payloads; keep scenario/account/navigation cleanup.
- [x] Update regression fixtures and tests for real API grading, AI-only playback, failures and retries; run checks/build, update docs and commit locally.

Scoring: word error rate (substitutions + insertions + deletions) against the expected word count, clamped to 0–100. Normalize Unicode compatibility, case and punctuation; preserve accents, word order and lexical differences. Use language-aware segmentation. These are repeatable practice marks, not proctored assessment; client playback/reveal signals are self-reported. No LLM grading, new provider, dependency, quota category or database migration is needed. API responses already contain task text for playback; UI withholding is not an anti-cheating boundary.

Verification: 133 unit/API tests, 14 focused Chromium regressions, typecheck, formatting and production web build passed. Review fixes have red/green coverage: nonzero edit distance cannot round to 100, and 21 consecutive quota failures cannot exhaust the completed-play allowance. Existing device speech code was removed; no extension rebuild or database migration is needed for this website change.
