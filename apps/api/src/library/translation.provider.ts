import type { TranslationInput } from "@contextspeak/contracts";
export interface TranslationProvider {
  translate(
    input: TranslationInput,
  ): Promise<{ meaning: string; provider: string }>;
}
export class FixtureTranslationError extends Error {
  constructor(readonly code: string) {
    super(code);
  }
}
// Original development examples; deliberately not a general-purpose translator.
const entries: Record<string, string> = {
  available: "Free to take part or ready to be used.",
  reschedule: "Arrange for something to happen at a different time.",
  confident: "Sure of your ability to do something.",
  "follow up": "Contact someone again to continue an earlier discussion.",
};
export class FixtureTranslationProvider implements TranslationProvider {
  async translate(input: TranslationInput) {
    if (input.sourceLanguage !== "en" || input.targetLanguage !== "en")
      throw new FixtureTranslationError("UNSUPPORTED_PAIR");
    const meaning = entries[input.text.normalize("NFKC").trim().toLowerCase()];
    if (!meaning) throw new FixtureTranslationError("FIXTURE_NOT_FOUND");
    return { meaning, provider: "original-fixtures-v1" };
  }
}
