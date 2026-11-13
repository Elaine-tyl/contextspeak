import { z } from "zod";
import { speakingUploadSchema, type SpeakingAssessment } from "./speaking";
import type { PracticeRecommendation, DictationFeedback } from "./practice";
import type { PictureId } from "./pictures";
export type { PictureId } from "./pictures";
export const gameTracks = [
  "listening",
  "speaking",
  "vocabulary",
  "reading",
] as const;
export type GameTrack = (typeof gameTracks)[number];
export const gameTrackLabels: Record<GameTrack, string> = {
  listening: "Listening games",
  speaking: "Speaking games",
  vocabulary: "Vocabulary games",
  reading: "Reading games",
};
export function gameRequiresAudio(round: {
  track: GameTrack;
  recall?: boolean;
}) {
  return (
    round.track === "listening" || round.track === "speaking" || !!round.recall
  );
}
export type GameKind =
  | "dictation"
  | "gap-fill"
  | "scene-words"
  | "picture"
  | "comprehension"
  | "response"
  | "riddle"
  | "puzzle"
  | "pk"
  | "spelling"
  | "word-order"
  | "minimal-pairs"
  | "true-false"
  | "word-meaning"
  | "word-context"
  | "reading-question"
  | "reading-order"
  | "word-scramble"
  | "mini-crossword"
  | "evidence-hunt"
  | "sentence-puzzle";
export type CrosswordEntry = {
  clue: string;
  row: number;
  column: number;
  direction: "across" | "down";
  length: number;
};
export type WordScene = {
  room:
    | "bedroom"
    | "kitchen"
    | "cafe"
    | "market"
    | "apartment"
    | "restaurant"
    | "airport";
  focus:
    | "wardrobe"
    | "mirror"
    | "blanket"
    | "kettle"
    | "cupboard"
    | "toaster"
    | "receipt"
    | "pastry"
    | "counter"
    | "produce"
    | "scales"
    | "basket"
    | "document"
    | "menu"
    | "suitcase"
    | "fan"
    | "window"
    | "building";
};
export type SceneWordNotes = {
  word: string;
  meaning: string;
  translation: string;
  example: string;
  phrases: string[];
};
export type GameRound = {
  id: string;
  track: GameTrack;
  kind: GameKind;
  level: number;
  title: string;
  genre?: "Story" | "Drama" | "Literature" | "Novel excerpt";
  instruction: string;
  audioText: string;
  /** Visible stimulus for text games; never the solution to paragraph ordering. */
  passage?: string;
  /** New reading editions: show marked words until the first completed narration. */
  recall?: true;
  scene?: WordScene;
  gapText?: string[];
  crossword?: CrosswordEntry[];
  archived?: boolean;
  topic?: string;
  source?: {
    id: string;
    provider: "Tatoeba" | "VOA Learning English" | "African Storybook";
    title: string;
    url: string;
    author: string;
    license: string;
    licenseUrl: string;
    publishedAt?: string;
  };
  choices?: { id: string; label: string; picture?: PictureId }[];
  /** Spelling (letters) and word-order (words): shuffled tiles, may include decoys. */
  tiles?: string[];
  /** True-or-false: the picture the spoken statement is about. */
  picture?: PictureId;
  /** True-or-false: seconds for full marks once the audio has finished. */
  timeLimit?: number;
  criteria?: string[];
  opponentScore?: number;
};
type TrackProgress = { level: number; passed: number[] };
// Older saved catalogs contain only the two original audio tracks.
export type GameProgress = Record<"listening" | "speaking", TrackProgress> &
  Partial<Record<"vocabulary" | "reading", TrackProgress>>;
export type GameCatalog = {
  rounds: GameRound[];
  progress: GameProgress;
  modeProgress?: Record<GameKind, { level: number }>;
  recommendations?: Partial<Record<GameTrack, PracticeRecommendation>>;
};
export const gameSubmissionSchema = z
  .object({
    eventId: z.uuid(),
    levelSessionId: z.uuid().optional(),
    roundId: z.string().min(1).max(80),
    response: z.string().trim().max(2000).optional(),
    gapAnswers: z
      .array(z.string().trim().min(1).max(80))
      .min(1)
      .max(6)
      .optional(),
    choice: z.string().min(1).max(80).optional(),
    completedPlays: z.number().int().min(0).max(20),
    /** True-or-false: milliseconds from the end of the audio to the answer. */
    elapsedMs: z.number().int().min(0).max(600000).optional(),
    audio: speakingUploadSchema.shape.audio.optional(),
    mimeType: speakingUploadSchema.shape.mimeType.optional(),
    durationMs: z.number().int().min(1).max(60000).optional(),
  })
  .strict();
export type GameSubmission = z.infer<typeof gameSubmissionSchema>;
export type GapFillFeedback = {
  score: number;
  words: { answer: string; expected: string; correct: boolean }[];
};
export type GameReceipt = {
  id: string;
  roundId: string;
  score: number | null;
  createdAt: string;
  feedback: {
    independent?: boolean;
    explanation: string;
    correctAnswer?: string;
    correct?: boolean;
    dictation?: DictationFeedback;
    gapFill?: GapFillFeedback;
    sceneWordNotes?: SceneWordNotes;
    speaking?: SpeakingAssessment;
    opponentScore?: number;
  };
};

