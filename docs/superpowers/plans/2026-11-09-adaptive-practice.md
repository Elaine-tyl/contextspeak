# Question quality, evidence-based speaking feedback and adaptive practice

Implement the user's requested improvements directly without RAG or new dependencies.

- [x] Pure performance policy: separate skills; three distinct independent 80+ tasks at each prior level before recommending higher work; at least two low results among three recent distinct tasks recommends easier practice; noise/unscorable results and self-checks cannot promote or demote. Recommend a weak speaking dimension only with multiple usable results. Preserve earned game unlocks and manual choice.
- [x] Contextual vocabulary questions at three levels, using saved examples when available and curated topic prompts otherwise. Show explicit success criteria. Freeze selected difficulty in the submitted task; server reconstructs the same question instead of grading against a changed recommendation.
- [x] Speaking rubric v2: anchored bands, five evidence entries with grounded transcript quotations for text dimensions, audio-quality handling, server-side weighted score and relevance ceiling. Limited/unusable evidence gives feedback without a numeric score. Existing v1 history stays readable; do not claim validated pronunciation or mastery.
- [x] Read bounded owned SQL history and store independent-attempt flags in existing JSON snapshots under the user transaction lock. Game recommendations stay within earned unlocks; ordinary vocabulary practice uses the recommended difficulty. No migration or external learner-history retrieval.
- [x] UI: question criteria, transparent recommendation/reason, manual game-level override, per-dimension evidence and limited-audio explanation. Keep automatic speaking, IPA, cached local voices and retry/idempotency behavior.
- [x] Regression tests for exact advancement thresholds, repeated/assisted/uncertain answers, lowering difficulty, owner separation, frozen-question consistency, invalid evidence and off-topic caps. Run checks/browser/build, review, commit locally and restart.

Feedback is an AI estimate. This change improves guardrails and consistency; empirical scoring accuracy still needs independent human-labelled evaluation. No per-phoneme pronunciation diagnosis or CEFR claim. Keep code in focused small modules, comments for non-obvious rules only.

## Verification

- 189 tests across 39 files passed; typecheck, formatting and diff checks passed.
- 48 browser regression tests passed, including recommendations, manual difficulty choice and limited-audio feedback. Extension and production web builds passed.
- Review findings were fixed with failing-then-passing regressions: retries/noise cannot evict independent qualification evidence, and delayed attempts can qualify again after 24 hours. Normalized expression/scenario/difficulty keys prevent capitalization or target-language changes from duplicating evidence.
- A live public synthetic-audio check returned five rubric evidence entries and capped an irrelevant response at 40 despite fluent language. This checks integration, not agreement with human ratings.
- No RAG, dependency additions or database migrations. Independent human-labelled scoring evaluation remains future work.
