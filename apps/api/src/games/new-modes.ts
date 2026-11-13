import {
  pictureLevels,
  pictureLibrary,
  type PictureId,
} from "@contextspeak/contracts";
import type { Challenge } from "./catalog";

// Original, immutable content. New revisions get new IDs so saved progress stays meaningful.

/** Deterministic shuffle so every learner sees the same, reviewable order. */
export function shuffle<T>(items: readonly T[], seed: string): T[] {
  let state = 2166136261;
  for (const char of seed)
    state = Math.imul(state ^ char.charCodeAt(0), 16777619) >>> 0;
  const result = [...items];
  for (let index = result.length - 1; index > 0; index--) {
    state = Math.imul(state ^ (state >>> 15), 2246822507) >>> 0;
    const swap = state % (index + 1);
    [result[index], result[swap]] = [result[swap], result[index]];
  }
  return result;
}

// ---------------------------------------------------------------- picture choice
// Each level has its own picture set: 4 pictures (levels 1–3), 6 (4–6) and a
// 3 × 3 grid of similar items (7–10). Clues move from naming to inference.
const pictureClues: Record<number, [PictureId, string][]> = {
  1: [
    ["umbrella", "Find the umbrella. I open my umbrella when it rains."],
    ["bicycle", "Find the bicycle. I ride my bicycle to the park."],
    ["book", "Find the book. She is reading a book in bed."],
  ],
  2: [
    ["bread", "Find the bread. We buy fresh bread every morning."],
    ["cake", "Find the cake. There is a candle on the birthday cake."],
    ["coffee", "Find the coffee. He drinks a hot cup of coffee."],
  ],
  3: [
    ["key", "Find the key. I can't open the door without my key."],
    ["clock", "Find the clock. Look at the clock — it's four o'clock."],
    ["lamp", "Find the lamp. Turn on the lamp; it's getting dark."],
  ],
  4: [
    ["plane", "It flies above the clouds and lands at an airport."],
    ["train", "It runs on rails and stops at every station."],
    ["boat", "It floats on the water and has a sail."],
  ],
  5: [
    ["rabbit", "It has long ears, a fluffy tail and loves carrots."],
    ["fish", "It lives in water and swims with its fins."],
    ["dog", "It barks at the door and loves to play fetch."],
  ],
  6: [
    ["glasses", "People wear these on their face to see more clearly."],
    ["shoe", "You put this on your foot before you go outside."],
    ["scarf", "You wrap this around your neck when the weather is cold."],
  ],
  7: [
    [
      "banana",
      "It's long, curved and yellow, and you peel it before you eat it.",
    ],
    [
      "lemon",
      "It's yellow too, but it tastes very sour. People squeeze it into drinks.",
    ],
    ["grapes", "These small round fruits grow together in a bunch."],
  ],
  8: [
    [
      "door",
      "Sam keeps hearing noise from the kitchen while he works in the living room. He doesn't want new furniture, and a window would not help, because the noise travels through the open gap between the two rooms. He needs something he can open to walk through and close when he wants quiet. Which picture shows it?",
    ],
    [
      "window",
      "The room is dark during the day, and the family wants natural light rather than another electric lamp. They would also like to see the garden and let in fresh air on warm days, but they do not need another way to walk in and out of the house. Which picture shows what they should add?",
    ],
    [
      "sofa",
      "A visitor is staying for the weekend. There is no spare bed, a single chair is too small to lie on, and the table is needed for meals. The host wants something in the living room where two people can sit together during the day and one person could sleep at night. Which picture fits?",
    ],
  ],
  9: [
    [
      "scooter",
      "The student's journey is short, but the bus is often late and there is nowhere to lock a bicycle at her office. She wants something without an engine that folds up small enough to carry inside, and that she pushes along the pavement with one foot. Which picture is it?",
    ],
    [
      "truck",
      "A bakery has to move two hundred loaves to supermarkets across the city every morning. A car is far too small, the train does not stop near the shops, and a helicopter would cost much more than the bread is worth. Which vehicle should they use for deliveries by road?",
    ],
    [
      "helicopter",
      "After an accident in the mountains, rescuers must reach a climber quickly. There is no road for an ambulance, the lake is frozen so a boat is useless, and there is nowhere flat enough for a plane to land. Which vehicle can take off and land almost anywhere?",
    ],
  ],
  10: [
    [
      "umbrella",
      "The festival has moved outdoors at the last minute and heavy rain is forecast. Visitors are told that a hat will not be enough and that waterproof coats will be handed out at the gate. They should still bring one item that folds up small, is held above the head and keeps the rain off. Which picture is it?",
    ],
    [
      "glasses",
      "Grandpa can hear the radio perfectly and the lamp is already switched on, yet he still cannot read the small print on his medicine box. He doesn't need another book or a clock; he needs something that he wears on his face. Which picture shows what he is looking for?",
    ],
    [
      "book",
      "During a power cut, the students need to check the meaning of an unfamiliar word. Their phones are flat, the lamp won't work and there is no clock to say how long the cut will last. Which picture shows something they can still use to look the word up?",
    ],
  ],
};

