# Learning methodology

## Collection and vocabulary memory

After explicit onboarding consent, automatically log selection-translation queries with bounded context; users can pause, exclude sites, or delete records. Translation still works when collection is off. Repeated words remain separate encounters, while normalized sense/expression candidates share a library item. Ambiguous senses remain unresolved rather than silently merged.

Keep three states: query history → vocabulary candidate → confirmed active review item. Automatic collection does not mean every lookup becomes a mandatory review. Learners can archive, activate, or defer items; reviewed starter content supports an empty library.

## Scenario inference and priorities

Analyze only authorized lookup excerpts and practice, not complete browsing history. The implemented optional analysis runs only on learner request after ten distinct new English contexts; sparse evidence displays an insufficient-data state. This threshold remains tunable. Return scenario label, confidence, and supporting encounter IDs. Suggestions require confirmation and cannot override declared goals; users can reject/reset them.

Normalize each score to [0,1] and start with this explainable ranking:

`priority = 0.30 goal keyword relevance + 0.25 personal encounter frequency + 0.20 subtitle-domain frequency + 0.15 reliable practice gap + 0.10 review urgency`

The implemented snapshot, missing-signal normalization, evidence rules and ownership limits are documented in [Personalization](PERSONALIZATION.md). Subtitle counts are not universal frequency; the model never supplies invented frequency data.

Use capped log-scaled eligible encounter counts for personal frequency; unknown skill is neutral (0.5). Review urgency comes from the scheduler, and zero is used for unscheduled items. Exclude ignored/archived items before ranking. Missing general frequency is marked unavailable and remaining weights are renormalized; do not invent a model-based frequency value. These starting weights require pilot calibration.

General frequency needs a versioned source and redistribution permissions. Candidate sources include SUBTLEX-US or wordfreq; neither is approved for bundling yet. Development uses explicitly synthetic fixture scores. Separate corpus/phrase frequency from personal recurrence; infrequent but goal-critical expressions may still rank highly.

## Lookup-time expansion

For each lookup, request up to two context-relevant near-synonyms and two spelling lookalikes in the same panel, without delaying the base translation. Show a short usage contrast and example. Phrase/nonword lookups may have no sensible spelling alternatives; show fewer rather than inventing them. Frequency badges require source evidence. Suggestions enter scheduled review only after confirmation.

## Review and listening/speaking cycle

1. Set goals and complete an optional baseline; skipped assessment uses conservative defaults.
2. Activate useful vocabulary; apply FSRS to explicit recall ratings (Again, Hard, Good, Easy), with default parameters until sufficient history exists.
3. Combine due items and goal-relevant expressions into a bounded daily scenario task.
4. Listen and demonstrate comprehension, then record an independent response.
5. Receive focused feedback, retry with less assistance, and later test a different scenario or speaker.

Example: `Are you available for a quick call?` becomes a meeting-time negotiation, then a later restaurant-reservation task. The plan continues without fresh browsing activity. Speaking-transfer selection is separate from FSRS recall ratings.

## Mastery evidence

| Skill | Evidence |
| --- | --- |
| Reading | Understand a specific sense in an unseen sentence. |
| Listening | Understand speech while tracking subtitle, hint, and replay use. |
| Speaking | Communicate the intended meaning independently in a new situation. |

A lookup is only a signal; shadowing or answer-assisted retries are not independent speaking tests. Track task version/difficulty, confidence, assistance, and retry relationships. Unreliable transcription prompts confirmation/re-recording rather than a penalty; corrected transcripts create a new assessment. Communication and intelligibility matter more than accent imitation. Do not map open-ended AI scores directly to FSRS ratings.

## Evaluation

Compare baseline with delayed, unseen, level-matched tasks. Measure comprehension, independent completion, assistance, and retention. Human-review AI assessments across accents/noise; keep held-out content out of generation. Evaluate scenario-suggestion acceptance/corrections and priority relevance. Use a comparison design before claiming causal improvement.

## References

- [Task-based language teaching](https://www.cambridge.org/core/elements/taskbased-language-teaching/395B3D3B0F7078DF325579CC8314E38B)
- [Spacing and retrieval](https://doi.org/10.1038/s44159-022-00089-1)
- [TypeScript FSRS implementation](https://github.com/open-spaced-repetition/ts-fsrs)
