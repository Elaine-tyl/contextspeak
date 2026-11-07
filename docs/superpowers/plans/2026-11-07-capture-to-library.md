# Capture-to-Library Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkboxes for tracking.

**Goal:** Demonstrate one consented selection-translation query appearing once in the same learner's website history and vocabulary candidates.

**Architecture:** WXT extension → authenticated NestJS API → PostgreSQL → Next.js library. Use deterministic translation fixtures first; integrate DeepL only after the flow works. This is a local developer slice, not a pilot release or completed M1.

**Tech Stack:** TypeScript, pnpm workspace, React/WXT, Next.js, NestJS, PostgreSQL, Prisma, Zod, Vitest, Supertest, Playwright; proposed implementation defaults, versions pinned during scaffold.

**Spec:** [Architecture](../../ARCHITECTURE.md), [methodology](../../METHODOLOGY.md), [product plan](../../PLANNING.md).

## Global constraints

- English throughout; independent implementation, no FluentRead code/assets.
- Collection-off requests can translate without persisting learner content.
- Persist eligible lookup intent independently of translation success.
- Derive ownership from verified identity, never a client owner field.
- No paid calls, speech, inferred scenarios, or queue infrastructure needed for this slice.

## Review focus

- Consent revoked while a query is queued: discard it before upload (Task 3).
- Same event ID with changed payload: reject conflict, do not overwrite (Task 2).
- Another learner uses the same event ID: isolate records by owner (Task 2).
- Translation fails after capture: retain pending history and show retry (Tasks 2/4).
- Ambiguous sense or a repeated word: preserve unresolved sense and separate encounters (Task 2).

## Task 1 — Contracts and authenticated API boundary

**Files:** root `package.json`, `pnpm-workspace.yaml`, `compose.yaml`, `.env.example`; `packages/contracts/src/lookup.ts`; `apps/api/src/auth/{identity,jwt.guard}.ts`; `apps/api/test/auth.e2e-spec.ts`.

**Interfaces:** `LookupInput = { eventId: UUID; text: string; context?: string; sourceUrl?: string; sourceLanguage: string; targetLanguage: string }`; `Identity = { issuer: string; subject: string }`; `verifyBearer(token: string): Promise<Identity>`. Text limit: 200 Unicode code points; context: 1,000; canonical source URL excludes credentials/query/fragment. No client-supplied owner.

- [x] Scaffold only contracts, API, web, and extension workspaces; pin compatible versions and add root `typecheck`/`test` scripts.
- [x] Add `rejects_missing_expired_wrong_audience_and_unsigned_tokens` using a local test JWKS issuer; assert HTTP 401 and no domain writes. Run `pnpm --filter @contextspeak/api test -- auth.e2e-spec.ts`; confirm failure before the guard exists.
- [x] Implement signature, issuer, audience, and expiry validation. Resolve identity by issuer+subject. Synthetic short-lived test identities support local development only; no production auth bypass or registration endpoint.
- [x] Run the test and `pnpm typecheck`; require passes. Commit the boundary. Real hosted login/pairing is required before pilot and is outside this slice.

## Task 2 — Durable history and candidate API

**Files:** `apps/api/prisma/schema.prisma`; `apps/api/src/library/{library.controller,library.service,translation.provider}.ts`; `apps/api/test/library.e2e-spec.ts`.

**Interfaces:** `POST /v1/lookups` consumes `LookupInput` and returns `{ lookupId, candidateId, translationStatus: 'pending' }`; `GET /v1/lookups?limit=20&cursor=...` returns owned history; `GET /v1/learning-items` returns owned candidates. `TranslationProvider.translate(input): Promise<{ meaning: string; provider: string }>` uses original fixture entries. Reject capture with HTTP 403 when server collection consent is off. `POST /v1/translations` works without collection and does not persist request text.