export const pictureChallenges: Challenge[] = Object.entries(
  pictureClues,
).flatMap(([key, clues]) => {
  const level = Number(key);
  const set = pictureLevels[level];
  return clues.map(([answer, audioText], index): Challenge => ({
    id: `game-picture-${level}-${index}-v4`,
    track: "listening",
    kind: "picture",
    level,
    topic: set.theme,
    title:
      set.pictures.length === 9
        ? "Nine-picture challenge"
        : set.pictures.length === 6
          ? "Six-picture challenge"
          : "Picture match",
    instruction: "Listen and choose the matching picture.",
    audioText,
    choices: shuffle(set.pictures, `${level}-${index}`).map((id) => ({
      id,
      label: pictureLibrary[id].label,
      picture: id,
    })),
    answer,
    explanation: `The answer is the ${pictureLibrary[answer].label.toLowerCase()}.`,
  }));
});

// ---------------------------------------------------------------- spelling bee
const spellingWords: [string, string][][] = [
  [
    ["cat", "The cat is asleep."],
    ["sun", "The sun is hot today."],
    ["bus", "I take the bus to school."],
  ],
  [
    ["fish", "We had fish for dinner."],
    ["milk", "Can I have some milk?"],
    ["door", "Please close the door."],
  ],
  [
    ["apple", "She ate a red apple."],
    ["bread", "We need more bread."],
    ["chair", "Sit on this chair."],
  ],
  [
    ["window", "Open the window, please."],
    ["orange", "I'd like an orange juice."],
    ["ticket", "I lost my train ticket."],
  ],
  [
    ["library", "The library is quiet."],
    ["weather", "The weather is lovely."],
    ["holiday", "We're on holiday next week."],
  ],
  [
    ["breakfast", "I eat breakfast at seven."],
    ["umbrella", "Take an umbrella with you."],
    ["kitchen", "Dinner is in the kitchen."],
  ],
  [
    ["necessary", "Is it necessary to book?"],
    ["beautiful", "What a beautiful view."],
    ["calendar", "Check the calendar."],
  ],
  [
    ["restaurant", "Let's try the new restaurant."],
    ["February", "My birthday is in February."],
    ["business", "She runs a small business."],
  ],
  [
    ["accommodate", "The hotel can accommodate fifty guests."],
    ["rhythm", "Dance to the rhythm."],
    ["separate", "Keep the papers separate."],
  ],
  [
    ["conscientious", "He is a conscientious worker."],
    ["pronunciation", "Her pronunciation is clear."],
    ["questionnaire", "Please fill in the questionnaire."],
  ],
];
const decoys = "aeiourstlnm";

export const spellingChallenges: Challenge[] = spellingWords.flatMap(
  (words, index) => {
    const level = index + 1;
    const extra = level <= 3 ? 0 : level <= 6 ? 2 : 3;
    return words.map(([word, sentence], position): Challenge => {
      const letters = [...word.toLowerCase()];
      const added = shuffle([...decoys], word).slice(0, extra);
      return {
        id: `game-spelling-${level}-${position}-v1`,
        track: "listening",
        kind: "spelling",
        level,
        topic: "Spelling",
        title: "Spelling bee",
        instruction:
          extra > 0
            ? "Listen, then build the word from the letter tiles. Some letters are not needed."
            : "Listen, then build the word from the letter tiles.",
        audioText: `Spell the word ${word}. ${sentence} ${word}.`,
        tiles: shuffle([...letters, ...added], `${word}-tiles`),
        answer: word,
        explanation: `${word} is spelled ${letters.join("-").toUpperCase()}.`,
      };
    });
  },
);

