# Inspectable personalization

Complete M3 for the confirmed website/extension MVP without RAG. Use explicit goals, optional AI scenario suggestions from ten distinct authorized English excerpts, and deterministic inspectable priorities. Suggestions never overwrite goals; learner can accept/dismiss/reset. Reject unsupported scenarios or evidence IDs. No full browsing history or page URL is sent to a model. Cache unchanged batches, bound model calls and recheck user/consent before saving.

A separately licensed CC BY-SA 4.0 FrequencyWords/OpenSubtitles 2018 snapshot retains original counts/ranks from the top 5,000 rows (non-word tokens filtered). Pin commit, attribution, change notice and integrity hash; display subtitle-domain provenance and unknown values rather than universal-frequency claims. No new dependency. Priority v1 combines confirmed-goal keyword clues, personal encounters, available subtitle-frequency evidence, reliable item-linked practice gaps and due reviews; normalize missing signals explicitly. Display components and practice links. Never infer mastery from lookup counts.

Persist learner profile JSON on User; use an additive migration shared with independent deletion ledger work. Add owned APIs and a dashboard personalization panel. Validate pure policy, ownership, sparse/duplicate evidence, confirmations, stale model replies, frequency integrity and quota/error behavior. Integrate with privacy/readiness work only after focused tests pass.

## Completed implementation and verification

Added the owned service/controller, optional Gemini adapter, confirmed-goal controls, hashed context snapshots and priority inspector. Account/privacy and pilot-control changes were independently implemented/reviewed and integrated through shared services. The context snapshot preserves new excerpts captured during generation. Goal drafts survive unchanged refreshes, while successful reset/save synchronizes the returned profile. Analysis uses a 60-second client deadline for the 45-second provider bound.

Regression evidence: 255 tests across 55 files; 57 browser flows plus five final focused dashboard/personalization checks; types, formatting, website/extension builds and upstream frequency reproduction. Local implementation is complete; human calibration, real-device/provider trials and deployment/recovery remain explicit external gates in MVP_CHECKLIST.md.
