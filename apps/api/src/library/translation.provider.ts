import type {
  TranslationInput,
  TranslationLanguage,
} from "@contextspeak/contracts";
export interface TranslationResult {
  meaning: string;
  provider: string;
  detectedSourceLanguage?: string;
}
export interface TranslationProvider {
  readonly name?: string;
  languages?(): Promise<TranslationLanguage[]>;
  translate(input: TranslationInput): Promise<TranslationResult>;
}
export class TranslationError extends Error {
  constructor(readonly code: string) {
    super(code);
  }
}
export { TranslationError as FixtureTranslationError };
// Original development examples; deliberately not a general-purpose translator.
const entries: Record<string, string> = {
  available: "Free to take part or ready to be used.",
  reschedule: "Arrange for something to happen at a different time.",
  confident: "Sure of your ability to do something.",
  "follow up": "Contact someone again to continue an earlier discussion.",
};
export class FixtureTranslationProvider implements TranslationProvider {
  readonly name = "fixture";
  async languages() {
    return [{ code: "en", name: "English", targets: ["en"] }];
  }
  async translate(input: TranslationInput) {
    if (
      !["en", "auto"].includes(input.sourceLanguage) ||
      input.targetLanguage !== "en"
    )
      throw new TranslationError("UNSUPPORTED_PAIR");
    const meaning = entries[input.text.normalize("NFKC").trim().toLowerCase()];
    if (!meaning) throw new TranslationError("FIXTURE_NOT_FOUND");
    return { meaning, provider: "original-fixtures-v1" };
  }
}
