import { z } from "zod";
import {
  languageCodeSchema,
  type TranslationInput,
  type TranslationLanguage,
} from "@contextspeak/contracts";
import {
  TranslationError,
  type TranslationProvider,
  type TranslationResult,
} from "./translation.provider";
const catalogSchema = z
  .array(
    z.object({
      code: languageCodeSchema,
      name: z.string().min(1).max(100),
      targets: z.array(languageCodeSchema).max(1000),
    }),
  )
  .min(1)
  .max(1000);
const resultSchema = z.object({
  translatedText: z.string().trim().min(1).max(20000),
  detectedLanguage: z.object({ language: languageCodeSchema }).optional(),
});

export class LibreTranslateProvider implements TranslationProvider {
  readonly name = "libretranslate-local";
  private readonly base: URL;
  private cached?: { until: number; languages: TranslationLanguage[] };
  constructor(private options: { url: string; timeoutMs?: number }) {
    this.base = new URL(
      options.url.endsWith("/") ? options.url : options.url + "/",
    );
    if (
      this.base.username ||
      this.base.password ||
      this.base.search ||
      this.base.hash ||
      (this.base.protocol !== "https:" &&
        !(
          this.base.protocol === "http:" &&
          ["127.0.0.1", "localhost", "[::1]"].includes(this.base.hostname)
        ))
    )
      throw new Error("Translator URL must use HTTPS or loopback HTTP");
  }
  private async request(path: string, body?: unknown): Promise<unknown> {
    try {
      const response = await fetch(new URL(path, this.base), {
        method: body ? "POST" : "GET",
        redirect: "error",
        headers: body ? { "Content-Type": "application/json" } : {},
        body: body ? JSON.stringify(body) : undefined,
        signal: AbortSignal.timeout(this.options.timeoutMs ?? 20000),
      });
      if (!response.ok)
        throw new TranslationError(
          response.status === 429
            ? "TRANSLATOR_BUSY"
            : response.status === 400
              ? "UNSUPPORTED_LANGUAGE"
              : "TRANSLATION_FAILED",
        );
      const text = await response.text();
      if (text.length > 128000)
        throw new TranslationError("INVALID_TRANSLATION_RESPONSE");
      try {
        return JSON.parse(text);
      } catch {
        throw new TranslationError("INVALID_TRANSLATION_RESPONSE");
      }
    } catch (error) {
      if (error instanceof TranslationError) throw error;
      throw new TranslationError(
        error instanceof Error &&
          ["AbortError", "TimeoutError"].includes(error.name)
          ? "TRANSLATION_TIMEOUT"
          : "TRANSLATOR_OFFLINE",
      );
    }
  }
  async languages() {
    if (this.cached && this.cached.until > Date.now())
      return this.cached.languages;
    const parsed = catalogSchema.safeParse(await this.request("languages"));
    if (!parsed.success)
      throw new TranslationError("INVALID_TRANSLATION_RESPONSE");
    this.cached = { languages: parsed.data, until: Date.now() + 60000 };
    return parsed.data;
  }
  async translate(input: TranslationInput): Promise<TranslationResult> {
    const languages = await this.languages();
    const source = languages.find(
      (language) => language.code === input.sourceLanguage,
    );
    if (
      !languages.some((language) => language.code === input.targetLanguage) ||
      (input.sourceLanguage !== "auto" &&
        (!source ||
          (input.sourceLanguage !== input.targetLanguage &&
            !source.targets.includes(input.targetLanguage))))
    )
      throw new TranslationError("UNSUPPORTED_LANGUAGE");
    if (input.sourceLanguage === input.targetLanguage)
      return {
        meaning: input.text,
        provider: this.name,
        detectedSourceLanguage: input.sourceLanguage,
      };
    // Never send surrounding private context or page URLs to the translation service.
    const parsed = resultSchema.safeParse(
      await this.request("translate", {
        q: input.text,
        source: input.sourceLanguage,
        target: input.targetLanguage,
        format: "text",
      }),
    );
    if (!parsed.success)
      throw new TranslationError("INVALID_TRANSLATION_RESPONSE");
    return {
      meaning: parsed.data.translatedText,
      provider: this.name,
      detectedSourceLanguage:
        parsed.data.detectedLanguage?.language ??
        (input.sourceLanguage === "auto" ? undefined : input.sourceLanguage),
    };
  }
}