- [x] Write integration tests against local PostgreSQL: `replay_is_idempotent`, `changed_payload_conflicts` (409), `same_id_different_owner_is_isolated`, `real_repeat_adds_encounter`, `unresolved_sense_is_preserved`, `collection_off_does_not_write`, `translation_failure_keeps_lookup`. Assert event/item counts and owner-filtered responses. Run `pnpm --filter @contextspeak/api test -- library.e2e-spec.ts`; confirm failures.
- [x] Implement transactions and unique `(userId, eventId)` constraint; normalize surface/language, preserve unresolved sense, and link subsequent encounters without pretending sense certainty. Replay returns the original IDs; translation failure cannot roll back capture.
- [x] Implement bounded local translation dispatch and pending/failed status; pending work resumes on local API startup. No Redis dependency yet. Reject conflicts, invalid lengths, invalid URLs, and unsupported fixture pairs without a successful fake translation.
- [x] Run integration tests, migrations on a fresh local database, and typecheck. Commit. Async expansion, real dictionary data, and production outbox dispatch remain subsequent M1 work.

## Task 3 — Extension capture and replay

**Files:** `apps/extension/entrypoints/{content,background}.ts`; `apps/extension/src/{outbox,settings}.ts`; `apps/extension/test/outbox.test.ts`.

**Interfaces:** `enqueueLookup(input: LookupInput): Promise<void>`; `flushOutbox(): Promise<void>`; `CollectionSettings = { enabled: boolean; excludedOrigins: string[] }`. Each genuine query receives a new UUID; retries reuse it. Auth uses a local short-lived test token for this developer demo, never an API key; expired identity pauses sync.

- [x] Test `offline_replay_survives_restart`, `ack_lost_replays_same_id`, `revoked_consent_discards_pending`, `excluded_origin_never_captures`, and `expired_identity_pauses_without_loss`. Run `pnpm --filter @contextspeak/extension test -- outbox.test.ts`; confirm failures.
- [x] Persist eligible queries in extension-local storage before upload. Recheck current consent and site eligibility before transmission; sanitize/exclude form inputs and restricted pages. Show pending, failed, and synchronized states. Consent-off translation sends no collection event.
- [x] Run tests and load the unpacked extension on a local fixture webpage. Verify one query, then offline/restart/replay. Commit.

## Task 4 — Website and end-to-end proof

**Files:** `apps/web/app/library/page.tsx`; `apps/web/src/api.ts`; `tests/e2e/capture-to-library.spec.ts`; update root `README.md` with actual setup commands.

**Interfaces:** Display the history/candidate responses from Task 2 under the same verified identity. Candidate status is visible; activation and FSRS are the next slice, not fake completed features.

- [x] Write an end-to-end test using a persistent Chromium context, unpacked extension, local test identities, API, database, and fixture webpage. Assert the selected expression appears once on the website, event replay adds no row, a second genuine lookup adds an encounter, a different identity cannot see it, and a translation error leaves history visible. Run `pnpm test:e2e`; confirm failure before UI implementation.
- [x] Implement the library, translation/pending/error display, and local-demo identity wiring; no hidden production bypass. Make the fixture test suite independent of paid providers.
- [x] Run `pnpm typecheck`, `pnpm test`, and `pnpm test:e2e`; require all passes. The installed-Chrome manual check remains outstanding (see verification record); bundled Chromium is covered. Record supported versions and exact setup commands; commit.

## Exit and next slice

Done means a real extension query is durable, private to its learner, replay-safe, and visible on the website. It does not mean the product is pilot-ready. Next: real login/pairing + translation/lexical expansion to finish M1, then confirmed activation + FSRS review, followed by voice and scenario inference. Review this plan before application scaffolding.

## Verification record

Implemented in `capture-to-library`. Fixture/PostgreSQL integration, account and consent race regressions, extension/browser restart, safe context extraction, and mobile layout checks are automated. A manual smoke check with the extension installed in the user's regular Chrome profile was not performed; it remains a pre-pilot check. Hosted login, real translation, and subsequent learning features remain outside this slice.

## Follow-up correctness improvements

Local persistence is now independent of network sync. Collected queries share the stored translation with the selection panel through an owner-scoped read endpoint; transient translation remains for collection-off/excluded sources. Regression coverage includes blocked settings/send, acknowledgement merge, immediate discard, cross-account capture cancellation, provider-call count across result reads/replays, and actual browser collection-on/off paths.