export const gameModes = [
  {
    kind: "scene-words",
    track: "listening",
    label: "Scene words",
    description: "Explore a 3D room, listen and type the missing word.",
  },
  {
    kind: "dictation",
    track: "listening",
    label: "Dictation",
    description: "Listen and write what you hear.",
  },
  {
    kind: "gap-fill",
    track: "listening",
    label: "Missing words",
    description: "Hear a sentence and fill in the missing words.",
  },
  {
    kind: "picture",
    track: "listening",
    label: "Picture choice",
    description: "Hear a word or clue and choose a picture.",
  },
  {
    kind: "comprehension",
    track: "listening",
    label: "Listening questions",
    description: "Listen to a passage and answer a question.",
  },
  {
    kind: "response",
    track: "speaking",
    label: "Speaking questions",
    description: "Hear an everyday question and answer in your own words.",
  },
  {
    kind: "riddle",
    track: "speaking",
    label: "Riddles",
    description: "Listen to a clue and say your answer.",
  },
  {
    kind: "puzzle",
    track: "speaking",
    label: "Puzzles",
    description: "Solve an everyday problem aloud.",
  },
  {
    kind: "pk",
    track: "speaking",
    label: "AI PK",
    description: "Practise a spoken challenge against a simulated AI target.",
  },
  {
    kind: "spelling",
    track: "listening",
    label: "Spelling bee",
    description: "Hear a word and build it from letter tiles.",
  },
  {
    kind: "word-order",
    track: "listening",
    label: "Sentence builder",
    description: "Hear a sentence and put the word tiles in order.",
  },
  {
    kind: "minimal-pairs",
    track: "listening",
    label: "Sound detective",
    description: "Hear a word and pick it from words that sound alike.",
  },
  {
    kind: "true-false",
    track: "listening",
    label: "Quick fire",
    description:
      "Hear a statement about a picture and answer true or false fast.",
  },
  {
    kind: "word-meaning",
    track: "vocabulary",
    label: "Word meanings",
    description: "Match a word to its meaning.",
  },
  {
    kind: "word-context",
    track: "vocabulary",
    label: "Word in context",
    description: "Choose the word that fits the sentence.",
  },
  {
    kind: "reading-question",
    track: "reading",
    label: "Reading questions",
    description: "Read and listen, then fill in the highlighted keywords.",
  },
  {
    kind: "reading-order",
    track: "reading",
    label: "Paragraph order",
    description: "Arrange a short text in a logical order.",
  },
  {
    kind: "word-scramble",
    track: "vocabulary",
    label: "Word scramble",
    description: "Solve the clue by arranging letter tiles into a word.",
  },
  {
    kind: "mini-crossword",
    track: "vocabulary",
    label: "Mini crossword",
    description: "Solve connected word clues and fill the crossing letters.",
  },
  {
    kind: "evidence-hunt",
    track: "reading",
    label: "Evidence hunt",
    description:
      "Read a short passage and highlight the sentence that supports the answer.",
  },
  {
    kind: "sentence-puzzle",
    track: "reading",
    label: "Sentence puzzle",
    description:
      "Read a short passage, then build its summary from word tiles.",
  },
] as const;
export const gameLevelStartSchema = z
  .object({
    eventId: z.uuid(),
    kind: z.enum([
      "dictation",
      "gap-fill",
      "scene-words",
      "picture",
      "comprehension",
      "response",
      "riddle",
      "puzzle",
      "pk",
      "spelling",
      "word-order",
      "minimal-pairs",
      "true-false",
      "word-meaning",
      "word-context",
      "reading-question",
      "reading-order",
      "word-scramble",
      "mini-crossword",
      "evidence-hunt",
      "sentence-puzzle",
    ]),
    level: z.number().int().min(1).max(10),
    topic: z.string().trim().min(1).max(80).optional(),
    source: z
      .enum([
        "ContextSpeak",
        "Tatoeba",
        "VOA Learning English",
        "African Storybook",
      ])
      .optional(),
  })
  .strict();
export type GameLevelStart = z.infer<typeof gameLevelStartSchema>;
export type GameLevelRun = {
  id: string;
  kind: GameKind;
  level: number;
  topic: string | null;
  source: string | null;
  rounds: GameRound[];
  receipts: GameReceipt[];
  completed: boolean;
  score: number | null;
  passed: boolean;
  nextLevel: number | null;
};

export const gameDifficultyBands = [
  { first: 1, last: 3, label: "Intermediate" },
  { first: 4, last: 7, label: "Upper-intermediate" },
  { first: 8, last: 10, label: "Advanced" },
] as const;

export function gameDifficulty(level: number) {
  return gameDifficultyBands.find((band) => level <= band.last)!.label;
}
