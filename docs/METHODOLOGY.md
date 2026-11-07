# ContextSpeak — Learning methodology

Status: proposed methodology to validate with learners; not a claim of proven product effectiveness.

## 1. Principle

Goals determine direction, everyday encounters supply useful material, and practice provides evidence. The system combines a small structured scenario curriculum with personal expressions, rather than relying on either random vocabulary collection or unrestricted AI chat.

This is a proposed blend of task-based interaction, retrieval practice, spacing, and delayed transfer. Task-based teaching research discusses input, interaction, output, and task design; learning research supports spacing and retrieval. Neither source establishes that this specific software improves fluency without evaluation.

## 2. Learning units

A learning item is a specific sense, phrase, collocation, or useful expression. Store the source context separately from the canonical learning item so that multiple encounters can support one item and the same surface word can have several meanings.

Example:

- Expression: `be available for…`
- Sense: having time to participate.
- Source excerpt: `Are you available for a quick call?`
- Scenario: arranging a conversation.
- Transfer task: negotiate a different time without reading the source sentence.

Model-suggested synonyms and lookalikes require validation. Frequency claims should reference a licensed or appropriately usable corpus/lexical resource. Do not treat generated suggestions as reliable frequency statistics or automatically schedule every suggestion.

## 3. Initial understanding

Ask the learner about goals, scenario interests, and time budget. Offer a short baseline containing listening comprehension and unscripted spoken responses. Allow a skip and start conservatively.

Do not infer general proficiency from a small sample, browser activity, accent, or a self-reported vocabulary count. Treat any level estimate as provisional and explain its evidence.

## 4. Daily task cycle

1. Choose a communicative outcome, such as suggesting an alternative appointment time.
2. Select a small number of relevant expressions, due reviews, and skill gaps.
3. Provide a level-matched listening example and a comprehension task.
4. Ask the learner to respond to a new prompt, initially with optional support.
5. Give limited, prioritized feedback on meaning, appropriateness, and intelligibility.
6. Offer a retry with reduced assistance.
7. Schedule a later task with new wording, context, or speaker.

Start with reviewed scenario templates and constrained generation. Generic starter tasks support learners who have not saved anything; the planner continues working without fresh browsing data.

## 5. Evidence and skill state

Maintain separate estimates for reading recognition, listening comprehension, and independent production. Each update references stored practice evidence.

Record at least:

- Task/version, target learning item, scenario, skill tested, and difficulty/support conditions.
- Response, timing, subtitle use, hints, replays, and retry relationship.
- Assessment rubric/version, provider/model version where available, uncertainty, and reason codes.
- Whether the attempt is eligible to update the skill estimate.

Important distinctions:

- A lookup is weak evidence of uncertainty, not proof of ignorance.
- Listening to an example is exposure, not proof of comprehension.
- Reading aloud or shadowing is not independent expression.
- A retry after seeing the answer is practice, not an independent test.
- Successful use of one sense is not mastery of every meaning of a word.
- An ASR failure or noisy recording is not a reliable language error.

Permit transcript correction and new recordings. Low-confidence attempts stay visible but do not automatically lower proficiency estimates. Corrections produce a new assessment linked to the original attempt rather than silently erasing its history.

## 6. Prioritization and scheduling

Begin with explainable rules using goal relevance, personal encounter frequency, general usefulness, forgetting risk, and recent reliable errors. Weight choices are provisional and must be versioned and evaluated.

Use spacing for well-defined recall tasks; evaluate FSRS as a candidate scheduler after selecting compatible task/rating semantics. Do not feed every model-generated conversation score directly into a memory algorithm. Listening and spoken transfer have separate task-selection rules.

Limit the daily active set. Keep saved material in an archive until selected; avoid converting all saved expressions into mandatory reviews. Learners can defer or dismiss items.

## 7. Feedback policy

Prioritize whether the learner understood and communicated the intended meaning. Correct a small number of consequential errors, offer a natural alternative, and invite another attempt. Avoid interrupting every minor grammar error.

Do not optimize for imitation of a single native accent. Speech recognition text can support meaning assessment but does not independently measure pronunciation quality. Dedicated acoustic analysis would need its own validation before adding pronunciation judgments.

## 8. Evaluate real improvement

Use delayed, unseen, level-matched tasks with different wording and contexts. Track independent completion, comprehension, assistance, and retention. Compare model assessments against human samples across recording quality and accents.

Separate formative feedback from outcome assessment. Keep held-out evaluation content outside personalized retrieval and exercise generation. Use a suitable comparison design before attributing improvement to the product.

## References

- [Task-Based Language Teaching — Cambridge University Press](https://www.cambridge.org/core/elements/taskbased-language-teaching/395B3D3B0F7078DF325579CC8314E38B)
- [The science of effective learning with spacing and retrieval practice](https://doi.org/10.1038/s44159-022-00089-1)
- [ABC of FSRS — official project wiki](https://github.com/open-spaced-repetition/fsrs4anki/wiki/ABC-of-FSRS)
