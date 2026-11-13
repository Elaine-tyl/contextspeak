import type { CrosswordEntry } from "@contextspeak/contracts";
import type { Challenge } from "./catalog";
import { shuffle } from "./new-modes";

function shuffled<T>(items: T[], seed: string) {
  const result = shuffle(items, seed);
  return result.every((value, index) => value === items[index])
    ? [...result.slice(1), result[0]]
    : result;
}
function letters(word: string) {
  return word.toLowerCase().replace(/[^a-z]/gu, "");
}
/** Place two words around one shared letter; public entries contain no solution. */
export function crosswordLayout(
  words: { word: string; meaning: string }[],
): CrosswordEntry[] {
  const [across, down] = words.map((word) => letters(word.word));
  const intersections: { across: number; down: number; distance: number }[] =
    [];
  for (let a = 0; a < across.length; a++) {
    for (let d = 0; d < down.length; d++) {
      if (across[a] === down[d])
        intersections.push({
          across: a,
          down: d,
          distance:
            Math.abs(a - (across.length - 1) / 2) +
            Math.abs(d - (down.length - 1) / 2),
        });
    }
  }
  const crossing = intersections.sort((a, b) => a.distance - b.distance)[0];
  if (!crossing) throw new Error("Crossword words need a shared letter.");
  return [
    {
      clue: words[0].meaning,
      row: crossing.down,
      column: 0,
      direction: "across",
      length: across.length,
    },
    {
      clue: words[1].meaning,
      row: 0,
      column: crossing.across,
      direction: "down",
      length: down.length,
    },
  ];
}
// Separate clues prevent a public Word meanings prompt from disclosing a puzzle answer.
const puzzleVocabulary: { word: string; meaning: string }[][] = [
  [
    { word: "station", meaning: "A place where trains stop for passengers" },
    { word: "library", meaning: "A place where people can borrow books" },
    {
      word: "garden",
      meaning: "An area where flowers or vegetables are grown",
    },
  ],
  [
    {
      word: "receipt",
      meaning: "A written record showing that a payment was made",
    },
    {
      word: "journey",
      meaning: "An occasion of travelling from one place to another",
    },
    { word: "package", meaning: "An item wrapped or boxed for delivery" },
  ],
  [
    {
      word: "volunteer",
      meaning: "A person who offers to help without being paid",
    },
    {
      word: "delivery",
      meaning: "The act of bringing an item to its destination",
    },
    {
      word: "deadline",
      meaning: "The latest time by which something must be completed",
    },
  ],
  [
    {
      word: "priority",
      meaning: "Something considered more important than other things",
    },
    {
      word: "resource",
      meaning: "Something available for use in achieving a purpose",
    },
    {
      word: "proposal",
      meaning: "A suggested plan presented for consideration",
    },
  ],
  [
    {
      word: "efficient",
      meaning: "Achieving a result with little wasted time or effort",
    },
    {
      word: "flexible",
      meaning: "Able to change to suit different circumstances",
    },
    {
      word: "capacity",
      meaning: "The amount that something can contain or produce",
    },
  ],
  [
    {
      word: "resilient",
      meaning: "Able to recover after difficulty or disruption",
    },
    {
      word: "threshold",
      meaning: "A point beyond which a condition or response changes",
    },
    { word: "innovation", meaning: "The introduction of a new idea or method" },
  ],
  [
    { word: "ambiguous", meaning: "Having more than one possible meaning" },
    {
      word: "empirical",
      meaning: "Based on observation or experience rather than theory alone",
    },
    {
      word: "coherent",
      meaning: "Logically connected and understandable as a whole",
    },
  ],
  [
    { word: "scrutiny", meaning: "Close and careful examination" },
    {
      word: "autonomy",
      meaning: "The ability to act or make decisions independently",
    },
    {
      word: "consensus",
      meaning: "General agreement among members of a group",
    },
  ],
  [
    {
      word: "contingent",
      meaning: "Dependent on a particular condition or event",
    },
    {
      word: "incremental",
      meaning: "Developing through a series of small changes",
    },
    {
      word: "transparent",
      meaning: "Open enough for actions and reasons to be understood",
    },
  ],
  [
    {
      word: "methodological",
      meaning: "Related to the methods used in an investigation",
    },
    { word: "unprecedented", meaning: "Not known to have happened before" },
    {
      word: "counterproductive",
      meaning: "Having an effect opposite to the intended result",
    },
  ],
];
const vocabularyPuzzles: Challenge[] = puzzleVocabulary.flatMap(
  (words, index) =>
    words.flatMap((word, item) => {
      const level = index + 1;
      const pairs = [word, words[(item + 1) % words.length]];
      const answers = pairs.map((entry) => letters(entry.word));
      const id = `game-word-scramble-${level}-${item}-v1`;
      return [
        {
          id,
          kind: "word-scramble",
          track: "vocabulary",
          level,
          topic: "Word clues",
          audioText: "",
          title: "Unlock the word",
          passage: word.meaning,
          instruction:
            "Arrange the letters to solve the clue. Hyphens are omitted.",
          tiles: shuffled([...answers[0]], id),
          answer: answers[0],
          explanation: `${word.word}: ${word.meaning}.`,
        },
        {
          id: `game-mini-crossword-${level}-${item}-v1`,
          kind: "mini-crossword",
          track: "vocabulary",
          level,
          topic: "Connected words",
          audioText: "",
          title: "Find the crossing words",
          instruction:
            "Solve both clues. The crossing letter must fit both words; omit hyphens.",
          crossword: crosswordLayout(pairs),
          gapAnswers: answers,
          answer: answers.join(" / "),
          explanation: pairs
            .map((entry) => `${entry.word}: ${entry.meaning}.`)
            .join(" "),
        },
      ];
    }),
);

