import { z } from "zod";
import {
  languageCodeSchema,
  sourceLanguageSchema,
} from "@contextspeak/contracts";
export const libraryFiltersSchema = z
  .object({
    q: z.string().trim().max(200).optional(),
    sourceLanguage: sourceLanguageSchema.optional(),
    targetLanguage: languageCodeSchema.optional(),
  })
  .strict();
export type LibraryFilters = z.infer<typeof libraryFiltersSchema>;
