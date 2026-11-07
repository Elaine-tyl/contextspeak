import { z } from "zod";
import {
  defaultTranslationPreferences,
  languageCodeSchema,
  translationPreferencesSchema,
  type TranslationLanguage,
} from "@contextspeak/contracts";

// Popup files can update before Chrome restarts the background worker.
export const EXTENSION_PROTOCOL = 2;
export const RELOAD_EXTENSION =
  "Reload the ContextSpeak extension in chrome://extensions, then refresh the webpage.";
export const localLanguages: TranslationLanguage[] = [
  { code: "en", name: "English", targets: ["en", "zh-Hans"] },
  { code: "zh-Hans", name: "Chinese", targets: ["en", "zh-Hans"] },
];
const optionsSchema = z.object({
  ok: z.literal(true),
  protocolVersion: z.literal(EXTENSION_PROTOCOL),
  provider: z.string(),
  languages: z
    .array(
      z.object({
        code: languageCodeSchema,
        name: z.string().min(1),
        targets: z.array(languageCodeSchema),
      }),
    )
    .min(1),
  preferences: translationPreferencesSchema,
});
export function readTranslationOptions(value: unknown) {
  if (
    !value ||
    (value as { protocolVersion?: number }).protocolVersion !==
      EXTENSION_PROTOCOL
  )
    throw new Error(RELOAD_EXTENSION);
  if ((value as { code?: string }).code === "ACCOUNT_CHANGED")
    throw new Error("The connected account changed. Please retry.");
  const parsed = optionsSchema.safeParse(value);
  if (!parsed.success)
    throw new Error(
      "Translation languages could not be loaded. Restart pnpm dev and try again.",
    );
  const { languages, preferences } = parsed.data;
  const supported = (code: string) =>
    languages.some((language) => language.code === code);
  // Older saved language codes must not produce a select with no matching option.
  return {
    ...parsed.data,
    preferences: {
      ...preferences,
      sourceLanguage:
        preferences.sourceLanguage === "auto" ||
        supported(preferences.sourceLanguage)
          ? preferences.sourceLanguage
          : "auto",
      targetLanguage: supported(preferences.targetLanguage)
        ? preferences.targetLanguage
        : supported(defaultTranslationPreferences.targetLanguage)
          ? defaultTranslationPreferences.targetLanguage
          : languages[0].code,
    },
  };
}
