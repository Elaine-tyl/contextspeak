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
    audioQuality: z.enum(["clear", "limited", "unusable"]),
    evidence: z
      .array(
        z
          .object({
            dimension: z.enum([
              "clarity",
              "fluency",
              "grammar",
              "vocabulary",
              "taskResponse",
            ]),
            quote: z.string().max(200),
            explanation: z.string().trim().min(1).max(300),
          })
          .strict(),
      )
      .max(5),
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
    pronunciation: z
      .array(
        z
          .object({
            word: z
              .string()
              .trim()
              .min(1)
              .max(80)
              .regex(/^[\p{L}\p{M}'’-]+$/u),
            ipa: z
              .string()
              .trim()
              .max(120)
              .regex(/^\/[^\/\r\n]+\/$/u),
            stress: z.string().trim().min(1).max(200),
            tip: z.string().trim().min(1).max(400),
          })
          .strict(),
      )
      .max(3)
      .optional(),
    nextTurn: z.string().trim().min(1).max(600).nullable().optional(),
    suggestedResponse: z.string().trim().max(1200),
  })
  .strict();
type SpeakingModel = z.infer<typeof speakingModelSchema>;
export type SpeakingAssessment = Omit<
  SpeakingModel,
  "audioQuality" | "evidence"
> &
  Partial<Pick<SpeakingModel, "audioQuality" | "evidence">> & {
    overall: number | null;
    rubricVersion: 1 | 2;
    model: string;
  };
export const speakingUploadSchema = z
  .object({
    eventId: z.uuid(),
    lookupId: z.uuid(),
    scenario: scenarioSchema.optional(),
    difficulty: z.number().int().min(1).max(3).optional(),
    conversation: z.literal(true).optional(),
    previousTurnId: z.uuid().optional(),
    consent: z.literal(true).optional(),
    replayed: z.boolean(),
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
