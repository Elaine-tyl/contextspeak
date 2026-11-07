# Free translation implementation plan

**Goal:** Translate selected words/sentences using a local, free translation service, with saved language settings and regression tests.

**Design:** Keep NestJS as the only extension API. It calls a separate loopback LibreTranslate service; no paid API, scraping or provider key. Start with installed English/Chinese models and automatic source detection. The provider advertises installed languages, so the UI never promises missing language pairs. Pin the Python/runtime dependencies and language models; automate setup/start through pnpm scripts.

**Behavior:** Preserve explicit collection consent, account ownership, the durable local outbox and one shared persisted result. Allow selections up to 2,000 Unicode characters. Language choices are snapshotted per lookup; changing settings never rewrites queued work. Save preferences per account. Offer optional automatic translation after selection, with a short debounce; the manual button remains available. Text is rendered as text, never provider HTML. Only selected text is sent to the local translator.

**Implementation steps:**

1. Write failing regressions for provider transport/errors/timeouts, sentence bounds, saved language preferences and worker language forwarding.
2. Implement the small LibreTranslate HTTP adapter, installed-language metadata and per-account preference endpoints. Keep fixtures explicitly available for deterministic tests.
3. Add popup and selection-panel language controls, optional automatic selection translation, friendly provider errors and language labels in the library. Test account-switch/pending-queue behavior.
4. Add pinned, isolated translator setup/start scripts with comments; run real English↔Chinese translation and browser capture checks. Document one-command setup, reload instructions and limitations.
5. Run unit/API, browser regressions, types, format and builds; request fresh review. Existing uncommitted Grammarly compatibility fix and live-login notes are preserved.

**Scope:** No external paid APIs or Google Cloud billing. Software/model downloads are needed once; local CPU/RAM/storage are used. Google login credentials remain unchanged. Hosted production operation is a separate milestone.

## Verification

- `pnpm test`: 62 API/unit regressions passed.
- `pnpm test:e2e`: 12 deterministic Chromium regressions passed.
- `pnpm test:translation:live`: 2 actual-model regressions passed, including automatic/manual translation, saved languages, shared library results and shutdown during translator startup.
- Type checking, formatting and both production builds passed.
- Real model smoke checks translated a new train-station sentence in both directions and `Capture` to `捕获`. No paid API was called.
- Fresh review findings were fixed: the manual button cancels the pending automatic timer; translator ownership is registered before readiness; the pairing sign-in link waits for the query code/session state. The duplicate-request test failed with two calls before the timer fix and passes with one.
- The previous Grammarly compatibility fix remains covered and unchanged.


## Follow-up: empty menus and missing panels

Browser test servers now receive SIGTERM with a cleanup timeout so embedded PostgreSQL can release shared-memory resources. Previous forced shutdowns exhausted the local segment limit; only confirmed unattached segments belonging to stopped test databases were removed. Persistent database files were preserved.

Reproduced and fixed a new popup talking to an old worker (zero target options), read-only `contenteditable=false` containers, and stopped mouseup bubbling. Added extension-protocol validation, visible error/retry states, known initial choices and normalized saved codes. Capture-phase mouseup handling preserves editor/privacy exclusions. A review-found account-response race was reproduced (old English choice overwrote the new account's Chinese target), then fixed with refresh generations and final background account checks. Final regression counts are recorded in the completion report.
