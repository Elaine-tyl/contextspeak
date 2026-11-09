# Pilot release checks

ContextSpeak's current MVP is the browser extension and responsive website for adult English learners. Local tests and the synthetic rehearsal below are development evidence. They do not establish that a deployment is safe to recruit learners into, that provider marks agree with humans, or that learning improves.

## Startup and monitoring

`assertProductionConfig` in `apps/api/src/operations/production-config.ts` leaves development behavior unchanged. With `NODE_ENV=production`, call it before authentication discovery, resource creation or provider work, supplying the actual configured providers. It rejects simulated sign-in, missing OIDC, loopback HTTP overrides, missing translation/speech/speaking providers, fixture/mock providers and unsafe origins. HTTPS DNS hostnames and individually listed Chrome extension IDs are accepted; wildcards, credentials, IP addresses and local/internal hostnames are rejected. This is syntactic configuration validation; verify DNS, certificates, network binding, secrets, provider credentials and quotas separately.

The app registers `OperationsController` and provides `OperationsService` with a bounded database `SELECT 1` probe. Public `GET /health/live` returns only process liveness. Public `GET /health/ready` returns `200 {"status":"ready"}` or `503 {"status":"unavailable"}` on a failed/timed-out database check; it never returns connection details or exception text. Concurrent probes coalesce, including a stalled probe. This does not test speech, identity or translation vendors, filesystem availability or backup freshness. Use the database client's statement/connection timeout as well: a response deadline does not cancel an underlying database query.

Authenticated `GET /v1/operations` exposes aggregate process-local request/server-error counts and uptime. The app records `recordRequest(res.statusCode)` on response completion, excluding health polling. Counts reset at restart and combine all routes without identities, content, URLs or tokens. Keep the endpoint behind operator access at the deployment boundary. A reachable health URL and aggregate counts are a monitoring baseline; they do not prove alert delivery, durable usage accounting or measured latency.

Before recruitment, choose numerical gates and an operator for request errors, capture/sync/upload success, p50/p95 latency, failed account cleanups, provider quota/rate-limit failures and cost per completed session. Exercise the alert path in the target environment. Keep raw recordings, lookup excerpts and auth tokens out of telemetry. Existing per-process abuse limits need a deployment-wide limit when multiple instances run.

## Safe restore rehearsal

```sh
# Dry run: prints the plan; creates no database and reads no environment file.
node --import tsx scripts/pilot-check.ts

# Explicitly execute using only new temporary databases and invented records.
node --import tsx scripts/pilot-check.ts --synthetic
```

The command accepts no connection URL, backup file or destination path. It ignores `DATABASE_URL`, never opens `.local` or `.env.local`, creates two migrated PostgreSQL instances under the operating system's temporary directory, and removes its temporary data on normal completion/failure. Local socket permission is required. The schema-aware JSON snapshot contains only invented data; the script checks that its table list matches the current migrated schema.

The rehearsal saves two synthetic accounts with vocabulary, lookup history and sessions, takes a snapshot, deletes one account, writes a separate later deletion ledger, restores the stale snapshot into a fresh database, then replays the ledger twice. It verifies that the surviving account and its related records remain while the deleted account's related records are removed. No recording files or external providers are exercised. This fixture roundtrip is not `pg_dump`/PITR, production recovery, retention validation or a performance/RTO/RPO measurement.

The production recovery procedure must be rehearsed separately with the chosen infrastructure and approved recovery objectives:

1. Keep restored infrastructure isolated from user traffic, background workers and outbound provider jobs. Verify backup integrity and schema/version compatibility; restore the database and private audio storage using the approved backup tools.
2. Load the **latest deletion ledger from storage independent of the database backup**. A ledger inside an older snapshot cannot account for later deletions. Preserve deletion UUIDs, times and `hadSpeechAssets`; do not add identities or learning content. Protect the ledger and document its durability and retention.
3. Replay every ledger UUID against restored data in transactions: preserve/upsert the tombstone, cascade-delete matching `User` rows, preserve `hadSpeechAssets=true` if either copy has it, and reset `cleanupCompletedAt=null`. Repeating replay must be safe. Never restore old cleanup-complete markers as proof that restored audio has been removed.
4. Construct `AccountService(prisma, sharedSpeechStore)` and run `retryCleanup()` until every restored tombstone has `cleanupCompletedAt` set. The method returns void: inspect pending tombstones explicitly. A tombstone marked `hadSpeechAssets=true` stays pending without a configured audio store. Verify deleted users' restored audio is absent and retry after simulated storage failure. Include deletion requests made during recovery before enabling traffic.
5. Verify surviving ownership boundaries, login/extension revocation, representative acknowledged captures, review/practice records, private audio integrity and account deletion. Record actual recovery duration, data-loss interval, evidence, operator and date. Only then enable traffic.

Confirm database, private-audio and ledger backup encryption/access, backup retention/expiry, restore permissions and incident ownership before collecting pilot data. Tombstones currently remain indefinitely; approve a ledger retention policy before release. Never use this synthetic tool to inspect or modify an existing learner database.

## Human scoring and held-out learning gate

Automated assessment-policy regressions cover deterministic rules such as missing evidence, poor audio and off-topic responses. They do not establish human agreement. Keep this release gate open until an independent review is complete:

- Freeze a rubric/version and a held-out sample across learner levels, accents, microphones and realistic noise. Include unintelligible/silent audio, assisted/repeated answers and fluent off-topic answers. Exclude held-out tasks from generation.
- Have at least two qualified reviewers independently label intelligibility/communication, task response, grammar, vocabulary and fluency, plus whether scoring should be withheld. Resolve disagreements without seeing model marks first.
- Predeclare sample size and acceptable error/agreement rates. Compare model marks and withholding decisions with adjudicated labels by subgroup; inspect severe errors and accent-related disparities. Record provider/model/prompt versions and rubric changes, then repeat on untouched examples after changes.
- Compare baseline with delayed unseen level-matched tasks, tracking independence, assistance, retention and completion. A score increase alone is not a causal learning claim. See [the methodology](METHODOLOGY.md#evaluation).

Release still requires geography and retention decisions, reviewed collection/privacy wording, real identity and provider setup/quotas, dictionary/frequency permissions, cost measurements, real Chrome pairing/revocation and responsive-device checks, production backup/media-erasure rehearsal, operator alerts and the human evaluation above. School roles and native apps remain outside this MVP.
