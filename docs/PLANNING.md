# ContextSpeak — Product and delivery plan

Date: 2026-11-07
Status: proposed planning baseline; estimates and technical decisions require validation.

## 1. Confirmed requirements

- Serve learners with an existing English foundation who want better listening comprehension and fluent conversation.
- Deliver a browser extension and a learning website first.
- Build an independent product; refer to FluentRead without copying it.
- Maintain a personal library of encountered words, meanings, phrases, and expressions.
- Adapt practice to goals, usage context, and demonstrated knowledge.
- Generate listening and speaking scenarios and update knowledge estimates from practice.
- Preserve a future path toward native apps and school use.

The earlier five-million-user figure is a long-term ambition, not an MVP traffic forecast or evidence of current demand.

## 2. Proposed defaults

These are planning recommendations, not user-approved commitments:

- Adults first; English learning only in the MVP.
- Begin with a small, reviewed set of everyday and work-social scenarios; confirm the first three before implementation.
- Responsive website for desktop and mobile browsers, with Chrome desktop as the first extension target.
- Approximately 10–15 minutes per daily session, adjustable by the learner.
- Explicit save by default; offer opt-in automatic collection with pause, site exclusions, and deletion controls.
- Turn-based recorded speech before full-duplex voice.
- Private pilot before general release.

## 3. Problem and outcome

Users often recognize written vocabulary but struggle to decode connected speech or retrieve an appropriate expression during conversation. A useful product must bridge recognition, listening, and independent use.

Primary outcome: a learner can complete an unfamiliar but level-matched spoken task in a chosen scenario with less assistance over time.

Do not equate progress with words saved, minutes played, self-reported confidence, or repeated success on memorized questions alone.

## 4. Core journey

1. Choose a goal, scenario interests, and daily time budget.
2. Complete a short baseline listening and speaking assessment; allow skip with conservative defaults.
3. Select text in a supported webpage, see a contextual explanation, and save useful material.
4. Open the website to receive a task combining due reviews, goals, and weak skills.
5. Listen, demonstrate understanding, record a response, receive limited actionable feedback, and retry.
6. Revisit the expression later in a different context with reduced support.

No saved words must not prevent practice: reviewed starter content supplies an initial path. No new browsing activity must not stop future sessions.

## 5. MVP requirements and acceptance criteria

| ID | Requirement | Acceptance criteria |
| --- | --- | --- |
| P01 | Account and extension pairing | Web and extension resolve to the same learner; expired sessions can be renewed without silently losing unsynchronized saves. |
| P02 | Goals and baseline | Persist goals and baseline evidence; skipped or failed assessment uses conservative defaults, not invented proficiency. |
| P03 | Contextual explanation | For supported ordinary webpages, explain the selected text in its local context; show retry/fallback on provider failure. |
| P04 | Personal library | Store a specific sense or expression with a minimal excerpt; allow edit, remove, and manual additions. |
| P05 | Collection and sync | Save locally before sending; replay the same event without duplicate records; clearly show pending/failed sync. |
| P06 | Daily planning | Explain why an item was selected; respect a session budget; generate tasks when the library is empty. |
| P07 | Listening practice | Record whether subtitles, replays, and hints were used; score comprehension against a task objective. |
| P08 | Spoken interaction | User starts/stops recording; can replay or retry; denied microphone permission and failed uploads have recoverable states. |
| P09 | Feedback | Give a small number of actionable corrections; uncertainty or unreliable transcription does not automatically reduce skill estimates. |
| P10 | Review and skill state | Separate recognition, listening, and production; persist assessment version and evidence; avoid counting retries as independent new evidence. |
| P11 | Privacy controls | User can pause collection, exclude sites, export data, and request deletion; deletion covers active database rows, audio, derived vectors if added, and queued work. |
| P12 | Operational controls | Enforce per-user request/usage limits; expose failed jobs to operators; never log tokens or raw audio in application logs. |

## 6. Outside MVP

- Native iOS/Android apps and desktop-wide selection translation.
- Teacher dashboards, class management, curriculum assignment, and minors-specific onboarding.
- Broad crawling, complete browsing-history analysis, background microphone capture.
- Full-duplex calls, pronunciation certification, examination certification, or guaranteed fluency claims.
- Fine-tuning, custom speech models, autonomous agent orchestration, and a standalone vector database.

## 7. Delivery milestones

The milestones define sequencing and exit conditions, not promised dates. Assign named owners and estimate effort after the feasibility work.

