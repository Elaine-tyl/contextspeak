import { z } from "zod";
import { speakingUploadSchema, type SpeakingAssessment } from "./speaking";
import type { PracticeRecommendation, DictationFeedback } from "./practice";
export type GameTrack = "listening" | "speaking";
export type GameKind =
  | "dictation"
  | "gap-fill"
  | "picture"
  | "comprehension"
  | "response"
  | "riddle"
  | "puzzle"
  | "pk";
export type PictureId = "umbrella" | "book" | "bridge" | "bicycle";
export type GameRound = {
  id: string;
  track: GameTrack;
  kind: GameKind;
  level: number;
  title: string;
  genre?: "Story" | "Drama" | "Literature" | "Novel excerpt";
  instruction: string;
  audioText: string;
  gapText?: string[];
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
  criteria?: string[];
  opponentScore?: number;
};
export type GameProgress = Record<
  GameTrack,
  { level: number; passed: number[] }
>;
export type GameCatalog = {
  rounds: GameRound[];
  progress: GameProgress;
  modeProgress?: Record<GameKind, { level: number }>;
  recommendations?: Record<GameTrack, PracticeRecommendation>;
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
    speaking?: SpeakingAssessment;
    opponentScore?: number;
  };
};

export const gameModes = [
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
] as const;
export const gameLevelStartSchema = z
  .object({
    eventId: z.uuid(),
    kind: z.enum([
      "dictation",
      "gap-fill",
      "picture",
      "comprehension",
      "response",
      "riddle",
      "puzzle",
      "pk",
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
