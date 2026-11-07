# ContextSpeak

**Build your vocabulary. Understand conversations. Speak with confidence.**

A browser extension and learning website that turns everyday English lookups into a personal vocabulary memory library, scheduled review, and contextual listening/speaking practice. The first release serves adults with basic English; broader language and school support follow later.

> Local developer slice implemented: fixture-backed capture → PostgreSQL history/candidates → website library. Not ready for public deployment.

## Run locally

Requires **Node.js 24** and **pnpm 11.25.0**. The launcher starts project-local PostgreSQL on loopback; Docker and paid translation APIs are unnecessary.

```bash
pnpm install
pnpm db:generate
pnpm build:extension
cp .env.example .env.local
# Add your Google web OAuth client ID and secret to .env.local.
pnpm dev
```

For simulated sign-in without a Google account, use **`pnpm dev:test`** instead. That local test provider is not real Google authentication. See [Google login setup](docs/LOGIN.md) for the callback registration and security boundaries.

1. Open [the library](http://127.0.0.1:3000/library) and **Sign in with Google** (or choose a test learner with `dev:test`).
2. In Chrome, open `chrome://extensions`, enable Developer mode, choose **Load unpacked**, and select `apps/extension/.output/chrome-mv3`.
3. In the extension, click **Pair extension**. Match its code on the website and **Approve extension**. Keep the popup open, or reopen it to finish pairing. Credentials are separate from your website session.
4. Enable automatic collection and **Save collection settings**. Collection starts off.
5. Open [the playground](http://127.0.0.1:3000/sample.html), select **available**, and click **Translate selection**. Refresh the library to see the encounter and candidate.

Fixtures cover `available`, `reschedule`, `confident`, and `follow up` (English → English only). Unknown expressions are saved with `FIXTURE_NOT_FOUND`. Google credentials and extension tokens never enter webpage content scripts.

Stop with Ctrl+C. Data stays in ignored `.local/postgres`; the extension queue survives restarts. Website sessions last seven days and paired extension access lasts thirty days. **Sign out** revokes the current website session; **Revoke all extensions** invalidates device access and unfinished approvals. Old developer-token identities are test data and are not merged into Google accounts.

Local ports: 3000, 4318, 4319 (simulated provider only), 55432. `compose.yaml` is an optional database configuration; do not run both databases on the same port.

## Verification

```bash
pnpm typecheck
pnpm test
pnpm exec playwright install chromium
pnpm test:e2e
pnpm build:web
```

`test:e2e` builds the extension and starts an isolated, disposable database and simulated OIDC provider; stop `pnpm dev` first so the ports are free. It exercises a real unpacked extension in bundled Chromium, including duplicate delivery, separate users, missing fixtures, and offline browser restart. Integration tests use native project-local PostgreSQL, not an in-memory substitute. A manual installed-Chrome smoke check is still required before pilot.

## Capture and result behavior

- Local query writes finish before any synchronization request; slow/offline sync does not hold the storage lock.
- Collected queries create one stored translation result. The selection panel reads it through the owned result endpoint; the website shows the same persisted meaning. A failed result does not trigger a second translation as a fallback.
- When collection is off or the site is excluded, the explicit translation request uses the transient translation-only endpoint without writing query history.
- One sync runs at a time, processing at most 20 eligible records per batch; consent/site-exclusion cleanup covers all pending records independently of that limit. Acknowledgements merge by owner/event ID rather than overwriting a queue snapshot. Discard aborts in-flight requests and removes local records; it cannot undo a request already accepted by the server.
- The selection panel polls for a bounded period. Saved queries can finish later and remain available in the website; paid-provider retry/exactly-once billing guarantees are outside this fixture slice.

## Current boundary

Implemented: Google OIDC sign-in, HttpOnly website sessions, one-time extension pairing and revocation, account ownership checks, collection settings/site exclusions, canonical URL stripping, automatic lookup history, unresolved vocabulary candidates, bounded durable replay, fixture success/failure/retry, and a responsive library. Candidate listing currently shows the latest 100; review activation, FSRS, lexical expansion, scenario inference, voice, export/deletion, and production operations are subsequent milestones. The simulated identity provider and local launcher must stay on loopback. Live Google sign-in needs configured credentials and a manual provider smoke check.

## System overview

![ContextSpeak: automatic lookup history, vocabulary memory, inferred scenarios, review, and listening/speaking practice](docs/assets/conceptual-diagram.png)

[Editable SVG](docs/assets/conceptual-diagram.svg)

## Documentation

| Document | Purpose |
| --- | --- |
| [Planning](docs/PLANNING.md) | Requirements mapping, MVP, roadmap, costs, and decisions |
| [Methodology](docs/METHODOLOGY.md) | Collection, scenario inference, word priorities, and mastery evidence |
| [Google login and pairing](docs/LOGIN.md) | Provider setup, sessions, pairing, and revocation |
| [Architecture](docs/ARCHITECTURE.md) | Stack, translation/dictionary services, data flow, and safeguards |
| [First coding plan](docs/superpowers/plans/2026-11-07-capture-to-library.md) | A bounded capture-to-library implementation with checks |

## Working principles

- After collection consent, automatically record each selection-translation query and derive vocabulary candidates; activation for scheduled review requires confirmation.
- Combine learner goals, inferred scenarios, and separate reading/listening/speaking evidence.
- Build independently. [FluentRead](https://github.com/FluentRead/FluentRead) is a functional reference, not a source of copied code or assets. This is not a legal clearance statement.

A distribution license is undecided. The diagram adapts a user-supplied layout reference; its ownership/permission and the ContextSpeak brand still need clearance before public release.
