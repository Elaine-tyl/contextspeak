# Harder Missing Words Implementation Plan

> **For agentic workers:** Execute inline with superpowers:executing-plans.
> One scoped independent final review. Keep unrelated dirty work unchanged.

**Goal:** Harder listening gaps, original academic practice and credited news.

**Architecture:** Versioned active/archive catalog; shared gap builder; reviewed
VOA corpus templates; existing scoring, levels, caching and source selectors.

**Tech Stack:** TypeScript/React/NestJS/Prisma, no dependencies or migrations.

**Spec:** `docs/superpowers/specs/2026-11-11-harder-missing-words-design.md`

## Global constraints

English UI. Original academic questions; official IELTS content linked only.
Forty v2 originals across ten levels, six reviewed news gaps. Preserve v1 IDs.
No cloud setup, RAG, runtime crawling, database migration, commit or push.

## Review focus

- Old runs/replays keep their original text, answers and earned progress.
- New starts and Retry must select current questions and valid filters.
- Unsupported news URLs, changed authorship or mismatched templates fail review.
- Source credit/title/date and resource links must not expose hidden answers.
- Six blanks, error feedback and retries remain usable on narrow screens.

### Task 1: Upgrade the existing mode and its sources

**Files:** game gap/legacy/catalog/levels modules; corpus validation/data and open
adapter; contracts; Games UI; unit/API/browser tests; existing game/source docs.

**Interfaces:** `activeChallenges` supplies catalog/new runs; `challenges`
retains archives. Public `archived?: boolean` and `source.publishedAt?: string`.
`makeGapFill(template,id,level,topic,source?)` builds originals/credited gaps.
Corpus optional `gapTemplate` must reconstruct its hashed excerpt exactly.

- [x] Write failing difficulty/archive/provenance tests before implementing.
- [x] Add v2 originals, preserve v1 archive, wire active selection; test old runs
  against literal v1 answers while new runs contain only v2/current content.
- [x] Verify and add six VOA excerpts, template/hash/date/author checks and live
  source verification; add external official-study links and edition notice.
- [x] Update browser regression fixtures to current questions; verify news and
  academic filtering, six blanks/mobile, old-run compatibility and Retry/Next.
- [x] Run full tests, Games browser suite, typecheck/format/build; independent
  source review and fix material findings with regression tests. Show fresh run.
