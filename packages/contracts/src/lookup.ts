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
const fields = {
  text: boundedText(200),
  context: boundedText(1000).optional(),
  sourceUrl: sourceUrl.optional(),
  sourceLanguage: z.enum(["en"]),
  targetLanguage: z.enum(["en"]),
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
  errorCode: string | null;
  createdAt: string;
}
export interface Candidate {
  id: string;
  text: string;
  status: string;
  senseStatus: string;
  encounterCount: number;
}
export interface Page<T> {
  items: T[];
  nextCursor: string | null;
}
