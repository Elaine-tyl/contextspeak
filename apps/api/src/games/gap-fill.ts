import type { GapFillFeedback, GameRound } from "@contextspeak/contracts";
import type { Challenge } from "./catalog";
import { gradeDictation } from "../practice/dictation";
import { legacyGapSentences, previousGapSentences } from "./gap-fill-legacy";

// One word per bracket. New editions get new IDs; old runs keep their answers.
const sentences = [
  [
    "Could you confirm the [ingredients] in this sandwich and check whether it contains any [allergens]?",
    "We should check everyone's [availability] before confirming the [reservation] for our monthly book club.",
    "Please check the [departure] time and your final [destination] before boarding the train.",
    "Students must complete their [registration] before attending the [compulsory] research workshop on Friday.",
  ],
  [
    "The café offers a [substitution] for dairy milk, but a small [surcharge] applies to each drink.",
    "Can we [postpone] the discussion until [Wednesday], when the project coordinator returns from the conference?",
    "You may request a [refund] if the [cancellation] prevents you from reaching your destination today.",
    "Your [assignment] should include a clear [hypothesis] and explain the evidence you plan to collect.",
  ],
  [
    "Could you [recommend] a [vegetarian] option and explain whether the ingredients are [seasonal] or available throughout the year?",
    "When we [reschedule] the meeting, please [circulate] the revised agenda so everyone can [prepare] their contributions in advance.",
    "Passengers should [retain] their receipts and contact the [operator] to request [compensation] for the overnight delay.",
    "The [lecturer] asked us to [summarize] the main argument and [evaluate] the reliability of its supporting evidence.",
  ],
  [
    "Please explain how the kitchen handles [dietary] [requirements] and prevents [contamination] when preparing food for customers with severe allergies.",
    "We need to [negotiate] a realistic [deadline] before deciding whether the proposed launch date is [feasible] for the whole team.",
    "Because of the [disruption], passengers may use an [alternative] route and apply for [reimbursement] of reasonable additional expenses.",
    "The [participants] should form a [representative] sample so that the researchers can draw meaningful [conclusions] about the wider population.",
  ],
  [
    "The manager [acknowledged] the mistake and offered a [complimentary] dessert, but we requested a [replacement] that met our [dietary] requirements.",
    "We should [prioritize] the essential tasks, [allocate] sufficient time for testing, and [anticipate] any [obstacles] before confirming the revised schedule.",
    "The airline requires [documentation] of the [disruption] before it can [authorize] a [reimbursement] for accommodation or meals purchased during the delay.",
    "The [researchers] used a [questionnaire] to [investigate] how access to public transport affected students' [attendance] during the winter semester.",
  ],
  [
    "The café's [procurement] policy gives [preference] to local suppliers, although seasonal [fluctuations] sometimes affect the [availability] of fresh ingredients on the menu.",
    "Before we [implement] the proposal, we should [consult] the affected teams and [assess] whether our current [resources] can support the additional workload.",
    "The [itinerary] allows a short [connection], but the operator cannot [guarantee] onward transport if unexpected [congestion] delays the first part of the journey.",
    "The [analysis] identified a strong [correlation], but the [investigators] emphasized that the available evidence did not establish [causation] between the two measured factors.",
  ],
  [
    "We would [appreciate] some [clarification] about the [discrepancy] between the advertised price and the [additional] charges, since those costs were not [disclosed] before we ordered.",
    "The project [coordinator] proposed a [contingency] plan to [accommodate] unexpected delays while maintaining [continuity] and protecting the most [vulnerable] parts of the delivery schedule.",
    "Although the [restrictions] were introduced as a [precaution], the company must still [communicate] the revised [arrangements] clearly and offer [assistance] to passengers who cannot travel.",
    "The [findings] were [consistent] across several regions, but the [researchers] warned that differences in [recruitment] might limit how confidently the results could be [interpreted].",
  ],
  [
    "The supplier offered [assurances] about [sustainability], but the café requested [independent] [verification] before making a long-term [commitment] to a contract advertised as environmentally responsible.",
    "If the proposed [restructuring] proceeds without [consultation], we risk [undermining] trust and creating [uncertainty], so managers should explain the [rationale] before announcing any changes to individual responsibilities.",
    "The operator [attributed] the repeated [cancellations] to severe weather, but passengers questioned whether [inadequate] maintenance and poor [coordination] had also contributed to the [disruption] across the regional network.",
    "While the [intervention] produced a [measurable] improvement, its [effectiveness] depended on [consistent] support, and the team could not rule out the influence of [unobserved] differences between the groups.",
  ],
  [
    "The manager promised [reimbursement] after reviewing the [discrepancy], but asked us to [substantiate] our complaint with the original [receipt] and explain why the proposed [substitution] would not provide an [equivalent] meal.",
    "Given the [uncertainty] surrounding the [negotiations], we should avoid [premature] announcements and develop a [contingency] arrangement that preserves [flexibility] without creating an [unmanageable] burden for the remaining staff.",
    "Before accepting the revised [itinerary], I requested [clarification] of the [eligibility] rules for [compensation], since the operator had previously [attributed] the delay to [exceptional] circumstances beyond its immediate control.",
    "The [assessment] revealed [substantial] [variation] across regions, but the authors warned that [inconsistent] measurement and incomplete [documentation] could undermine the [reliability] of comparisons between the different population groups.",
  ],
  [
    "Although the café [acknowledged] the error, the proposed [substitution] failed to address my [dietary] requirements, so I requested [reimbursement] and a written [explanation] of how similar [discrepancies] would be prevented in future.",
    "Unless we can [demonstrate] that the [reallocation] will improve [accessibility] without [disproportionately] affecting existing services, the committee is unlikely to [endorse] a proposal based on such [speculative] financial assumptions.",
    "The operator initially [disputed] our [eligibility] for [compensation], but an [independent] review found that [inadequate] communication had prevented passengers from making [alternative] arrangements before the last connecting service departed.",
    "Although the study acknowledges [methodological] limitations and several [confounding] variables, its [generalizability] remains uncertain until independent [replication] provides [corroborating] evidence, so the authors describe their conclusions as [tentative].",
  ],
];
const topics = ["Café", "Making plans", "Travel", "Academic study"];

