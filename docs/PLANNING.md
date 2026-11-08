# Product plan

## Requirements mapping

| Client requirement | MVP response |
| --- | --- |
| Automatic personal vocabulary memory | After consent, every selection-translation query enters history and produces a vocabulary candidate; confirmed items enter scheduled review. |
| Infer language-use scenarios | Suggest scenarios from authorized lookup context, with evidence and learner correction; no full browsing-history collection. |
| Prioritize high-frequency words | Rank useful learning items using a sourced frequency signal, personal context, goals, and reliable skill gaps. |
| Know what the learner has mastered | Separate reading, listening, and independent speaking estimates with traceable practice evidence. |
| Expand vocabulary during lookup | Show up to two useful near-synonyms and two spelling lookalikes asynchronously, with differences and examples where appropriate. |
| Generate listening/speaking practice | Use confirmed vocabulary and learner scenarios for level-matched tasks and delayed transfer. |
| Extension, PC, and mobile | Chrome desktop extension plus responsive website on PC/mobile; native apps follow usage validation. |
| Multiple languages and school roles | Adult English is the first segment; multilingual and teacher/student support are later phases. |

Automatic history/candidates versus confirmed review enrollment is a proposed compromise to validate before pilot. Do not silently treat the narrower first segment as the complete client deliverable.

## Delivery roadmap

| Stage | Deliverable and acceptance |
| --- | --- |
| M0 — Validate | Confirm first three scenarios, collection consent, speech quality, provider costs, and pilot gates. Prototype the journey. |
| M1 — Capture and remember | Login, selection translation/expansion, automatic history/candidates, library, and durable sync; verify no duplicate events or lost acknowledged queries. |
| M2 — Review and practice | Confirm active items; FSRS-based explicit recall review; listening and recorded responses; recover from microphone, upload, and provider failures. |
| M3 — Personalize | History-based scenario suggestions, sourced word priorities, and skill updates; learner can inspect/correct suggestions; low-confidence/assisted attempts do not inflate mastery. |
| M4 — Pilot | Export/deletion, usage limits, monitoring, restore rehearsal, responsive-device checks, and held-out learning evaluation. |
| P2 — Expand access | Evaluate installable PWA/offline support and native mobile apps; desktop-native capture only if demand justifies it. |
| P3 — Education and languages | Teacher/student permissions, class goals, approved curriculum content, minors safeguards, and additional languages. |

M0–M4 are ordered milestones, not promised dates. P2/P3 require separate scoping and budget. Broad crawling, background recording, full-duplex voice, and custom model training remain outside MVP.

## Illustrative monthly costs

**Assumptions, not vendor quotes:** 30 active days; each DAU consumes 10,000 input and 4,400 output text tokens daily. Economy rates are assumed at $0.10/$0.40 per million input/output tokens; premium at $2.50/$7.50. No caching or batching savings assumed.

| Daily active users | All economy text | All premium text | Illustrative speech addition |
| --- | --- | --- | --- |
| 100 | $8.28 | $174 | $315 |
| 10,000 | $828 | $17,400 | $31,500 |
| 1,000,000 | $82,800 | $1,740,000 | $3,150,000 |

Speech assumption: 5 recognized minutes at $0.006/minute plus 5 generated minutes at an equivalent $0.015/minute per DAU/day; actual billing units vary. Text cost = DAU × 30 × (0.01 × input rate + 0.0044 × output rate). Speech cost = DAU × 30 × $0.105.

Excluded: translation API charges, dictionary/content licenses, hosting, storage/egress, operations/support, taxes, retries, and separate onboarding/batch analysis. Five million registered users does not imply one million DAU. Replace assumptions with dated provider quotes and measured usage before budgeting. Route routine work to lower-cost services and reserve stronger models for difficult assessments.

## Pilot and open decisions

Use [held-out learning evaluation](METHODOLOGY.md#evaluation), plus session return/completion, sync/upload reliability, p50/p95 latency, and completed-session cost. Set numerical gates and sample size before recruitment.

Before external pilot: confirm release geography, first scenarios, team/budget/timeline, identity provider, dictionary/frequency permissions, translation/model/speech providers, retention/deletion/backup periods, commercial license/brand clearance, and quotas. Framework-only local development and synthetic provider tests can begin before these purchasing decisions.

**Start coding:** implement the [capture-to-library slice](superpowers/plans/2026-11-07-capture-to-library.md) first; add recall review second, then voice and inferred scenarios.

## Implementation status

Implemented on `capture-to-library`: Google login and separate extension pairing/revocation; automatic selection translation with seven language choices; shared AI explanations and bilingual examples; immediate capture, durable sync and owned history/candidates; translation caching; compact searchable library cards and full-detail pages; explicit FSRS review enrollment, recall ratings and pause/resume; and on-device listening plus local speaking recording/replay with separate saved self-checks. The practice hub includes curated English introductions, making-plans and asking-for-help role-plays using a chosen saved expression. Empty review queues check for newly due cards automatically. Local Google/Gemini setup has been smoke-tested; fixtures keep regression tests reproducible without credentials or quota.

M1, the recall portion of M2 and guided listening/speaking practice are usable locally. M1 synonym/lookalike expansion remains pending. The next product slice is dynamically generated, level-matched scenarios and a separately evaluated transcription/feedback provider; scenario inference, sourced priorities, independent skill assessment and pilot hardening remain planned. No listening/speaking mastery claims are implemented. Deployment, provider quotas and release/privacy decisions still need validation before an external pilot. See the root README for setup and verified commands.
