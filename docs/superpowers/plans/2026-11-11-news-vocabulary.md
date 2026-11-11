# News Vocabulary Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans inline. One scoped
> independent final review; preserve unrelated dirty work.

**Goal:** Intermediate–advanced target words and varied reusable news.
**Architecture:** v3 active/v1+v2 archived templates; reviewed-news manifest;
existing corpus builder, grading, filters and speech cache.
**Tech Stack:** TypeScript/NestJS/React, existing dependencies.
**Spec:** docs/superpowers/specs/2026-11-11-news-vocabulary-design.md

## Global constraints

English UI/docs; ten levels; 2–6 blanks; v1/v2 immutable; twelve new news excerpts;
BBC external links only; no dependencies, migration, provider setup, commit/push.

## Review focus

- Existing v2 runs and saved feedback preserve text/answers/unlocks.
- New starts/Retry select v3 with valid existing theme filters.
- News provenance rejects changed author/date/URL/byline and mismatched gaps.
- Source attribution is distinct from syndicated photo credit and excluded quotes.
- New words fit inline/mobile inputs and feedback; BBC links expose no answers.

### Task 1: Upgrade vocabulary and curated news

Files: gap-fill/legacy templates and catalog tests; new content/reviewed-news.ts
manifest, content validation/corpus/tests; GapFill UI, API/browser regression
fixtures, README and existing game/source documentation.

- [x] Write failing v3/archive/news topic and manifest-policy tests.
- [x] Add forty v3 templates, preserve v2 in archive and adapt regression fixtures.
- [x] Import twelve verified news excerpts and pin their staff attribution.
- [x] Add difficulty guide/BBC resources; verify source filtering and mobile layout.
- [x] Run full tests, Games browser suite, typecheck/format/build, source check and
      independent review; fix material findings with regressions, restore app.
