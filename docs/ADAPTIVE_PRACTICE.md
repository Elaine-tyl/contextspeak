# Adaptive practice and speaking rubric v2

No RAG, vector database or new model call is used to select practice. Owned PostgreSQL history drives transparent rules; existing JSON snapshots preserve the question, difficulty, criteria and assessment version.

## Questions

Vocabulary speaking uses a saved multiword example as context when possible. Otherwise a small original topic bank covers planning, study and asking for help, with a meaning-and-example fallback. Each task states its requirements: a relevant sentence at Level 1; a reason and follow-up at Level 2; comparison and justification at Level 3. Long expressions are referred to as the saved expression rather than quoted in a misleading truncated form. Prompts fit the existing 600-character speech limit. Role-play and game prompts also show explicit criteria; the server passes the same criteria and saved sense to assessment.

The browser submits the shown difficulty (1–3). The server reconstructs that version rather than substituting a newer recommendation. Dialogue cannot silently change difficulty midway. Completed retries return their original stored result.

## Speaking feedback

Rubric v2 requests audible evidence and anchored bands: 0–39 major difficulty or task not addressed; 40–59 partial response; 60–79 understandable with gaps; 80–89 clear and mostly complete; 90–100 strong evidence meeting the task. Natural accents, verbosity and confidence alone do not earn marks.

The model supplies audio quality and one evidence entry per dimension. Text-dimension quotations must occur exactly in the transcript. Fabricated/duplicate evidence is rejected for a scored response. Low-quality clips retain coaching but no numeric mark or progression effect; invalid quoted evidence is not displayed. “Why these scores?” exposes the evidence. Grammar/vocabulary correction spans and IPA retain their existing grounding/reference safeguards.

Server weights: clarity 20%, fluency 20%, grammar 15%, vocabulary 15%, task response 30%. Overall is capped at taskResponse + 20, so fluent unrelated answers cannot pass through averaging alone. Games retain their additional equal weighting of overall and task response. These are practice estimates, not calibrated exam/CEFR grades or phoneme-level pronunciation measurements. Independent human-rated recordings are still required to measure empirical accuracy, accent fairness and calibration. Legacy v1 results remain readable but do not promote speaking difficulty under v2.

## Recommendation policy

Listening and speaking are separate. Use the last 30 days, with bounded SQL reads for both recent valid results and independent evidence (up to 30 rows of each per source). Noise and retries cannot push qualifications out of the sampled history. Game marks and actual dictation/AI results are used; self-check labels and translation activity are not scores.

- Recommend a higher level only after three different independent 80+ tasks at every prior level. One high score or repeated attempts at one answer cannot promote.
- If at least two of the last three different usable tasks are below 55, recommend one easier level. Unscorable audio is filtered before sampling and never counts as failure.
- Independent evidence requires no answer reveal and no scored attempt at the same exercise in the preceding 24 hours. A delayed unassisted retry can requalify, avoiding permanent first-attempt lockout. Reading a role-play question is allowed; later coached dialogue turns are not promotion evidence. Dictation with more than two plays is treated as assisted.
- Stable normalized-expression/scenario/difficulty identities prevent capitalization, translation-target changes or repeat lookups from creating new exercises.
- Show a weak speaking dimension only after multiple usable results support it. Preserve earned game unlocks and manual selection. Game recommendations are capped by earned access; returning to games uses the latest recommendation after progress refresh.

No new migration is required. Stored independence is decided inside the existing user-row transaction lock, not supplied by the client. Learner history stays on the server; Gemini receives only the current task/criteria/saved sense and the existing bounded dialogue context, not the full history.

## Verification

Deterministic regressions cover advancement thresholds, different-task identity, assisted repeats, delayed requalification, age/window displacement, many unusable recordings, owner separation, relevance ceilings, audio-quality withholding, evidence grounding and frozen question difficulty. Browser tests cover recommendation/manual-level controls, criteria and automatic feedback. Public synthetic relevant/unrelated speech is used for live integration checks; it is not a validation study of learner voices.
