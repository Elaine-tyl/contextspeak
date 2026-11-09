import { z } from "zod";
import { scenarioSchema } from "./practice";
export const speakingScoresSchema = z
  .object({
    clarity: z.number().int().min(0).max(100),
    fluency: z.number().int().min(0).max(100),
    grammar: z.number().int().min(0).max(100),
    vocabulary: z.number().int().min(0).max(100),
    taskResponse: z.number().int().min(0).max(100),
  })
  .strict();
export const speakingModelSchema = z
  .object({
    speechDetected: z.boolean(),
    transcript: z.string().trim().max(2000),
    scores: speakingScoresSchema.nullable(),
    strengths: z.array(z.string().trim().min(1).max(400)).max(3),
    improvements: z.array(z.string().trim().min(1).max(400)).min(1).max(3),
    corrections: z
      .array(
        z
          .object({
            original: z.string().trim().min(1).max(300),
            corrected: z.string().trim().min(1).max(300),
            reason: z.string().trim().min(1).max(400),
          })
          .strict(),
      )
      .max(5)
      .optional(),
    nextTurn: z.string().trim().min(1).max(600).nullable().optional(),
    suggestedResponse: z.string().trim().max(1200),
  })
  .strict();
export type SpeakingAssessment = z.infer<typeof speakingModelSchema> & {
  overall: number | null;
  rubricVersion: 1;
  model: string;
};
export const speakingUploadSchema = z
  .object({
    eventId: z.uuid(),
    lookupId: z.uuid(),
    scenario: scenarioSchema.optional(),
    conversation: z.literal(true).optional(),
    previousTurnId: z.uuid().optional(),
    consent: z.literal(true),
    replayed: z.literal(true),
    revealed: z.boolean(),
    durationMs: z.number().int().min(1).max(60000),
    mimeType: z.enum(["audio/webm", "audio/mp4", "audio/wav", "audio/ogg"]),
    audio: z
      .string()
      .min(4)
      .max(2796204)
      .regex(
        /^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/,
      ),
  })
  .strict()
  .refine(
    (value) =>
      (!value.conversation || !!value.scenario) &&
      (!value.previousTurnId || !!value.conversation),
    {
      message: "A conversation needs a scenario and an optional previous turn.",
    },
  );
export type SpeakingUpload = z.infer<typeof speakingUploadSchema>;
