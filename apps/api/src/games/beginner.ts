import type { Challenge } from "./catalog";

// Original short tasks bridge single words to the existing long-form excerpts.
const stages = [
  {
    texts: ["Hello.", "Thank you.", "A book."],
    speaking: [
      "What do you say when you meet someone?",
      "Say the number after one.",
      "Say hello to a friend.",
    ],
    answers: ["Hello.", "Two.", "Hello."],
    criteria: [
      "Give one relevant word or short phrase. No explanation is needed.",
    ],
  },
  {
    texts: ["My name is Mina.", "I like this book.", "The bus is here."],
    speaking: [
      "What is your name?",
      "Name something you like to eat.",
      "Invite a friend to sit down.",
    ],
    answers: ["My name is Mina.", "I like rice.", "Please sit down."],
    criteria: ["Give one simple relevant sentence."],
  },
  {
    texts: [
      "I read a book every day.",
      "Can you help me, please?",
      "The library opens at nine.",
    ],
    speaking: [
      "What do you do in the morning?",
      "You need a pen. Ask a friend for one.",
      "Ask a friend to meet tomorrow.",
    ],
    answers: [
      "I eat breakfast in the morning.",
      "Can I borrow a pen, please?",
      "Can we meet tomorrow?",
    ],
    criteria: ["Answer with a complete everyday sentence."],
  },
  {
    texts: [
      "Mina wants some tea. Her friend brings a cup.",
      "A: Are you free today? B: Yes, after lunch.",
      "The book is on the table beside the window.",
    ],
    speaking: [
      "Tell a friend what you like to do after class.",
      "Your bus is late. What could you do instead?",
      "Invite a friend for lunch and suggest a time.",
    ],
    answers: [
      "I like to walk after class.",
      "I could walk or take another bus.",
      "Would you like lunch at twelve?",
    ],
    criteria: ["Give one or two relevant sentences."],
  },
  {
    texts: [
      "Mina walks to the library after lunch. She wants to borrow a storybook.",
      "A: Shall we meet at two? B: I finish work at three. Can we meet later?",
      "It started to rain on the way home. Nora opened her umbrella and kept walking.",
    ],
    speaking: [
      "Describe a book you like and say why you like it.",
      "Your friend forgot an umbrella. Suggest a way to help.",
      "Suggest a weekend activity and give one reason.",
    ],
    answers: [
      "I like short stories because they are fun.",
      "We can share my umbrella.",
      "Let's go to the park because the weather is nice.",
    ],
    criteria: [
      "Give a relevant answer",
      "Add one simple reason or useful detail",
    ],
  },
  {
    texts: [
      "The train was late, so Mina called her friend. They decided to meet at the cafe instead of the station.",
      "A: I cannot find my ticket. B: Check your bag first. If it is not there, ask at the ticket office.",
      "Nora planted a small tree by the window. Every morning she checked the soil and gave it a little water.",
    ],
    speaking: [
      "Tell a friend about a time you needed help. What happened?",
      "Your usual cafe is closed. Suggest another place and explain your choice.",
      "Make a plan with a friend and ask one follow-up question.",
    ],
    answers: [
      "I was lost, so I asked someone for directions.",
      "We could go to the library cafe because it is close.",
      "Let's meet on Saturday. What time are you free?",
    ],
    criteria: [
      "Give two connected sentences",
      "Include a reason or relevant follow-up",
    ],
  },
  {
    texts: [
      "Mina planned a picnic for Saturday. The forecast said it might rain, so she booked a room at the community centre as a backup. Her friends could still meet even if the weather changed.",
      "A: The library is closing early today. B: Then we should borrow the books before lunch. A: Good idea. We can visit the museum afterwards if we have time.",
      "Nora found a letter inside a second-hand book. It described a village she had never visited. She read it twice, wondering who had written it and why it had been left there.",
    ],
    speaking: [
      "Tell a short story about a plan that changed. Explain what you did next.",
      "Choose between walking and taking a bus to meet a friend. Explain one advantage of your choice.",
      "Invite a friend to an event, suggest a backup plan and ask what they think.",
    ],
    answers: [
      "Our picnic was cancelled because of rain, so we met indoors.",
      "I would take the bus because it is faster.",
      "Let's go to the park. If it rains, we can meet at a cafe. What do you think?",
    ],
    criteria: [
      "Give two or three connected sentences",
      "Explain your choice or next step",
    ],
  },
];
export const beginnerChallenges: Challenge[] = stages.flatMap(
  (stage, index) => {
    const level = index + 1;
    const picture = index < 3 ? "umbrella" : "bridge";
    const meeting = [
      "station",
      "library",
      "kitchen",
      "station",
      "kitchen",
      "library",
      "station",
    ][index];
    return [
      ...stage.texts.map((text, genre): Challenge => ({
        id: `listen-${level}-${genre}-v2`,
        track: "listening",
        kind: "dictation",
        level,
        title: `${(level <= 2 ? ["Word", "Phrase", "Sentence"] : ["Story", "Drama", "Literature"])[genre]} dictation`,
        ...(level > 2
          ? { genre: (["Story", "Drama", "Literature"] as const)[genre] }
          : {}),
        instruction: "Listen, then type exactly what you hear.",
        audioText: text,
        answer: text,
        explanation:
          "Case and punctuation are ignored. Compare your answer with the original.",
      })),
      {
        id: `picture-${level}-v2`,
        track: "listening",
        kind: "picture",
        level,
        title: "Hear it, find it",
        instruction: "Listen and choose the matching picture.",
        audioText:
          level === 1
            ? "umbrella"
            : level < 4
              ? "Take an umbrella."
              : "We cross the river on a bridge.",
        choices: [
          { id: "umbrella", label: "Umbrella", picture: "umbrella" },
          { id: "book", label: "Book", picture: "book" },
          { id: "bridge", label: "Bridge", picture: "bridge" },
          { id: "bicycle", label: "Bicycle", picture: "bicycle" },
        ],
        answer: picture,
        explanation: `The answer is ${picture}.`,
      },
      {
        id: `question-${level}-v2`,
        track: "listening",
        kind: "comprehension",
        level,
        title: "Listen and answer",
        instruction: "Where will they meet?",
        audioText: [
          "Meet me at the station.",
          "We will meet at the library at two.",
          "Mina and Nora will meet in the kitchen after lunch.",
          "Mina wanted to meet at the library, but it was closed. They chose the station instead.",
          "Nora suggested the station, but Mina needed to cook lunch. They agreed to meet in the kitchen.",
          "Mina called Nora to change their meeting place. The station was too noisy, so they chose the library, where both could find books for their class.",
          "Although Nora preferred the library because it was quieter, Mina was travelling later that afternoon. They agreed that the station would be more convenient and decided to meet there after lunch.",
        ][index],
        choices: [
          { id: "station", label: "At the station" },
          { id: "library", label: "At the library" },
          { id: "kitchen", label: "At the kitchen" },
        ],
        answer: meeting,
        explanation: `They choose the ${meeting} as their meeting place.`,
      },
      ...stage.speaking.map((prompt, kind): Challenge => ({
        id: `speak-${level}-${kind}-v2`,
        track: "speaking",
        kind: (["riddle", "puzzle", "pk"] as const)[kind],
        level,
        title: [
          "Speak the riddle",
          "Talk through a puzzle",
          "AI sparring match",
        ][kind],
        instruction:
          level <= 2
            ? "Listen and give a short answer aloud. One word or one sentence can be enough."
            : "Listen and answer aloud. Feedback is automatic after recording.",
        audioText: prompt,
        answer: stage.answers[kind],
        criteria: stage.criteria,
        explanation:
          "The game mark combines your AI overall estimate and task-response estimate equally.",
        ...(kind === 2 ? { opponentScore: 40 + level * 3 } : {}),
      })),
    ];
  },
);
