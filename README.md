# ContextSpeak

**Build your vocabulary. Understand conversations. Speak with confidence.**

A browser extension and learning website that turns everyday English lookups into a personal vocabulary memory library, scheduled review, and contextual listening/speaking practice. The first release serves adults with basic English; broader language and school support follow later.

> Working local flow: Google login → paired Chrome extension → free local word/sentence translation → PostgreSQL history/candidates → website library. Not ready for public deployment.

## Run locally

Requires **Node.js 24**, **pnpm 11.25.0** and **Python 3** for one-time bootstrap. The launcher starts PostgreSQL and a free local LibreTranslate service on loopback. Runtime dependencies and model checksums are pinned; Docker and translation API keys are unnecessary.

```bash
pnpm install
pnpm db:generate
pnpm build:extension
cp .env.example .env.local
# Add your Google web OAuth client ID and secret to .env.local.
pnpm dev
```

For real translation with simulated sign-in and no Google account, use **`pnpm dev:local`**. `pnpm dev:test` is reserved for deterministic fixture-only tests. See [translation setup](docs/TRANSLATION.md) and [Google login setup](docs/LOGIN.md).

1. Open [the library](http://127.0.0.1:3000/library) and **Sign in with Google** (or choose a test learner with `dev:local`).
2. In Chrome, open `chrome://extensions`, enable Developer mode, choose **Load unpacked**, and select `apps/extension/.output/chrome-mv3`.
3. In the extension, click **Pair extension**. Match its code on the website and **Approve extension**. Keep the popup open, or reopen it to finish pairing. Credentials are separate from your website session.
4. Enable automatic collection and **Save collection settings**. Collection starts off.
5. Choose source/target languages in the popup and save them. Optionally enable automatic translation after selection. Open [the playground](http://127.0.0.1:3000/sample.html) or another webpage and select a word or sentence. Refresh the library to see a collected encounter and candidate.

Normal mode translates arbitrary selections up to 2,000 characters with installed English/Chinese models. First startup downloads and verifies the models; later translation runs locally. Reload the unpacked extension and refresh existing pages after an update. Google credentials and extension tokens never enter webpage content scripts.

Stop with Ctrl+C. Data stays in ignored `.local/postgres`; the extension queue survives restarts. Website sessions last seven days and paired extension access lasts thirty days. **Sign out** revokes the current website session; **Revoke all extensions** invalidates device access and unfinished approvals. Old developer-token identities are test data and are not merged into Google accounts.

Local ports: 3000, 4318, 4319 (simulated provider only), 5001 (translator), 55432. `compose.yaml` is an optional database configuration; do not run both databases on the same port.

## Verification

```bash
pnpm typecheck
pnpm test
pnpm exec playwright install chromium
pnpm test:e2e
pnpm test:translation:live
pnpm build:web
```

`test:e2e` builds the extension and starts an isolated, disposable database and simulated OIDC provider; stop `pnpm dev` first so the ports are free. It exercises a real unpacked extension in bundled Chromium, including duplicate delivery, separate users, missing fixtures, and offline browser restart. Integration tests use native project-local PostgreSQL, not an in-memory substitute. The live translation suite exercises the actual local models; a manual installed-Chrome smoke check is still required before pilot.

## Capture and result behavior

- Local query writes finish before any synchronization request; slow/offline sync does not hold the storage lock.
- Collected queries create one stored translation result. The selection panel reads it through the owned result endpoint; the website shows the same persisted meaning. A failed result does not trigger a second translation as a fallback.
- When collection is off or the site is excluded, the explicit translation request uses the transient translation-only endpoint without writing query history.
- One sync runs at a time, processing at most 20 eligible records per batch; consent/site-exclusion cleanup covers all pending records independently of that limit. Acknowledgements merge by owner/event ID rather than overwriting a queue snapshot. Discard aborts in-flight requests and removes local records; it cannot undo a request already accepted by the server.
- The selection panel polls for a bounded period. Saved queries can finish later and remain available in the website; paid-provider integration and billing guarantees are outside this local release.

## Current boundary

Implemented: Google OIDC sign-in, HttpOnly website sessions, one-time extension pairing and revocation, account ownership checks, collection settings/site exclusions, canonical URL stripping, automatic lookup history, unresolved vocabulary candidates, bounded durable replay, real free word/sentence translation, language preferences, optional automatic selection translation, success/failure/retry, and a responsive library. Candidate listing currently shows the latest 100; review activation, FSRS, lexical expansion, scenario inference, voice, export/deletion, and production operations are subsequent milestones. The simulated identity provider and local launcher must stay on loopback. Live Google sign-in needs configured credentials and a manual provider smoke check.

## System overview

![ContextSpeak: automatic lookup history, vocabulary memory, inferred scenarios, review, and listening/speaking practice](docs/assets/conceptual-diagram.png)

[Editable SVG](docs/assets/conceptual-diagram.svg)

## Documentation

| Document | Purpose |
| --- | --- |
| [Planning](docs/PLANNING.md) | Requirements mapping, MVP, roadmap, costs, and decisions |
| [Methodology](docs/METHODOLOGY.md) | Collection, scenario inference, word priorities, and mastery evidence |
| [Free translation](docs/TRANSLATION.md) | Reproducible local setup, languages, usage and live regression suite |
| [Google login and pairing](docs/LOGIN.md) | Provider setup, sessions, pairing, and revocation |
| [Architecture](docs/ARCHITECTURE.md) | Stack, translation/dictionary services, data flow, and safeguards |
| [First coding plan](docs/superpowers/plans/2026-11-07-capture-to-library.md) | A bounded capture-to-library implementation with checks |

## Working principles

- After collection consent, automatically record each selection-translation query and derive vocabulary candidates; activation for scheduled review requires confirmation.
- Combine learner goals, inferred scenarios, and separate reading/listening/speaking evidence.
- Build independently. [FluentRead](https://github.com/FluentRead/FluentRead) is a functional reference, not a source of copied code or assets. This is not a legal clearance statement.

A distribution license is undecided. The diagram adapts a user-supplied layout reference; its ownership/permission and the ContextSpeak brand still need clearance before public release.