// ---------------------------------------------------------------- sentence builder
const orderSentences = [
  ["I like green tea", "The cat is small", "We walk to school"],
  ["She has a red bike", "My bag is on the chair", "They eat lunch at noon"],
  [
    "Can I have a glass of water",
    "The bus leaves at nine",
    "He reads a book every night",
  ],
  [
    "We are going to the beach on Sunday",
    "Could you open the window please",
    "The library closes at six today",
  ],
  [
    "I have never been to Japan before",
    "Please turn off the lights when you leave",
    "My brother is better at maths than me",
  ],
  [
    "If it rains we will stay at home",
    "The meeting was moved to next Tuesday",
    "She asked me where the station was",
  ],
  [
    "Although it was late the shop was still open",
    "I would rather walk than wait for the bus",
    "The tickets had already sold out by Friday",
  ],
  [
    "Not only did he apologise but he also paid for the damage",
    "The report should have been finished before the deadline",
    "Hardly had we arrived when it started to rain",
  ],
  [
    "Had I known about the delay I would have taken an earlier train",
    "The new policy is likely to affect people who work from home",
    "Despite the warnings many residents refused to leave their homes",
  ],
  [
    "It is widely believed that the decision was influenced by political pressure",
    "Only after reading the contract carefully did she realise the risk",
    "The committee is unlikely to approve the plan unless costs are reduced",
  ],
];
const orderDecoys = ["the", "very", "was", "to"];

export const wordOrderChallenges: Challenge[] = orderSentences.flatMap(
  (sentences, index) => {
    const level = index + 1;
    return sentences.map((sentence, position): Challenge => {
      // Keep "I" and proper nouns capitalised; lower-case the first word so it isn't a giveaway.
      const words = sentence
        .split(" ")
        .map((word, at) =>
          at === 0 &&
          word !== "I" &&
          !/^(Japan|Sunday|Tuesday|Friday)$/.test(word)
            ? word.toLowerCase()
            : word,
        );
      const extra =
        level >= 8 ? [orderDecoys[position % orderDecoys.length]] : [];
      return {
        id: `game-word-order-${level}-${position}-v1`,
        track: "listening",
        kind: "word-order",
        level,
        topic: "Word order",
        title: "Sentence builder",
        instruction:
          extra.length > 0
            ? "Listen, then tap the words in the order you hear them. One word is not needed."
            : "Listen, then tap the words in the order you hear them.",
        audioText: `${sentence}.`,
        tiles: shuffle([...words, ...extra], `${sentence}-tiles`),
        answer: `${sentence}.`,
        explanation: `The sentence is: ${sentence}.`,
      };
    });
  },
);

// ---------------------------------------------------------------- sound detective
const pairs: [string[], string][][] = [
  [
    [["ship", "sheep"], "sheep"],
    [["cat", "cut"], "cat"],
    [["bed", "bad"], "bed"],
  ],
  [
    [["light", "right"], "right"],
    [["fan", "van"], "van"],
    [["pen", "pan"], "pan"],
  ],
  [
    [["hat", "hut"], "hut"],
    [["sick", "thick"], "thick"],
    [["bag", "back"], "back"],
  ],
  [
    [["walk", "work", "woke"], "work"],
    [["full", "fool", "fall"], "fool"],
    [["live", "leave", "love"], "leave"],
  ],
  [
    [["three", "tree", "free"], "three"],
    [["coat", "caught", "cot"], "caught"],
    [["pull", "pool", "pole"], "pool"],
  ],
  [
    [["berry", "very", "fairy"], "very"],
    [["sink", "think", "zinc"], "think"],
    [["ride", "write", "light"], "ride"],
  ],
  [
    [["thirty", "thirteen", "forty", "fourteen"], "thirteen"],
    [["sit", "seat", "set", "sat"], "seat"],
    [["rice", "rise", "race", "raise"], "raise"],
  ],
  [
    [["desert", "dessert", "deserve", "desire"], "dessert"],
    [["walked", "worked", "woke", "walk"], "worked"],
    [["fifteen", "fifty", "sixteen", "sixty"], "fifty"],
  ],
  [
    [["accept", "except", "expect", "aspect"], "except"],
    [["price", "prize", "pride", "bride"], "prize"],
    [["lose", "loose", "lost", "loss"], "loose"],
  ],
  [
    [["quite", "quiet", "quit", "quote"], "quiet"],
    [["affect", "effect", "infect", "effort"], "effect"],
    [["personal", "personnel", "personally", "persona"], "personnel"],
  ],
];

export const minimalPairChallenges: Challenge[] = pairs.flatMap(
  (items, index) => {
    const level = index + 1;
    return items.map(([options, answer], position): Challenge => ({
      id: `game-minimal-pairs-${level}-${position}-v1`,
      track: "listening",
      kind: "minimal-pairs",
      level,
      topic: "Similar sounds",
      title: "Sound detective",
      instruction: "Listen carefully. Which word do you hear?",
      // Repeat the word; higher levels give no helpful context sentence.
      audioText: level <= 3 ? `${answer}. ${answer}.` : `${answer}.`,
      choices: shuffle(options, `${answer}-${level}`).map((word) => ({
        id: word,
        label: word,
      })),
      answer,
      explanation: `The word was “${answer}”. Listen for the sound that makes it different from ${options
        .filter((word) => word !== answer)
        .map((word) => `“${word}”`)
        .join(" and ")}.`,
    }));
  },
);

