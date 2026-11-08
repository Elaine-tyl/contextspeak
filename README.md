# ContextSpeak

[![CI](https://github.com/Elaine-tyl/contextspeak/actions/workflows/ci.yml/badge.svg)](https://github.com/Elaine-tyl/contextspeak/actions/workflows/ci.yml)

**Build your vocabulary. Understand conversations. Speak with confidence.**

A browser extension and learning website that turns everyday English lookups into a personal vocabulary memory library, scheduled review, and contextual listening/speaking practice. The learning focus is adults with basic English; translation cards support six languages, while school workflows follow later.

> Working local flow: Google login → paired Chrome extension → Gemini or local word/sentence translation → PostgreSQL history/candidates → website library. Not ready for public deployment.

## Run locally

Requires **Node.js 24**, **pnpm 11.25.0** and [Ollama](https://ollama.com). The launcher starts PostgreSQL and reuses or starts local Ollama on loopback. The Qwen3 4B model and digest are pinned; Docker and translation API keys are unnecessary. Python 3 is needed only for the optional plain LibreTranslate provider.

```bash
pnpm install --frozen-lockfile
pnpm db:generate
pnpm build:extension
cp .env.example .env.local
# Add your Google web OAuth client ID and secret to .env.local.
# Install/start Ollama once, then download and verify the model:
pnpm ai:setup
pnpm dev
```

For real translation with simulated sign-in and no Google account, use **`pnpm dev:local`**. `pnpm dev:test` is reserved for deterministic fixture-only tests. See [AI translation setup](docs/AI_TRANSLATION.md) and [Google login setup](docs/LOGIN.md).

1. Open [the library](http://127.0.0.1:3000/library) and **Sign in with Google** (or choose a test learner with `dev:local`).
2. In Chrome, open `chrome://extensions`, enable Developer mode, choose **Load unpacked**, and select `apps/extension/.output/chrome-mv3`.
3. In the extension, click **Pair extension**. Match its code on the website and **Approve extension**. Keep the popup open, or reopen it to finish pairing. Credentials are separate from your website session.
4. Enable automatic collection and **Save collection settings**. Collection starts off.
5. Choose source/target languages in the popup and save them. Automatic translation defaults on; existing explicit off settings remain respected. Open [the playground](http://127.0.0.1:3000/sample.html) or another webpage and select a word or sentence. Refresh the library to see a collected encounter and candidate.

AI mode translates selections up to 600 characters in English, Chinese, Japanese, Korean, Spanish, and French, with meanings and bilingual examples. No initial Translate button is needed. The optional plain English/Chinese provider retains its 2,000-character limit. Model setup is a one-time verified download; generation runs locally. Reload the unpacked extension and refresh existing pages after an update. Google credentials and extension tokens never enter webpage content scripts.

Stop with Ctrl+C. Data stays in ignored `.local/postgres`; the extension queue survives restarts. Website sessions last seven days and paired extension access lasts thirty days. **Sign out** revokes the current website session; **Revoke all extensions** invalidates device access and unfinished approvals. Old developer-token identities are test data and are not merged into Google accounts.

Local ports: 3000, 4318, 4319 (simulated provider only), 11434 (AI), 5001 (optional plain translator), 55432. `compose.yaml` is an optional database configuration; do not run both databases on the same port.

Gemini cloud explanations are available with a server-only API key; see [Gemini setup](docs/GEMINI.md). Free-tier availability and quotas depend on the Google project.

## Verification

```bash
pnpm typecheck
pnpm test
pnpm exec playwright install chromium
pnpm test:e2e
pnpm test:ai:live
pnpm test:translation:live
pnpm test:popup
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
| [Local AI translation](docs/AI_TRANSLATION.md) | Six languages, automatic cards, examples, setup and model pin |
| [Plain free translation](docs/TRANSLATION.md) | Reproducible local setup, languages, usage and live regression suite |
| [Google login and pairing](docs/LOGIN.md) | Provider setup, sessions, pairing, and revocation |
| [Architecture](docs/ARCHITECTURE.md) | Stack, translation/dictionary services, data flow, and safeguards |
| [First coding plan](docs/superpowers/plans/2026-11-07-capture-to-library.md) | A bounded capture-to-library implementation with checks |

## Working principles

- After collection consent, automatically record each selection-translation query and derive vocabulary candidates; activation for scheduled review requires confirmation.
- Combine learner goals, inferred scenarios, and separate reading/listening/speaking evidence.
- Build independently. [FluentRead](https://github.com/FluentRead/FluentRead) is a functional reference, not a source of copied code or assets. This is not a legal clearance statement.

A distribution license is undecided. The diagram adapts a user-supplied layout reference; its ownership/permission and the ContextSpeak brand still need clearance before public release.

## Automated checks

[GitHub Actions](https://github.com/Elaine-tyl/contextspeak/actions) runs CI on pushes to `main` and pull requests: locked dependency install, Prisma client generation, formatting, typecheck, unit/integration tests, extension browser tests, popup tests and both builds. CI uses temporary databases, simulated login and translation fixtures; no Google or Gemini secrets are required. Live provider checks remain opt-in. Failed browser runs retain diagnostics for seven days. Browser screenshots are saved under Playwright’s portable test-output folders.
