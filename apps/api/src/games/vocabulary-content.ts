import type { Challenge } from "./catalog";
import { shuffle } from "./new-modes";

type Word = {
  word: string;
  meaning: string;
  sentence: string;
  alternatives: string[];
};
// Original graded practice vocabulary. A new edition must use new question IDs.
const levels: Word[][] = [
  [
    {
      word: "available",
      meaning: "Free to use or able to meet",
      sentence: "Are you ___ after class? I would like to meet for coffee.",
      alternatives: ["expensive", "crowded", "delayed"],
    },
    {
      word: "borrow",
      meaning: "Take something temporarily and return it",
      sentence: "Could I ___ your pen? I will give it back after the lesson.",
      alternatives: ["lend", "spend", "sell"],
    },
    {
      word: "appointment",
      meaning: "An arranged time to meet someone",
      sentence:
        "I have an ___ with the dentist at two, so I need to leave work early.",
      alternatives: ["ingredient", "invitation", "entrance"],
    },
  ],
  [
    {
      word: "reschedule",
      meaning: "Arrange an event for a different time",
      sentence:
        "The trainer is ill, so we need to ___ tomorrow's workshop for next week.",
      alternatives: ["attend", "advertise", "repeat"],
    },
    {
      word: "refund",
      meaning: "Money returned after a purchase is cancelled",
      sentence:
        "The concert was cancelled, and every ticket holder received a full ___.",
      alternatives: ["discount", "deposit", "receipt"],
    },
    {
      word: "convenient",
      meaning: "Suitable and easy for someone's needs",
      sentence:
        "The station is next to my office, which makes taking the train very ___.",
      alternatives: ["distant", "fragile", "temporary"],
    },
  ],
  [
    {
      word: "reliable",
      meaning: "Able to be trusted to work well or act as expected",
      sentence:
        "We need a ___ delivery service that arrives on time every day.",
      alternatives: ["reluctant", "remote", "disposable"],
    },
    {
      word: "postpone",
      meaning: "Delay something until a later time",
      sentence:
        "Heavy rain forced the organisers to ___ the outdoor match until Friday.",
      alternatives: ["accelerate", "overlook", "sponsor"],
    },
    {
      word: "estimate",
      meaning: "An approximate calculation rather than an exact figure",
      sentence:
        "Before agreeing to the repair, I asked the mechanic for a cost ___.",
      alternatives: ["apology", "delivery", "reservation"],
    },
  ],
  [
    {
      word: "accommodate",
      meaning: "Adjust to include or meet someone's needs",
      sentence:
        "The café changed its layout to ___ customers who use wheelchairs.",
      alternatives: ["discourage", "resemble", "interrupt"],
    },
    {
      word: "allocate",
      meaning: "Assign a resource to a particular purpose",
      sentence:
        "The council will ___ part of its budget to repairing the bridge.",
      alternatives: ["borrow", "conceal", "postpone"],
    },
    {
      word: "constraint",
      meaning: "A limit that restricts what can be done",
      sentence:
        "The main ___ is time: we have only two days to finish the project.",
      alternatives: ["incentive", "outcome", "permission"],
    },
  ],
  [
    {
      word: "feasible",
      meaning: "Practical and possible to carry out",
      sentence:
        "The proposal is technically ___, but we still need permission to begin.",
      alternatives: ["compulsory", "identical", "irrelevant"],
    },
    {
      word: "incentive",
      meaning: "Something that encourages a person to act",
      sentence:
        "Free bus passes gave employees an ___ to leave their cars at home.",
      alternatives: ["obstacle", "exception", "estimate"],
    },
    {
      word: "sustain",
      meaning: "Keep something going over a period of time",
      sentence:
        "Short-term donations helped, but regular funding was needed to ___ the service.",
      alternatives: ["terminate", "distinguish", "withdraw"],
    },
  ],
  [
    {
      word: "mitigate",
      meaning: "Make a harmful effect less severe",
      sentence:
        "Planting trees can help ___ summer heat, although it will not eliminate it.",
      alternatives: ["intensify", "disregard", "predict"],
    },
    {
      word: "trade-off",
      meaning: "A benefit gained by accepting a disadvantage",
      sentence:
        "The smaller flat saves money but offers less space: that is the main ___.",
      alternatives: ["coincidence", "certainty", "requirement"],
    },
    {
      word: "accountable",
      meaning: "Responsible for decisions and expected to explain them",
      sentence:
        "Public officials should be ___ to the people affected by their decisions.",
      alternatives: ["immune", "unrelated", "identical"],
    },
  ],
  [
    {
      word: "plausible",
      meaning: "Reasonable enough to be believable, though not proven",
      sentence:
        "Her explanation is ___, but we need evidence before accepting it as fact.",
      alternatives: ["conclusive", "mandatory", "unrelated"],
    },
    {
      word: "discrepancy",
      meaning: "A difference between things expected to agree",
      sentence:
        "An audit found a ___ between the recorded stock and the actual items on the shelf.",
      alternatives: ["consensus", "subsidy", "precedent"],
    },
    {
      word: "contingency",
      meaning: "A possible future event that requires preparation",
      sentence:
        "We reserved an indoor venue as a ___ plan in case the weather turned bad.",
      alternatives: ["retrospective", "compulsory", "redundant"],
    },
  ],
  [
    {
      word: "substantiate",
      meaning: "Support a claim with evidence",
      sentence:
        "Before publication, the author must ___ the claim with independently checked data.",
      alternatives: ["obscure", "overlook", "retract"],
    },
    {
      word: "nuanced",
      meaning: "Showing subtle distinctions rather than a simple division",
      sentence:
        "Instead of calling the policy entirely good or bad, she offered a ___ assessment of its different effects.",
      alternatives: ["categorical", "superficial", "unanimous"],
    },
    {
      word: "disproportionate",
      meaning: "Too large or small in relation to something else",
      sentence:
        "The fee imposed a ___ burden on low-income families because it took a much larger share of their income.",
      alternatives: ["negligible", "uniform", "voluntary"],
    },
  ],
  [
    {
      word: "corroborate",
      meaning: "Provide independent support for an account or finding",
      sentence:
        "Investigators used a second, independent source to ___ the witness's account.",
      alternatives: ["fabricate", "dismiss", "withhold"],
    },
    {
      word: "equivocal",
      meaning: "Unclear or open to more than one interpretation",
      sentence:
        "The results were ___: one measure improved, another declined, and neither change was statistically clear.",
      alternatives: ["definitive", "unanimous", "irreversible"],
    },
    {
      word: "exacerbate",
      meaning: "Make an existing problem worse",
      sentence:
        "Closing the only shelter would ___ the housing shortage rather than relieve it.",
      alternatives: ["alleviate", "resolve", "preclude"],
    },
  ],
  [
    {
      word: "commensurate",
      meaning: "Corresponding appropriately in amount or degree",
      sentence:
        "The panel argued that the penalty should be ___ with the seriousness of the offence.",
      alternatives: ["incompatible", "incidental", "indifferent"],
    },
    {
      word: "tenuous",
      meaning: "Weak or uncertain and therefore difficult to justify",
      sentence:
        "The link between the two events remained ___; timing alone did not establish a causal relationship.",
      alternatives: ["unequivocal", "indispensable", "conclusive"],
    },
    {
      word: "reconcile",
      meaning: "Find a way for apparently conflicting things to fit together",
      sentence:
        "The proposal seeks to ___ environmental protection with the community's need for affordable housing.",
      alternatives: ["exacerbate", "exclude", "misconstrue"],
    },
  ],
];
const bank = levels.flat();
export const vocabularyChallenges: Challenge[] = levels.flatMap(
  (words, index) =>
    words.flatMap((entry, item) => {
      const level = index + 1;
      const offset = index * 3 + item;
      const meanings = [
        entry,
        ...[5, 11, 17].map((step) => bank[(offset + step) % bank.length]),
      ];
      const common = {
        track: "vocabulary" as const,
        level,
        audioText: "",
        topic:
          level < 4
            ? "Everyday words"
            : level < 8
              ? "Work and community"
              : "Academic vocabulary",
      };
      const meaningId = `game-word-meaning-${level}-${item}-v1`;
      const contextId = `game-word-context-${level}-${item}-v1`;
      const meaningChoices = shuffle(meanings, meaningId).map(
        (word, position) => ({ id: `option-${position}`, label: word.meaning }),
      );
      return [
        {
          ...common,
          id: meaningId,
          kind: "word-meaning" as const,
          title: "Find the meaning",
          instruction: "Choose the closest meaning of this word.",
          passage: entry.word,
          choices: meaningChoices,
          answer: meaningChoices.find(
            (choice) => choice.label === entry.meaning,
          )!.id,
          explanation: `${entry.word}: ${entry.meaning}. In context: ${entry.sentence.replace("___", entry.word)}`,
        },
        {
          ...common,
          id: contextId,
          kind: "word-context" as const,
          title: "Find the missing word",
          instruction:
            "Read the sentence and choose the word that fits its meaning.",
          passage: entry.sentence,
          choices: shuffle(
            [entry.word, ...entry.alternatives].map((word) => ({
              id: word,
              label: word,
            })),
            contextId,
          ),
          answer: entry.word,
          explanation: `${entry.sentence.replace("___", entry.word)} Here, ${entry.word} means: ${entry.meaning.toLowerCase()}.`,
        },
      ];
    }),
);
