# Direct speaking feedback and IPA guidance

Implement the requested change directly: remove the agreement checkbox and mandatory replay for AI assessment. Keep a deliberate Get AI feedback click after recording; recording alone never uploads audio. Send the actual replay flag, no fabricated consent. Accept legacy consent:true metadata optionally for old clients. Self-check replay requirements stay unchanged.

Add bounded optional pronunciation guidance to the existing model/result schema: word, target IPA, stress explanation and articulation tip. Only include words actually in the transcript or saved expression. Label IPA as AI reference guidance, not a measured transcription of the learner's sounds. Silence produces no guidance. Preserve saved old results and all retry/idempotency/ownership behavior.

- [x] Update API/provider and browser regressions for direct submit and IPA rendering, including silence/ungrounded word filtering.
- [x] Implement schema, provider prompt/filter and compact UI; update obsolete documentation and remove unused checkbox styles.
- [x] Run unit/API tests, practice browser tests, typecheck, formatting and web build; save a local commit and restart the app.

Verification: 155 unit/API tests, all 17 practice browser regressions, TypeScript, formatting and diff checks passed. Production web build verified before commit. Live Gemini integration returned grounded IPA/stress guidance from an existing public synthetic recording; no personal audio uploaded. IPA remains reference coaching, not validated phoneme measurement.
