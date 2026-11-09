# Guided dialogue and correction highlights Implementation Plan

> Execute directly in this existing isolated worktree, as requested by the user.

**Goal:** Four-turn everyday English role-play using saved vocabulary, AI follow-ups and clear listening/speaking corrections.
**Architecture:** Reuse recording, AI speech, consent-gated speaking assessment and existing practice JSON snapshots. Store a server-validated previous-turn chain; derive prompts/history from owned saved turns, never accept client conversation text as authority. Keep FSRS unchanged.
**Tech stack:** Existing TypeScript, NestJS, Next.js, Prisma, Gemini, Vitest and Playwright. No new dependencies.

## Constraints and review focus
- Explicit consent for each audio upload; include disclosure that earlier dialogue text is sent with subsequent turns.
- Four scored turns maximum. Silence does not advance the dialogue. Existing single-turn speaking and dictation remain available.
- Check ownership, lookup, scenario and chain length before provider calls. Completed retries stay idempotent; no raw audio persistence.
- Listening uses sequence alignment so repeated tokens, omissions and insertions are correct. Colour also has text labels.
- Speaking corrections quote exact unique transcript phrases; discard invented/ambiguous spans. Do not compare a free reply against a model answer or claim pronunciation certainty.
- Provider quota failures keep the same recording retryable. Each turn and summary are practice estimates, not mastery.

## Tasks
- [x] Add alignment and correction contracts with failing regressions; implement shared aligned tokens and safe correction ranges.
- [x] Extend speaking provider and service with validated dialogue chain, follow-up generation and persisted turn metadata. Add provider/API regressions for ownership, silence, four-turn boundary and retries.
- [x] Add correction highlights, dialogue mode, per-turn audio/recording flow, feedback history and end summary. Add browser regressions for turn progression, fresh consent, corrections and existing modes.
- [x] Update practice docs, run typecheck/tests/browser/build/format verification and commit locally.

## Verification record

- Alignment and safe-range tests observed failing before implementation, then green. Word-boundary/no-op correction regression also observed red then green.
- 154 unit/API tests in 31 files passed; typecheck, formatting and diff checks passed.
- Full 42-test browser suite passed; after boundary hardening and compact feedback sections, all 17 practice tests passed, including the added mid-conversation silence/re-record case.
- Extension production build passed. Final web production build verified before commit.
- Live Gemini smoke: existing public synthetic English recording produced its actual transcript and a context-appropriate follow-up about meeting at the library café; no personal audio uploaded. This verifies integration, not scoring validity.
- Independent review found no important regressions; its partial-word highlighting concern was fixed with a regression, and missing mid-conversation silence coverage was added.
- Local decisions: four scored turns; English conversations; previous feedback collapses while continuing; active conversation resets on refresh while individual results persist. Streaming speech and validated pronunciation remain out of scope.