export function makeGapFill(
  template: string,
  id: string,
  level: number,
  topic: string,
  source?: GameRound["source"],
): Challenge {
  const fragments = template.split(/\[([^\]]+)\]/u);
  const audioText = fragments.join("");
  return {
    id,
    kind: "gap-fill",
    track: "listening",
    level,
    title:
      topic === "Academic study"
        ? "Academic listening: missing words"
        : "Listen for the missing words",
    topic,
    instruction: "Play the sentence, then fill each blank with one word.",
    gapText: fragments.filter((_, index) => index % 2 === 0),
    gapAnswers: fragments.filter((_, index) => index % 2 === 1),
    audioText,
    answer: audioText,
    source,
    explanation: "Each blank counts equally. Case and punctuation are ignored.",
  };
}

export const gapFillChallenges = sentences.flatMap((stage, index) =>
  stage.map((sentence, topic) =>
    makeGapFill(
      sentence,
      `gap-fill-${index + 1}-${topic}-v3`,
      index + 1,
      topics[topic],
    ),
  ),
);
export const archivedGapFillChallenges = [
  { sentences: legacyGapSentences, version: 1 },
  { sentences: previousGapSentences, version: 2 },
].flatMap((edition) =>
  edition.sentences.flatMap((stage, index) =>
    stage.map((sentence, topic) => ({
      ...makeGapFill(
        sentence,
        `gap-fill-${index + 1}-${topic}-v${edition.version}`,
        index + 1,
        topics[topic],
      ),
      archived: true,
    })),
  ),
);

export function gradeGapFill(
  expected: string[],
  answers: string[],
): GapFillFeedback {
  const words = expected.map((word, index) => ({
    answer: answers[index],
    expected: word,
    correct: gradeDictation(word, answers[index], "en").score === 100,
  }));
  return {
    score: Math.round(
      (100 * words.filter((word) => word.correct).length) / words.length,
    ),
    words,
  };
}
