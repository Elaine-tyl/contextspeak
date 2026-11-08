import type { LearningDetails } from "./learning";
import { z } from "zod";
export type Identity = { issuer: string; subject: string };
const boundedText = (max: number) =>
  z
    .string()
    .transform((s) => s.normalize("NFKC").trim())
    .refine(
      (s) => Array.from(s).length > 0 && Array.from(s).length <= max,
      "Text is empty or too long",
    );
const sourceUrl = z
  .string()
  .url()
  .refine(
    (s) => ["http:", "https:"].includes(new URL(s).protocol),
    "HTTP(S) source required",
  )
  .transform((s) => {
    const u = new URL(s);
    u.username = "";
    u.password = "";
    u.search = "";
    u.hash = "";
    return u.href;
  });
export const MAX_SELECTION_CHARACTERS = 2000;
export const languageCodeSchema = z
  .string()
  .regex(/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})?$/);
export const sourceLanguageSchema = z.union([
  z.literal("auto"),
  languageCodeSchema,
]);
export const translationPreferencesSchema = z
  .object({
    sourceLanguage: sourceLanguageSchema,
    targetLanguage: languageCodeSchema,
    autoTranslate: z.boolean(),
  })
  .strict();
export type TranslationPreferences = z.infer<
  typeof translationPreferencesSchema
>;
export const defaultTranslationPreferences: TranslationPreferences = {
  sourceLanguage: "auto",
  targetLanguage: "zh-Hans",
  autoTranslate: true,
};
export interface TranslationLanguage {
  code: string;
  name: string;
  targets: string[];
}
export interface TranslationOptions {
  provider: string;
  maxSelectionCharacters?: number;
  languages: TranslationLanguage[];
}
const fields = {
  text: boundedText(MAX_SELECTION_CHARACTERS),
  context: boundedText(1000).optional(),
  sourceUrl: sourceUrl.optional(),
  sourceLanguage: sourceLanguageSchema,
  targetLanguage: languageCodeSchema,
};
export const translationSchema = z.object(fields).strict();
export const lookupSchema = z.object({ eventId: z.uuid(), ...fields }).strict();
export const settingsSchema = z
  .object({
    enabled: z.boolean(),
    excludedOrigins: z
      .array(
        z
          .string()
          .url()
          .refine((s) => ["http:", "https:"].includes(new URL(s).protocol)),
      )
      .max(50)
      .transform((ss) => [...new Set(ss.map((s) => new URL(s).origin))]),
  })
  .strict();
export type LookupInput = z.infer<typeof lookupSchema>;
export type TranslationInput = z.infer<typeof translationSchema>;
export type CollectionSettings = z.infer<typeof settingsSchema>;
export interface LookupReceipt {
  lookupId: string;
  candidateId: string;
  translationStatus: string;
}
export interface LookupRecord {
  id: string;
  eventId: string;
  candidateId: string;
  text: string;
  context: string | null;
  sourceUrl: string | null;
  sourceLanguage: string;
  targetLanguage: string;
  translationStatus: "pending" | "succeeded" | "failed";
  meaning: string | null;
  learningDetails?: LearningDetails | null;
  errorCode: string | null;
  createdAt: string;
}
export interface Candidate {
  reviewDueAt?: string | null;
  sourceLanguage: string;
  targetLanguage: string;
  id: string;
  text: string;
  status: string;
  senseStatus: string;
  encounterCount: number;
}
export interface VocabularyDetail {
  item: Candidate;
  lookup: LookupRecord | null;
}
export interface Page<T> {
  items: T[];
  nextCursor: string | null;
}

export interface LookupTranslationResult {
  lookupId: string;
  translationStatus: "pending" | "succeeded" | "failed";
  meaning: string | null;
  learningDetails?: LearningDetails | null;
  provider: string | null;
  detectedSourceLanguage?: string | null;
  errorCode: string | null;
}

export {
  aiLanguages,
  aiLanguageCodes,
  aiResponseSchema,
  learningDetailsSchema,
  type LearningDetails,
} from "./learning";

export {
  reviewSubmissionSchema,
  reviewEnrollmentSchema,
  type ReviewSubmission,
  type ReviewPrompt,
  type ReviewQueue,
  type ReviewReceipt,
} from "./review";