type TextPuzzle = {
  title: string;
  lines: string[];
  question: string;
  evidence: number;
  summary: string;
};
// New passages: never reveal an existing paragraph-order solution. Summaries are
// paraphrases rather than a sentence copied from the visible text.
const texts: TextPuzzle[][] = [
  [
    {
      title: "The last bus",
      lines: [
        "Jo planned to stay at the party until ten.",
        "Her bus timetable showed that the final service left at nine thirty.",
        "She said goodbye early and reached the stop before it arrived.",
      ],
      question: "Which sentence explains why Jo left the party early?",
      evidence: 1,
      summary: "Jo left the party early to catch her bus.",
    },
    {
      title: "An empty shelf",
      lines: [
        "The shop had sold its last loaf before Kim arrived.",
        "Kim asked whether another delivery was expected that afternoon.",
        "When the assistant said no, Kim decided to visit a different bakery.",
      ],
      question: "Which sentence shows that Kim could not buy bread there?",
      evidence: 0,
      summary: "Kim visited another bakery because the shop had no bread.",
    },
    {
      title: "The lost scarf",
      lines: [
        "Zoe noticed that her scarf was missing after she left the café.",
        "She returned and described it to the server.",
        "He gave her a scarf that matched her description and had been found under her chair.",
      ],
      question: "Which sentence shows how Zoe recovered her scarf?",
      evidence: 2,
      summary: "The server returned the scarf that Zoe had lost.",
    },
  ],
  [
    {
      title: "A late parcel",
      lines: [
        "Ravi waited at home for a delivery that was due before noon.",
        "At one, a message explained that the van had broken down.",
        "He arranged delivery for the next morning instead of waiting all afternoon.",
      ],
      question: "Which sentence identifies the cause of the delay?",
      evidence: 1,
      summary: "A van breakdown delayed the delivery of Ravi's parcel.",
    },
    {
      title: "The quiet table",
      lines: [
        "The café offered tables downstairs and in a small upstairs room.",
        "Dan needed to read a long document before his meeting.",
        "He chose the upstairs room after the server explained that it was usually much quieter.",
      ],
      question: "Which sentence connects Dan's choice with the noise level?",
      evidence: 2,
      summary: "Dan chose the quieter room to read his document.",
    },
    {
      title: "A short walk",
      lines: [
        "The lift was out of service when the family reached the apartment building.",
        "Their flat was on the second floor and none of their bags was heavy.",
        "They used the stairs and reported the broken lift to the manager.",
      ],
      question:
        "Which sentence explains why the family could not use the lift?",
      evidence: 0,
      summary: "The family took the stairs because the lift was broken.",
    },
  ],
  [
    {
      title: "A careful purchase",
      lines: [
        "Nell compared two second-hand bicycles with similar prices.",
        "One seller could provide maintenance records, while the other knew nothing about the bicycle's history.",
        "Nell bought the bicycle with the records, valuing information about its condition over its newer paint.",
      ],
      question:
        "Which sentence provides the information that helped Nell judge reliability?",
      evidence: 1,
      summary: "Maintenance records helped Nell choose a second-hand bicycle.",
    },
    {
      title: "The extra session",
      lines: [
        "A pottery class was fully booked before several residents could register.",
        "The instructor found another room and asked whether people would attend on Thursday evenings.",
        "Enough residents agreed, so she opened a second weekly class.",
      ],
      question: "Which sentence shows that there was demand for another class?",
      evidence: 2,
      summary:
        "The instructor added a class after residents expressed interest.",
    },
    {
      title: "A changed menu",
      lines: [
        "The vegetable delivery contained fewer peppers than the restaurant had ordered.",
        "The chef checked which dishes depended on them before planning dinner service.",
        "She replaced two pepper dishes with alternatives using ingredients already in the kitchen.",
      ],
      question:
        "Which sentence identifies the problem the chef had to address?",
      evidence: 0,
      summary: "A delivery shortage led the chef to change two dishes.",
    },
  ],
  [
    {
      title: "Access to the hall",
      lines: [
        "Residents wanted the community hall to host more evening events.",
        "The committee discovered that the last local bus departed before most proposed events would finish.",
        "It adjusted the programme to end earlier rather than assuming that everyone could drive.",
      ],
      question:
        "Which sentence identifies a practical barrier to participation?",
      evidence: 1,
      summary: "The committee changed event times to accommodate bus users.",
    },
    {
      title: "A useful trial",
      lines: [
        "A workplace introduced standing desks in one department before making a wider purchase.",
        "During the trial, staff could choose when to stand and reported their experiences each week.",
        "Their mixed feedback led management to offer a choice of desk types rather than replace every seated desk.",
      ],
      question:
        "Which sentence shows that the trial influenced the final decision?",
      evidence: 2,
      summary: "Staff feedback led management to retain a choice of desks.",
    },
    {
      title: "Finding the entrance",
      lines: [
        "First-time visitors repeatedly mistook a delivery door for the theatre entrance.",
        "Staff watched several arriving groups to understand where the confusion began.",
        "They moved the main entrance sign to the corner where most visitors first approached.",
      ],
      question:
        "Which sentence supplies evidence that the entrance was difficult to identify?",
      evidence: 0,
      summary: "The theatre moved a sign to help visitors find its entrance.",
    },
  ],
  [
    {
      title: "A misleading total",
      lines: [
        "A reading club celebrated an increase in the number of books completed.",
        "Members' records showed that much of the increase came from choosing shorter books rather than spending more time reading.",
        "The organiser began asking about reading time and enjoyment alongside completion totals.",
      ],
      question: "Which sentence limits what the higher total can tell us?",
      evidence: 1,
      summary:
        "Shorter book choices partly explained the rise in completed books.",
    },
    {
      title: "A flexible response",
      lines: [
        "A food cooperative considered requiring every member to volunteer on Saturday mornings.",
        "Several members worked weekends, despite being available during the week.",
        "The cooperative introduced weekday tasks as alternatives, keeping the volunteering requirement but allowing different schedules.",
      ],
      question:
        "Which sentence shows how the cooperative preserved its aim while changing its method?",
      evidence: 2,
      summary:
        "The cooperative offered different schedules for the same volunteering requirement.",
    },
    {
      title: "The maintenance budget",
      lines: [
        "The proposed playground could be built with existing funds, but those funds did not cover future repairs.",
        "Councillors requested an estimate of annual maintenance before approving construction.",
        "The final budget set aside money for upkeep as well as the initial building work.",
      ],
      question:
        "Which sentence explains why the initial funding figure was incomplete?",
      evidence: 0,
      summary:
        "The playground budget needed to include future maintenance costs.",
    },
  ],
  [
    {
      title: "Unequal access",
      lines: [
        "A booking service replaced its telephone line with an online form to reduce waiting times.",
        "Residents without reliable internet access reported that they now found it harder to make appointments.",
        "The service restored a staffed telephone option while retaining the online form.",
      ],
      question:
        "Which sentence challenges the assumption that online booking helped everyone?",
      evidence: 1,
      summary:
        "An online-only booking system created barriers for some residents.",
    },
    {
      title: "A broader measure",
      lines: [
        "A delivery team was initially rewarded for completing as many stops as possible.",
        "Supervisors found that rushed deliveries sometimes resulted in misplaced parcels and repeat visits.",
        "They added successful first-time delivery to the assessment, so speed alone could no longer determine the reward.",
      ],
      question: "Which sentence describes an adjustment to a narrow incentive?",
      evidence: 2,
      summary: "The delivery team began rewarding accuracy as well as speed.",
    },
    {
      title: "A temporary safeguard",
      lines: [
        "Repairs to the only footbridge were expected to finish before the school term, but the completion date was not certain.",
        "The council arranged an alternative school bus route that could be used if necessary.",
        "It explained that the arrangement was a precaution rather than a forecast that the bridge would remain closed.",
      ],
      question:
        "Which sentence establishes the uncertainty that justified the precaution?",
      evidence: 0,
      summary:
        "An alternative bus route protected against uncertainty over bridge repairs.",
    },
  ],
  [
    {
      title: "Who answered?",
      lines: [
        "An online survey suggested that most residents supported extending a park's opening hours.",
        "A check of the responses found that older residents and people without home internet were poorly represented.",
        "The council held additional face-to-face sessions before treating the result as a view of the whole community.",
      ],
      question:
        "Which sentence provides grounds for questioning the sample's representativeness?",
      evidence: 1,
      summary:
        "Uneven participation limited the representativeness of the online survey.",
    },
    {
      title: "What changed?",
      lines: [
        "A school introduced a homework app shortly before examination results improved.",
        "The same term also included extra teaching hours and a change in the examination format.",
        "The headteacher acknowledged that the app's separate contribution could not be established from the results alone.",
      ],
      question: "Which sentence states the limit on the causal conclusion?",
      evidence: 2,
      summary:
        "Several simultaneous changes prevented a clear assessment of the app.",
    },
    {
      title: "The cost of certainty",
      lines: [
        "A retailer could guarantee next-day delivery only by keeping more vehicles available than it normally needed.",
        "Managers compared that added cost with the value customers placed on a guaranteed arrival date.",
        "They offered the guarantee as an optional service instead of raising the price of every order.",
      ],
      question: "Which sentence explains why a stronger guarantee had a cost?",
      evidence: 0,
      summary:
        "Guaranteed delivery required spare capacity that increased operating costs.",
    },
  ],
  [
    {
      title: "An encouraging association",
      lines: [
        "Participants in a voluntary mentoring scheme reported higher confidence after six months.",
        "Because the participants had selected themselves and there was no comparison group, the study could not determine whether mentoring alone produced the change.",
        "The researchers reported the improvement while recommending a design capable of testing that causal claim.",
      ],
      question:
        "Which sentence explains why the reported improvement is not conclusive evidence of causation?",
      evidence: 1,
      summary:
        "The study observed improvement without isolating the effect of mentoring.",
    },
    {
      title: "A conditional agreement",
      lines: [
        "A review board supported a plan to convert an office block into housing.",
        "Its support depended on retaining accessible entrances and demonstrating that essential repairs could be funded.",
        "The board objected when publicity presented its position as unconditional approval.",
      ],
      question:
        "Which sentence shows that the publicity omitted an important qualification?",
      evidence: 1,
      summary:
        "The board's support depended on conditions omitted from the publicity.",
    },
    {
      title: "A distributional question",
      lines: [
        "A new local fee charged every household the same amount, which officials initially described as an equal contribution.",
        "Residents asked whether an identical payment imposed an identical burden on households with very different incomes.",
        "A review found that the fee consumed a substantially greater proportion of income among the lowest-paid households.",
      ],
      question:
        "Which sentence gives evidence that equal payments could have unequal effects?",
      evidence: 2,
      summary:
        "Identical fees imposed different burdens on households with unequal incomes.",
    },
  ],
  [
    {
      title: "Repetition or corroboration?",
      lines: [
        "Several reports appeared to confirm an account of changes in a company's working conditions.",
        "Tracing their references showed that they all depended on one anonymous statement rather than separately gathered observations.",
        "Reviewers therefore sought independent sources before describing the account as corroborated.",
      ],
      question:
        "Which sentence undermines the claim that the reports were independent confirmations?",
      evidence: 1,
      summary:
        "Repeated use of one source did not provide independent corroboration.",
    },
    {
      title: "The visible explanation",
      lines: [
        "Residents blamed a sudden increase in traffic on a recently opened supermarket.",
        "Traffic counters showed that most additional vehicles were passing through the district after a nearby road closure, rather than stopping at the store.",
        "The council investigated temporary diversion routes instead of assuming that restricting supermarket access would remove the main cause.",
      ],
      question:
        "Which sentence supplies evidence for an alternative explanation of the traffic increase?",
      evidence: 1,
      summary: "A road closure explained much of the additional traffic.",
    },
    {
      title: "Consistency is not enough",
      lines: [
        "A selection system produced the same recommendations whenever the same applications were assessed again.",
        "Its designers cited this repeatability as evidence that the system was dependable.",
        "An audit nevertheless found that the consistently applied rule disadvantaged applicants whose relevant experience had been gained outside conventional employment.",
      ],
      question:
        "Which sentence demonstrates a limitation of relying on repeatability alone?",
      evidence: 2,
      summary:
        "A repeatable selection rule could still disadvantage qualified applicants.",
    },
  ],
  [
    {
      title: "Two meanings of fairness",
      lines: [
        "An organisation defended a uniform relocation payment as fair because every affected household would receive the same sum.",
        "Consultation established that households with particular access needs faced materially higher costs in obtaining suitable replacement accommodation.",
        "The revised proposal distinguished uniform procedure from comparable practical support, while making the criteria for any adjustment open to independent scrutiny.",
      ],
      question:
        "Which sentence provides the strongest factual reason for reconsidering uniform payments?",
      evidence: 1,
      summary:
        "Different relocation needs challenged the fairness of identical payments.",
    },
    {
      title: "The boundaries of agreement",
      lines: [
        "A review found broad agreement among studies using a common model and dataset.",
        "It distinguished that agreement from evidence that the same conclusions would withstand different assumptions or independently collected observations.",
        "The authors recommended further tests across those boundaries rather than treating either consensus or scepticism as sufficient in itself.",
      ],
      question:
        "Which sentence identifies the precise limit of the evidence described?",
      evidence: 1,
      summary:
        "Agreement within one framework did not establish broader robustness.",
    },
    {
      title: "Judging a decision",
      lines: [
        "An emergency purchasing decision produced a favourable outcome despite departing from the usual approval process.",
        "The review separated that outcome from the question of whether the departure was justified by evidence available at the time.",
        "It concluded that a defensible assessment required examining the original uncertainty, the alternatives considered and the safeguards actually retained.",
      ],
      question:
        "Which sentence most clearly rejects judging the decision solely by its eventual outcome?",
      evidence: 1,
      summary:
        "A favourable outcome alone could not justify the earlier decision.",
    },
  ],
];
const readingPuzzles: Challenge[] = texts.flatMap((stories, index) =>
  stories.flatMap((story, item) => {
    const level = index + 1;
    const passage = story.lines.join("\n\n");
    const common = {
      track: "reading" as const,
      level,
      audioText: "",
      topic:
        level < 4
          ? "Everyday clues"
          : level < 8
            ? "Reasons and consequences"
            : "Arguments and evidence",
      passage,
      title: story.title,
    };
    const id = `game-sentence-puzzle-${level}-${item}-v1`;
    return [
      {
        ...common,
        id: `game-evidence-hunt-${level}-${item}-v1`,
        kind: "evidence-hunt" as const,
        instruction: story.question,
        choices: story.lines.map((label, line) => ({
          id: `line-${line}`,
          label,
        })),
        answer: `line-${story.evidence}`,
        explanation: `The supporting evidence is: ${story.lines[story.evidence]}`,
      },
      {
        ...common,
        id,
        kind: "sentence-puzzle" as const,
        instruction:
          "Read the short passage, then build the reference summary from all the word tiles.",
        tiles: shuffled(story.summary.split(" "), id),
        answer: story.summary,
        explanation: `Reference summary: ${story.summary} The puzzle checks the order of these supplied tiles, not every possible way to summarise the passage.`,
      },
    ];
  }),
);
export const textPuzzleChallenges = [...vocabularyPuzzles, ...readingPuzzles];
