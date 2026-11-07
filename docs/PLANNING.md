# Product plan

## MVP scope

| Surface | Included |
| --- | --- |
| Chrome extension | Contextual selection translation, saved expressions, minimal source context, retryable sync |
| Learning website | Goals, baseline assessment, personal library, daily tasks, listening, recorded conversation, progress |
| Account controls | Shared login, collection preferences, export, and deletion |

Initial proposal: adult learners, a small set of everyday/work scenarios, optional 10–15 minute sessions, explicit saving by default, and turn-based voice. Reviewed starter tasks support users with no saved expressions.

Deferred: native apps, school roles, broad crawling, background recording, full-duplex voice, and custom model training.

## Delivery milestones

| Milestone | Deliverable | Acceptance criteria |
| --- | --- | --- |
| M0 — Validate | First scenarios, clickable journey, speech feasibility | Learners understand the flow; measure speech quality, latency, and session cost; agree pilot thresholds. |
| M1 — Capture | Login, translation, library, sync | Saved expressions appear on the website; offline/repeated submission does not lose or duplicate acknowledged saves. |
| M2 — Practice | Listening, recorded responses, feedback, retry | Complete a session; recover from denied microphone access, upload failure, and provider errors. |
| M3 — Adapt | Skill estimates and scheduled reviews | Each update links to eligible evidence; hints, retries, and uncertainty affect eligibility correctly. |
| M4 — Pilot | Monitoring, privacy controls, release package | Verify user isolation, deletion, restore, usage limits, and end-to-end flow; review learning outcomes. |

M0 → M1 → M2 → M3 → M4. Assign owners and estimate effort after feasibility work; no delivery dates or budget are committed.

## Pilot measures

Use the [learning evaluation](METHODOLOGY.md#evaluation) for outcomes. Also track session completion/return, capture and upload success, p50/p95 latency, and cost per completed session. Set numerical thresholds before recruiting the pilot.

Cost estimate: active learners × sessions per learner × variable session cost, plus hosting, operations, and support. Five million users is a long-term ambition; capacity planning needs measured activity and concurrency.

## Decisions before implementation

- First three scenarios, release geography, and supported devices.
- Team capacity, budget, timeline, pricing, and usage caps.
- Authentication, model, speech, and hosting providers.
- Audio retention, deletion turnaround, backup expiry, and consent wording.
- Pilot sample, success thresholds, human review capacity, and distribution license.
