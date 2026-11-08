import { z } from "zod";
import { aiLanguageCodes } from "./learning";
export const speechInputSchema = z
  .object({
    text: z
      .string()
      .trim()
      .min(1)
      .max(1200)
      .refine((text) => [...text].length <= 600),
    language: z.enum(aiLanguageCodes),
  })
  .strict();
export const speechAudioSchema = z.object({
  audio: z.string().min(60).max(4200000),
  mimeType: z.literal("audio/wav"),
});
export type SpeechInput = z.infer<typeof speechInputSchema>;
export type SpeechAudio = z.infer<typeof speechAudioSchema>;
