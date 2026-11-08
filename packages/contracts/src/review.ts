import { z } from "zod";
export const reviewRatingSchema = z.enum(["again", "hard", "good", "easy"]);
export const reviewSubmissionSchema = z
  .object({
    eventId: z.uuid(),
    version: z.number().int().nonnegative(),
    rating: reviewRatingSchema,
  })
  .strict();
export type ReviewSubmission = z.infer<typeof reviewSubmissionSchema>;
export interface ReviewPrompt {
  id: string;
  version: number;
  prompt: string;
  answer: string;
  context: string | null;
  sourceLanguage: string;
  targetLanguage: string;
  dueAt: string;
}
export interface ReviewQueue {
  items: ReviewPrompt[];
  dueCount: number;
  totalCount: number;
  nextDueAt: string | null;
}
export interface ReviewReceipt {
  id: string;
  version: number;
  dueAt: string;
}
