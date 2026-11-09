# AI speaking practice assessment

Extend the existing English practice recorder with optional explicit Gemini audio assessment. Keep local replay/self-check available. Show upload disclosure and require a fresh consent checkbox plus Submit for AI assessment after recording/replay. No automatic microphone upload.

- [x] Test and define strict upload/result schemas: <=60 seconds, <=2 MB decoded audio, supported media signatures, consent, owned lookup and bounded feedback.
- [x] Implement Gemini inline-audio assessment with fixed rubric/model, timeout, bounded output, no raw-audio storage, silence/unscorable handling and server-computed overall mark.
- [x] Add an owned authenticated endpoint with route-specific body limit, process-local concurrency/per-user guards, event-ID coalescing/idempotency, durable transcript/feedback snapshot and deletion recheck. Preserve existing self-check/dictation endpoints and FSRS.
- [x] Expose the completed recording Blob; add submit/retry/cancel cleanup, score breakdown, transcript, actionable feedback and recent-history label. Keep consent/reset/recording payload frozen for retries.
- [x] Add deterministic provider/API/browser regressions, verify a public synthetic-audio live smoke if available, update privacy/config docs, run checks/build and commit locally.

Scope: English adult practice. Five equally weighted 0–100 rubric dimensions: clarity, fluency, grammar, vocabulary, task response. Scores are approximate AI feedback, not phoneme measurements, CEFR certification or inferred mastery. The model must identify audible speech; silence/noise gets no score. Upload and exercise prompt go to Google only after explicit in-product consent. ContextSpeak stores transcript, rubric, consent and model metadata, never the raw recording. Google processing follows the project's existing service terms; do not promise provider-side deletion. Free-tier quota is shared with existing Gemini text calls; no billing changes/fallback. Inline media is memory-only; no Files API or new dependencies/migration.

## Verification

146 unit/API tests and 40 browser tests passed. Type checking, extension build, production web build, formatting and diff checks passed. Review found and resolved an overlapping-submit race (with a red/green regression) and a duplicate history score label. A live Gemini check transcribed fresh public synthetic English speech and returned rubric feedback; no personal microphone recording was uploaded. Physical microphone quality and scoring accuracy across learners still require user evaluation. No database migration or FSRS changes.
