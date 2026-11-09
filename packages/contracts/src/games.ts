import { z } from "zod";
import { speakingUploadSchema, type SpeakingAssessment } from "./speaking";
import type { PracticeRecommendation, DictationFeedback } from "./practice";
export type GameTrack = "listening" | "speaking";
export type GameKind =
  "dictation" | "picture" | "comprehension" | "riddle" | "puzzle" | "pk";
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
  recommendations?: Record<GameTrack, PracticeRecommendation>;
};
export const gameSubmissionSchema = z
  .object({
    eventId: z.uuid(),
    roundId: z.string().min(1).max(80),
    response: z.string().trim().max(2000).optional(),
    choice: z.string().min(1).max(80).optional(),
    completedPlays: z.number().int().min(0).max(20),
    audio: speakingUploadSchema.shape.audio.optional(),
    mimeType: speakingUploadSchema.shape.mimeType.optional(),
    durationMs: z.number().int().min(1).max(60000).optional(),
  })
  .strict();
export type GameSubmission = z.infer<typeof gameSubmissionSchema>;
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
    speaking?: SpeakingAssessment;
    opponentScore?: number;
  };
};
