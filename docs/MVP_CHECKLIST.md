# Current MVP completion checklist

Scope confirmed by the user: Chrome extension and learning website; school roles and native apps are later projects. Updated 2026-11-10. This checklist distinguishes shipped code from real-world release validation.

## Implemented locally

- [x] Google login, extension pairing/revocation and consent-aware capture.
- [x] Shared automatic translation, language settings, examples and owned vocabulary library.
- [x] FSRS reviews, audio caching/local voices, assessed listening/speaking and guided role-play.
- [x] Seven game modes, ten levels each, reviewed sources, scored level runs and Retry/Next.
- [x] Adaptive difficulty and evidence-based scoring safeguards.
- [x] Learning progress dashboard: owned review totals, scored listening/speaking, per-mode levels, evidence-based focus, refresh/error/session states and actionable practice links.

## Remaining implementation

- [ ] Contextual synonym/spelling expansion without blocking the initial saved translation.
- [ ] Evidence-linked scenario suggestions and learner-confirmed goals.
- [ ] Inspectable word priorities with honest frequency provenance; missing global-frequency data must remain explicit.
- [ ] Owned data export and account deletion with session/media cleanup and late-work regression checks.
- [ ] Pilot readiness checks: production configuration guards, health/operations guidance, restore/deletion rehearsal and responsive-device checks.

## External validation gates

These cannot be marked complete with synthetic tests: held-out human-rated speech/learning evaluation; actual pilot recruitment and success gates; selected production host/domain and release geography; measured model quota/cost and retention/backup policy. Implementation can prepare the controls and evaluation harness, but deployment and empirical validation need the corresponding environment/evidence.

No RAG, broad crawling, full-duplex voice, custom training, real-player matchmaking, school/minor roles or native mobile apps are required for this MVP.
