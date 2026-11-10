import { z } from "zod";
export const reviewEnrollmentSchema = z
  .object({ lookupId: z.uuid().optional() })
  .strict()
  .default({});
export const reviewRatingSchema = z.enum(["again", "hard", "good", "easy"]);
export const reviewSubmissionSchema = z
  .object({
    eventId: z.uuid(),
    version: z.number().int().nonnegative(),
    rating: reviewRatingSchema,
  })
  .strict();
export type ReviewSubmission = z.infer<typeof reviewSubmissionSchema>;
export const reviewPromptSchema = z.object({
  id: z.string().min(1),
  version: z.number().int().nonnegative(),
  prompt: z.string().min(1),
  answer: z.string().min(1),
  context: z.string().nullable(),
  sourceLanguage: z.string().min(1),
  targetLanguage: z.string().min(1),
  dueAt: z.iso.datetime(),
});
export const reviewQueueSchema = z.object({
  items: z.array(reviewPromptSchema),
  dueCount: z.number().int().nonnegative(),
  totalCount: z.number().int().nonnegative(),
  nextDueAt: z.iso.datetime().nullable(),
});
export type ReviewPrompt = z.infer<typeof reviewPromptSchema>;
export type ReviewQueue = z.infer<typeof reviewQueueSchema>;
export interface ReviewReceipt {
  id: string;
  version: number;
  dueAt: string;
}
