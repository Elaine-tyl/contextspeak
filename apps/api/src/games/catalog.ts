import { openChallenges } from "./open-content";
import { beginnerChallenges } from "./beginner";
import type { GameRound, GameProgress } from "@contextspeak/contracts";
export type Challenge = GameRound & { answer: string; explanation: string };
// Original, immutable content: new revisions get new IDs so saved progress stays meaningful.
const passages = [
  [
    "Mina missed the bus. A neighbour offered her a ride to the library. She thanked him and promised to return his umbrella tomorrow.",
    "A: Are you available after lunch? B: Yes, but I need to buy a train ticket first. A: Let's meet at the station at two.",
    "The small lamp made a circle of light on the desk. Outside, rain tapped the window. Inside, a child opened a book and began a new adventure.",
    "Nora found a key inside her grandfather's old coat. A paper label said garden gate. She looked outside, but the garden had no gate.",
  ],
  [
    "Mina arrived at the community kitchen before anyone else. The delivery was late, so she made a list of ingredients already available. Instead of cancelling dinner, the volunteers changed the menu. By six o'clock, a warm meal was ready for every guest.",
    "A: I thought we agreed to meet outside the museum. B: We did, but the entrance was closed for repairs. I sent you a message. A: My phone was off. Next time, let's choose a second meeting place in advance.",
    "By morning, the river had forgotten the storm. Its smooth surface reflected the bridge and the pale sky. A cyclist paused halfway across, listening to the city wake. For one quiet minute, the journey mattered more than the destination.",
    "Nora turned the key over and noticed a tiny number scratched into the metal. At the library, she found the same number on an old map. The missing garden gate had once stood beside the railway, behind a house that nobody remembered.",
  ],
  [
    "When the town announced plans to remove the old market, Mina asked the traders what they would lose. Their answers were about more than income. The market connected neighbours who rarely met elsewhere. She recorded their stories, compared alternative proposals, and presented a plan that preserved the stalls while improving access for emergency vehicles.",
    "A: The rehearsal has been moved again, and several people are frustrated. B: I understand, but the hall is unavailable until the inspection is complete. A: Could we rehearse outdoors? B: Possibly. We should check the forecast and ask whether the equipment can be moved safely before making another announcement.",
    "At dusk, the station clock continued its patient argument with the fading light. Travellers hurried beneath it, measuring their lives in departures. An elderly woman remained on the bench, holding an unopened letter. She had arrived early, not because she feared missing the train, but because she was not yet ready to leave.",
    "The map did not show a house at all. Nora had mistaken the outline of a greenhouse for a building with rooms. Beneath a layer of dust, the librarian uncovered a photograph: her grandfather beside a young tree, holding the same labelled key. On the back, someone had written that the gate was only the beginning.",
  ],
];
const genres = ["Story", "Drama", "Literature", "Novel excerpt"] as const;
const choices: NonNullable<GameRound["choices"]> = [
  { id: "umbrella", label: "Umbrella", picture: "umbrella" },
  { id: "book", label: "Book", picture: "book" },
  { id: "bridge", label: "Bridge", picture: "bridge" },
  { id: "bicycle", label: "Bicycle", picture: "bicycle" },
];
const spoken = [
  [
    [
      "I have pages and a cover, but I am not a door. What am I? Explain your answer.",
      "book",
    ],
    [
      "A meeting starts at three. Your bus arrives at two forty-five, and the walk takes ten minutes. Will you be on time? Explain.",
      "Yes. You arrive at two fifty-five, five minutes early.",
    ],
    [
      "Invite a friend to meet this weekend. Suggest a time and place, then ask whether they are available.",
      "A polite invitation with a time, a place and a question about availability.",
    ],
  ],
  [
    [
      "I connect two banks, but I do not keep money. People cross me to reach the other side. What am I? Explain both meanings of bank.",
      "bridge; banks here are the sides of a river",
    ],
    [
      "You need a train ticket and a library book. The ticket office closes at five, the library at six. It is four thirty. Which should you visit first, and why?",
      "Visit the ticket office first because it closes earlier, then the library.",
    ],
    [
      "Your friend cancels your plan at the last minute. Respond politely, suggest an alternative and ask a follow-up question.",
      "An understanding response, an alternative plan and a relevant question.",
    ],
  ],
  [
    [
      "I can be broken without being touched. Keeping me can build trust, but making me is easier than fulfilling me. What am I? Explain your reasoning.",
      "a promise",
    ],
    [
      "Three people share a project. One can work only mornings, another only afternoons, and the third can work either. Propose a fair plan that allows a handover without requiring everyone to meet together.",
      "A workable morning and afternoon plan, using the flexible person for handover and explaining fairness.",
    ],
    [
      "A community event has lost its venue. Propose a practical alternative, explain one trade-off and persuade a doubtful organiser to consider it.",
      "A practical alternative, an explicit trade-off and persuasive reasoning.",
    ],
  ],
];
const legacyChallenges: Challenge[] = passages.flatMap((texts, index) => {
  const level = index + 1;
  const base = texts.map((text, genre) => ({
    id: `listen-${level}-${genre}-v1`,
    track: "listening" as const,
    kind: "dictation" as const,
    level: level + 7,
    title: `${genres[genre]} dictation`,
    genre: genres[genre],
    instruction: "Listen, then type exactly what you hear.",
    audioText: text,
    answer: text,
    explanation:
      "Compare the original passage with your checked answer. Case and punctuation are ignored.",
  }));
  const picture = ["umbrella", "bridge", "bicycle"][index];
  return [
    ...base,
    {
      id: `picture-${level}-v1`,
      track: "listening",
      kind: "picture",
      level: level + 7,
      title: "Hear it, find it",
      instruction: "Listen and choose the matching picture.",
      audioText: [
        "umbrella",
        "A bridge connects the two sides of a river.",
        "Choose the vehicle with two wheels that you pedal.",
      ][index],
      choices,
      answer: picture,
      explanation: `The word was ${picture}.`,
    },
    {
      id: `question-${level}-v1`,
      track: "listening",
      kind: "comprehension",
      level: level + 7,
      title: "Listen and answer",
      instruction: [
        "Where will they meet?",
        "Why did the volunteers change the menu?",
        "What did the traders value besides income?",
      ][index],
      audioText: texts[index === 0 ? 1 : 0],
      choices:
        index === 0
          ? [
              { id: "station", label: "At the station" },
              { id: "library", label: "At the library" },
              { id: "kitchen", label: "At the kitchen" },
            ]
          : index === 1
            ? [
                { id: "delivery", label: "The delivery was late" },
                { id: "guests", label: "Nobody arrived" },
                { id: "weather", label: "It was raining" },
              ]
            : [
                { id: "community", label: "Connections between neighbours" },
                { id: "parking", label: "Free parking" },
                { id: "travel", label: "Shorter journeys" },
              ],
      answer: ["station", "delivery", "community"][index],
      explanation: [
        "They agree to meet at the station at two.",
        "The late delivery made them use available ingredients.",
        "The market connected neighbours who rarely met elsewhere.",
      ][index],
    },
    ...spoken[index].map(([prompt, answer], kind) => ({
      id: `speak-${level}-${kind}-v1`,
      criteria:
        kind === 0
          ? [
              "Name a plausible answer",
              "Explain how a clue supports your answer",
            ]
          : kind === 1
            ? ["Give a workable solution", "Explain your reasoning"]
            : level === 1
              ? ["Suggest a time and place", "Ask about availability"]
              : level === 2
                ? [
                    "Respond politely",
                    "Suggest an alternative and ask a follow-up",
                  ]
                : [
                    "Propose a practical venue",
                    "Explain a trade-off and persuade the organiser",
                  ],
      track: "speaking" as const,
      kind: (["riddle", "puzzle", "pk"] as const)[kind],
      level: level + 7,
      title: ["Speak the riddle", "Talk through a puzzle", "AI sparring match"][
        kind
      ],
      instruction:
        "Listen to the challenge, then explain your answer aloud. Feedback is automatic when recording finishes.",
      audioText: prompt,
      answer,
      explanation:
        "The game mark combines your AI overall estimate and task-response estimate equally.",
      ...(kind === 2 ? { opponentScore: [65, 75, 85][index] } : {}),
    })),
  ];
});
export const challenges = [
  ...beginnerChallenges,
  ...legacyChallenges,
  ...openChallenges,
];
export function gameProgress(passedIds: string[]): GameProgress {
  const passed = new Set(passedIds);
  const result = {} as GameProgress;
  for (const track of ["listening", "speaking"] as const) {
    const counts = Array.from({ length: 10 }, (_, index) => index + 1).map(
      (level) =>
        challenges.filter(
          (round) =>
            round.track === track &&
            round.level === level &&
            passed.has(round.id),
        ).length,
    );
    // Keep access to moved v1 tasks; immutable IDs preserve old receipts.
    const legacyLevel = Math.max(
      1,
      ...legacyChallenges
        .filter((round) => round.track === track && passed.has(round.id))
        .map((round) => round.level),
    );
    let level = legacyLevel;
    while (level < 10 && counts[level - 1] >= 3) level++;
    result[track] = { level, passed: counts };
  }
  return result;
}
export function publicRound({
  answer,
  explanation,
  ...round
}: Challenge): GameRound {
  return { ...round, topic: roundTopic(round) };
}

export function roundTopic(round: GameRound) {
  if (round.topic) {
    const labels: Record<string, string> = {
      help: "Asking for help",
      meet: "Meeting people",
      time: "Time",
      plan: "Making plans",
      library: "Reading",
      story: "Stories",
      greetings: "Greetings",
      introductions: "Introductions",
      home: "Home",
      friends: "Friends",
      plans: "Making plans",
    };
    return labels[round.topic] ?? round.topic;
  }
  if (round.genre) return round.genre === "Story" ? "Stories" : round.genre;
  if (round.kind === "picture")
    return /umbrella/.test(round.audioText) ? "Weather" : "Travel";
  if (round.kind === "comprehension") return "Making plans";
  if (round.kind === "pk") return "Making plans";
  if (round.kind === "riddle")
    return round.level <= 3 ? "Everyday words" : "Wordplay";
  if (round.kind === "puzzle")
    return round.level <= 3 ? "Everyday questions" : "Solving problems";
  return "Everyday English";
}
