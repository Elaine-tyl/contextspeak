import { z } from "zod";
export const aiLanguageCodes = [
  "en",
  "zh-Hans",
  "ja",
  "ko",
  "es",
  "fr",
  "vi",
] as const;
export const aiLanguages = aiLanguageCodes.map((code, index) => ({
  code,
  name: [
    "English",
    "Chinese",
    "Japanese",
    "Korean",
    "Spanish",
    "French",
    "Vietnamese",
  ][index],
  targets: [...aiLanguageCodes],
}));
const pair = z
  .object({
    source: z.string().min(1).max(500),
    translation: z.string().min(1).max(500),
  })
  .strict();
export const aiResponseSchema = z
  .object({
    selection: z.string().min(1).max(4000),
    sourceLanguage: z.enum(aiLanguageCodes),
    targetLanguage: z.enum(aiLanguageCodes),
    kind: z.enum(["word", "phrase", "sentence"]),
    meaning: z.string().min(1).max(4000),
    definition: z.string().min(1).max(1000),
    partOfSpeech: z.string().max(80).nullable(),
    notes: z.string().max(1000).nullable(),
    collocations: z.array(pair).max(2),
    examples: z.array(pair).min(1).max(2),
  })
  .strict();
export const learningDetailsSchema = aiResponseSchema
  .omit({
    selection: true,
    sourceLanguage: true,
    targetLanguage: true,
    meaning: true,
  })
  .extend({
    version: z.literal(1),
    model: z.string(),
    modelDigest: z.string().nullable(),
    promptVersion: z.literal(1),
  });
export type LearningDetails = z.infer<typeof learningDetailsSchema>;
