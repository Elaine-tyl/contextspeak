# Automatic speaking questions and feedback

Implement the user's explicit requested flow: show and play a question based on saved vocabulary, record a spoken answer, assess automatically when the recording completes. No submit or agreement button. Failed assessment has an explicit retry preserving its event ID; never automatically loop on quota failures. Reuse the existing audio endpoint, IPA feedback, ownership and bounded uploads.

Provide a deterministic vocabulary question without an extra model call; subsequent conversation turns remain AI-generated. Keep listening transcripts unchanged. Server and browser must use the same speaking question. Debounce the initial effect until after React development cleanup; cancel it on unmount and submit each recording once. Preserve local self-checks and recording cleanup.

Verification: add/update provider/API and browser regressions for automatic assessment, exact-once retries and question consistency; run unit/API suite, practice browser tests, typecheck and build. Commit locally and restart.

Verified: 155 unit/API tests and all 17 practice browser tests passed. Added API assertions that listening text stays intact while the speaking assessment receives the generated vocabulary question. Browser regression covers one automatic upload after recording, no feedback button or replay requirement, no automatic retry loop and identical-event manual retries. Typecheck, formatting, diff check and production build verified before commit.
