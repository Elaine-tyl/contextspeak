# Translation cache implementation plan

Goal: reduce repeated Gemini requests while keeping owned vocabulary readable during quota exhaustion.

Design: a bounded, per-user in-memory success cache, shared by transient translations and saved-lookup processing. Exact text, context and source/target languages identify results; URLs do not affect Gemini's input. Entries expire after 24 hours, capacity is 500, concurrent identical requests share one promise, and failures are never cached. Cache lifetime follows the provider/service instance, so changing model/prompt on restart invalidates it. Saved history remains database-backed and independent of the cache/provider. No dependencies or database migrations.

Alternatives: browser-only caching misses worker/repeated-tab requests; persistent caching needs retention and invalidation machinery. A bounded backend cache fits this development slice with minimal changes. It intentionally does not survive restarts.

- [x] Add failing cache tests: repeat/coalescing, scope/context/language isolation, expiry/capacity, failures and mutation safety.
- [x] Implement cache and wire authenticated user scope into both translation paths; Gemini only.
- [x] Add API regression for repeated captures and transient reuse, quota failure, readable saved history and retry without duplicate encounters.
- [x] Run full unit/integration tests, typecheck, formatting and capture browser regressions. Update Gemini docs. Do not commit unrelated pending work.

Validation: cache regression failures observed before implementation; 96 unit/integration tests and 12 Chromium browser regressions passed. Typecheck and formatting passed. Static independent review found no actionable issues. No live Gemini quota was consumed by these tests.
