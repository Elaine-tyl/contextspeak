import corpus from "../content/corpus.json";
import { validateCorpus } from "../content/validation";
import type { Challenge } from "./catalog";
import { shuffle } from "./new-modes";

type Question = [prompt: string, answers: string[], explanation: string];
type Pack = {
  sourceId: string;
  level: number;
  excerptIds?: string[];
  questions: Question[];
};

// Published excerpts stay in the verified snapshot. Questions and distractors
// are original; the first editorial answer is shuffled before publication.
const packs: Pack[] = [
  {
    sourceId: "asp-0001",
    level: 1,
    excerptIds: ["asp-0001-full"],
    questions: [
      [
        "Why did the man change several objects?",
        [
          "They were too small or too low for him.",
          "They belonged to his neighbours.",
          "He wanted to sell them.",
          "He had lost all his furniture.",
        ],
        "His height made the hoe, doorway, bed and bicycle unsuitable.",
      ],
      [
        "Which object did the man buy rather than make?",
        [
          "A very high bicycle.",
          "A very long bed.",
          "A long hoe handle.",
          "High door frames.",
        ],
        "The text says he bought the bicycle but made the bed, handle and door frames.",
      ],
      [
        "Where did he eventually live?",
        [
          "In a big forest.",
          "In a shop.",
          "Beside a railway station.",
          "In a small boat.",
        ],
        "The last sentence describes his move to the forest.",
      ],
    ],
  },
  {
    sourceId: "voa-ancient-ai-6488034",
    level: 3,
    questions: [
      [
        "What is one aim of the AI system?",
        [
          "Help restore historical writings.",
          "Invent a new ancient language.",
          "Replace every museum guide.",
          "Teach robots to manufacture ceramics.",
        ],
        "The system helps restore writings and identify their time and place of origin.",
      ],
      [
        "Why are these writings useful to historians?",
        [
          "They contain evidence about ancient societies.",
          "They give modern weather forecasts.",
          "They identify every ancient person by name.",
          "They prove all societies were organised identically.",
        ],
        "The passage connects inscriptions with how people lived and structured their societies.",
      ],
      [
        "Where have inscriptions been found?",
        [
          "On rock, ceramic and metal.",
          "Only on paper.",
          "Only in printed books.",
          "Exclusively on wooden furniture.",
        ],
        "These three materials are explicitly listed in the excerpt.",
      ],
    ],
  },
  {
    sourceId: "voa-worker-ai-7296498",
    level: 5,
    questions: [
      [
        "Which group participated in the study?",
        [
          "More than 700 business consultants.",
          "Seven hundred school pupils.",
          "Only computer programmers.",
          "A single company director.",
        ],
        "The participants were business advisors from Boston Consulting Group.",
      ],
      [
        "What qualification limits the apparent benefit of AI?",
        [
          "Performance fell on some tasks the tool handled poorly.",
          "All participants improved equally on every task.",
          "The researchers studied only creative writing.",
          "No participant was allowed to use AI.",
        ],
        "The passage reports lower performance particularly on tasks outside the tool's strengths.",
      ],
      [
        "What organisational obstacle does the team identify?",
        [
          "Uncertainty about which tasks suit AI best.",
          "A complete absence of writing tasks.",
          "A ban on consultants doing research.",
          "The inability to measure any worker performance.",
        ],
        "The barrier is knowing where the technology can be used effectively.",
      ],
    ],
  },
  {
    sourceId: "voa-writing-ai-7498850",
    level: 7,
    questions: [
      [
        "What tension does the passage identify in research tools?",
        [
          "They simplify collecting sources but may hinder evaluating them.",
          "They make source lists impossible to produce.",
          "They eliminate the need for academic publications.",
          "They prevent all online research.",
        ],
        "Convenient source collection does not guarantee understanding or choosing the best evidence.",
      ],
      [
        "How does research training develop as students progress?",
        [
          "They continue learning research and publication skills at higher levels.",
          "They stop writing once they enter college.",
          "They replace all research with grammar correction.",
          "They are expected never to propose new ideas.",
        ],
        "The excerpt connects advanced degrees with continued development in research and academic writing.",
      ],
      [
        "Which claim would overstate the evidence in the passage?",
        [
          "Automated source collection guarantees the best sources.",
          "Tools can improve the readability of writing.",
          "Students learn research methods in college.",
          "Experienced students may seek publication.",
        ],
        "The final excerpt explicitly cautions that the tools can obstruct finding or understanding the best sources.",
      ],
    ],
  },
  {
    sourceId: "voa-carbon-fuel-5090884",
    level: 9,
    questions: [
      [
        "Which comparison best captures the technical motivation described?",
        [
          "Converting carbon dioxide while addressing costly purification in other methods.",
          "Replacing all electricity generation with ceramic inscriptions.",
          "Demonstrating that carbon dioxide has no connection with human activity.",
          "Proving that purification costs are irrelevant to fuel production.",
        ],
        "The reactor produces a pure form of formic acid; the comparison describes other methods as energy-intensive and costly.",
      ],
      [
        "Which conclusion goes beyond the evidence provided?",
        [
          "The reactor has already eliminated industrial emissions worldwide.",
          "Fossil-fuel use contributes to carbon dioxide emissions.",
          "Some industries have sought cleaner energy options.",
          "Other conversion methods require purification.",
        ],
        "The passage describes a research device, without evidence of worldwide deployment or eliminated emissions.",
      ],
      [
        "Why does the passage discuss electricity, heating and transportation?",
        [
          "To identify human activities associated with producing the gas.",
          "To argue that only transportation produces emissions.",
          "To establish the precise price of formic acid.",
          "To show that no industries seek cleaner alternatives.",
        ],
        "They illustrate the fossil-fuel-related activities producing much of the heat-trapping gas.",
      ],
    ],
  },
];
const reviewed = validateCorpus(corpus);
export const openReadingChallenges: Challenge[] = packs.flatMap((pack) => {
  const original = reviewed.sources.find(
    (source) => source.id === pack.sourceId,
  )!;
  const { fetchUrl, ...source } = original;
  const excerpts = reviewed.excerpts.filter(
    (excerpt) =>
      excerpt.sourceId === pack.sourceId &&
      (!pack.excerptIds || pack.excerptIds.includes(excerpt.id)),
  );
  const passage = excerpts.map((excerpt) => excerpt.text).join("\n\n");
  return pack.questions.map(([instruction, answers, explanation], index) => {
    const id = `open-reading-${pack.sourceId}-${index}-v1`;
    const choices = shuffle(answers, id).map((label, position) => ({
      id: `option-${position}`,
      label,
    }));
    return {
      id,
      kind: "reading-question",
      track: "reading",
      level: pack.level,
      title: original.title,
      topic: original.publishedAt ? "News & science" : "Stories",
      audioText: "",
      passage,
      instruction,
      choices,
      source,
      answer: choices.find((choice) => choice.label === answers[0])!.id,
      explanation,
    };
  });
});