| Milestone | Deliverables | Exit condition | Depends on |
| --- | --- | --- | --- |
| M0 — Validate | Scenario selection, clickable journey, speech/provider experiment, privacy decisions, pilot rubric | Target learners can follow the journey; speech quality, latency, and costs measured on representative samples; pilot thresholds agreed | Product owner and initial user access |
| M1 — Capture | Auth/pairing, selection explanation, expression library, local outbox and synchronization | Same expression is visible on the website; interruption/retry does not duplicate or lose acknowledged saves | M0 and auth/provider choices |
| M2 — Practice | Goal-based planner, listening tasks, turn-based recording, feedback/retry | One complete listen/respond/feedback session succeeds; failure and microphone-denial paths work | M1 and audio feasibility |
| M3 — Adapt | Evidence records, separate skill estimates, spaced review, delayed transfer tasks | A reproducible record explains each update; hint/retry effects and low-confidence exclusions are verified | M2 and evaluation rubric |
| M4 — Pilot | Monitoring, export/deletion, restore rehearsal, browser packaging, cost limits, pilot readout | Release checklist passes; pilot evidence supports continue/change/stop decision | M1–M3 |

Parallel preparation can cover reviewed content and UX while backend work proceeds, but security and data rules must be agreed before collecting pilot recordings.

## 8. Workstreams and responsibilities

| Workstream | Responsibilities |
| --- | --- |
| Product/learning design | Scenarios, task rubrics, acceptance criteria, interviews, pilot interpretation |
| Frontend/extension | Permission flow, capture, website, recording UX, accessibility, browser checks |
| Backend/platform | Auth, ownership isolation, sync, storage, jobs, deployment and recovery |
| AI/evaluation | Provider comparison, constrained generation, content validation, scoring calibration |
| QA/operations | Failure tests, end-to-end checks, monitoring, restore and release rehearsal |

One person may own several workstreams; do not assume a staffing count or fixed delivery time until available capacity is known.

## 9. Evaluation and release gates

Define numerical targets and a pilot sample size before M4. The following are measures, not claims of established performance:

- Learning: baseline versus delayed unseen listening tasks; independently completed scenario tasks; assistance required; delayed expression recall.
- Engagement: first completed session, return to meaningful practice, and completion of due practice.
- Quality: human-reviewed content correctness, assessment agreement, and uncertainty rates across accents and recording conditions.
- Reliability: successful capture-to-sync, recording/upload completion, provider error rates, and p50/p95 interaction latency.
- Economics: cost per completed session, per active learner, and per speech minute.

Use unseen parallel tasks and human sampling. Repeated task scores alone cannot establish transfer; a pilot without a comparison design cannot establish causal learning benefit.

Before pilot release: verify ownership isolation, retry behavior, data deletion, microphone denial, provider outage behavior, cost caps, accessible core navigation, and backup restoration. Set measured service objectives before rollout instead of claiming that the system already meets them.

## 10. Cost and scale planning

Monthly variable cost should be estimated from observed usage:

`active learners × sessions per learner × (text generation + speech recognition + speech synthesis + storage/egress per session)`

Add database, queue, monitoring, hosting, support, and recovery costs. Record provider usage and cost attribution without exposing private content in telemetry. Benchmark actual session lengths and retry rates; user count alone does not determine capacity.

## 11. Risks and mitigations

| Risk | Response |
| --- | --- |
| Browsing topics do not match speaking goals | Use declared goals and baseline assessment; allow manual expression input. |
| ASR errors become incorrect learner judgments | Preserve uncertainty, allow transcript correction, and exclude unreliable attempts. |
| Generated content contains wrong answers or unnatural language | Schema/rubric checks, reviewed starter tasks, human sampling, fallback content. |
| Review backlog overwhelms the learner | Limit active items and session budget; separate archive from scheduled learning. |
| Similar words confuse rather than help | Offer selective contrast after context is understood; do not auto-enroll all suggestions. |
| Voice costs or latency make sessions impractical | Measure early, cap session duration, reuse approved generic audio, and monitor usage. |
| Extension is suspended or loses connectivity | Durable local outbox and idempotent server processing. |
| Sensitive page content is collected | Minimum context, explicit controls, excluded surfaces, no whole-page or history capture by default. |

## 12. Decisions to resolve

- First three scenarios and first release geography.
- Exact learner onboarding language and supported browser/device matrix.
- Team capacity, budget, and delivery expectations.
- Auth, model, speech, hosting, and database providers.
- Audio retention, deletion turnaround, backup expiry, and consent wording.
- Session limits, pricing, and cost ceilings.
- Numerical pilot gates, human review resources, and comparison design.
- Product distribution license and dependency/content review policy.

## 13. Immediate next work

1. Confirm the decisions necessary for M0.
2. Prototype onboarding → capture → daily task → feedback.
3. Run speech and assessment feasibility experiments.
4. Turn MVP requirements into sized engineering tasks with named owners.
5. Review the design before building the production application.