// ---------------------------------------------------------------- quick fire
const statements: [PictureId, string, boolean][][] = [
  [
    ["cat", "This animal says meow.", true],
    ["bus", "This is a kind of fruit.", false],
    ["apple", "You can eat this.", true],
  ],
  [
    ["umbrella", "You use this when it rains.", true],
    ["fish", "This animal lives in trees.", false],
    ["clock", "This tells you the time.", true],
  ],
  [
    ["bed", "People usually sleep on this.", true],
    ["shoe", "You wear this on your head.", false],
    ["key", "You use this to open a door.", true],
  ],
  [
    ["plane", "This vehicle travels on rails.", false],
    ["lemon", "This fruit tastes as sweet as sugar.", false],
    ["glasses", "These help some people see clearly.", true],
  ],
  [
    ["bridge", "This lets people cross a river.", true],
    ["rabbit", "This animal has short ears and a long tail.", false],
    ["sofa", "More than one person can sit on this.", true],
  ],
  [
    ["scarf", "People usually wear this in hot weather.", false],
    ["helicopter", "This can take off without a runway.", true],
    ["bread", "This is made mainly from flour.", true],
  ],
  [
    ["book", "You need electricity to read this.", false],
    ["duck", "This bird can both swim and fly.", true],
    ["window", "This lets light into a room.", true],
  ],
  [
    ["bicycle", "This vehicle needs fuel to move.", false],
    ["cheese", "This is usually made from milk.", true],
    ["truck", "This is mainly used to carry goods.", true],
  ],
  [
    ["glove", "You would normally wear this on your foot.", false],
    ["phone", "You can use this to call someone far away.", true],
    ["grapes", "This fruit grows in bunches.", true],
  ],
  [
    [
      "umbrella",
      "Although it isn't clothing, this protects you from the rain.",
      true,
    ],
    [
      "coffee",
      "This drink is usually served cold and contains no caffeine.",
      false,
    ],
    [
      "bridge",
      "This structure moves passengers from one bank to the other by itself.",
      false,
    ],
  ],
];

export const trueFalseChallenges: Challenge[] = statements.flatMap(
  (items, index) => {
    const level = index + 1;
    const timeLimit = level <= 3 ? 15 : level <= 6 ? 10 : 7;
    return items.map(([picture, statement, truth], position): Challenge => ({
      id: `game-true-false-${level}-${position}-v1`,
      track: "listening",
      kind: "true-false",
      level,
      topic: "Quick fire",
      title: "Quick fire",
      instruction: `Look at the picture, listen, and answer within ${timeLimit} seconds for full marks.`,
      audioText: statement,
      picture,
      timeLimit,
      choices: [
        { id: "true", label: "True" },
        { id: "false", label: "False" },
      ],
      answer: truth ? "true" : "false",
      explanation: `“${statement}” is ${truth ? "true" : "false"} for the ${pictureLibrary[picture].label.toLowerCase()}.`,
    }));
  },
);

/** Letter-level similarity, so a near miss in spelling still earns part marks. */
export function gradeSpelling(expected: string, response: string) {
  const a = expected.toLowerCase();
  const b = response.toLowerCase().replace(/\s+/g, "");
  const row = Array.from({ length: b.length + 1 }, (_, index) => index);
  for (let i = 1; i <= a.length; i++) {
    let previous = row[0];
    row[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const current = row[j];
      row[j] = Math.min(
        row[j] + 1,
        row[j - 1] + 1,
        previous + (a[i - 1] === b[j - 1] ? 0 : 1),
      );
      previous = current;
    }
  }
  const distance = row[b.length];
  return distance === 0
    ? 100
    : Math.max(0, Math.min(99, Math.round(100 * (1 - distance / a.length))));
}

/** Correct inside the time limit earns full marks; correct but late earns 60. */
export function gradeQuickFire(
  correct: boolean,
  elapsedMs: number | undefined,
  timeLimit: number,
) {
  if (!correct) return 0;
  return elapsedMs !== undefined && elapsedMs <= timeLimit * 1000 ? 100 : 60;
}

export const newModeChallenges: Challenge[] = [
  ...pictureChallenges,
  ...spellingChallenges,
  ...wordOrderChallenges,
  ...minimalPairChallenges,
  ...trueFalseChallenges,
];
