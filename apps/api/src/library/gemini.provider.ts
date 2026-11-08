import { z } from "zod";
import {
  aiLanguages,
  translationSchema,
  type TranslationInput,
} from "@contextspeak/contracts";
import { prepareLearning } from "./ai-learning";
import {
  TranslationError,
  type TranslationProvider,
} from "./translation.provider";
export const GEMINI_MODEL = "gemini-3.1-flash-lite";
// Keep unsupported JSON-schema keywords out of the REST request; enforce the full Zod schema locally.
export function geminiSchema(value: any): any {
  if (Array.isArray(value)) return value.map(geminiSchema);
  if (!value || typeof value !== "object") return value;
  const allowed = new Set([
    "type",
    "title",
    "description",
    "enum",
    "items",
    "minItems",
    "maxItems",
    "anyOf",
    "oneOf",
    "properties",
    "additionalProperties",
    "required",
    "minimum",
    "maximum",
    "$defs",
    "$ref",
  ]);
  const result: Record<string, unknown> = {};
  for (const [key, item] of Object.entries(value)) {
    if (key === "const") {
      result.enum = [item];
      continue;
    }
    if (!allowed.has(key)) continue;
    result[key] =
      key === "properties" || key === "$defs"
        ? Object.fromEntries(
            Object.entries(item as Record<string, unknown>).map(
              ([name, schema]) => [name, geminiSchema(schema)],
            ),
          )
        : geminiSchema(item);
  }
  return result;
}
const envelope = z.object({
  modelVersion: z.string().max(200).optional(),
  candidates: z
    .array(
      z.object({
        finishReason: z.string(),
        content: z
          .object({
            parts: z.array(
              z.object({
                text: z.string().max(64000).optional(),
                thought: z.boolean().optional(),
              }),
            ),
          })
          .optional(),
      }),
    )
    .min(1)
    .max(1),
});
export class GeminiTranslationProvider implements TranslationProvider {
  readonly name = "gemini";
  readonly maxSelectionCharacters = 600;
  private key: string;
  private fetcher: typeof fetch;
  private timeoutMs: number;
  private requests: number[] = [];
  constructor(
    options: {
      apiKey?: string;
      fetcher?: typeof fetch;
      timeoutMs?: number;
    } = {},
  ) {
    this.key = options.apiKey?.trim() ?? "";
    this.fetcher = options.fetcher ?? fetch;
    this.timeoutMs = options.timeoutMs ?? 60000;
  }
  async languages() {
    return aiLanguages;
  }
  async translate(input: TranslationInput) {
    if (!this.key) throw new TranslationError("GEMINI_NOT_CONFIGURED");
    if (
      !aiLanguages.some((x) => x.code === input.targetLanguage) ||
      (input.sourceLanguage !== "auto" &&
        !aiLanguages.some((x) => x.code === input.sourceLanguage))
    )
      throw new TranslationError("UNSUPPORTED_LANGUAGE");
    const value = translationSchema.parse(input);
    if (Array.from(value.text).length > 600)
      throw new TranslationError("AI_SELECTION_TOO_LONG");
    const now = Date.now();
    this.requests = this.requests.filter((time) => now - time < 60000);
    if (this.requests.length >= 5)
      throw new TranslationError("GEMINI_QUOTA_EXCEEDED");
    this.requests.push(now);
    const learning = prepareLearning(value, true);
    const signal = AbortSignal.timeout(this.timeoutMs);
    try {
      const response = await this.fetcher(
        `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_MODEL}:generateContent`,
        {
          method: "POST",
          redirect: "error",
          signal,
          headers: {
            "Content-Type": "application/json",
            "x-goog-api-key": this.key,
          },
          body: JSON.stringify({
            systemInstruction: { parts: [{ text: learning.prompt }] },
            contents: [
              {
                role: "user",
                parts: [{ text: JSON.stringify(learning.data) }],
              },
            ],
            generationConfig: {
              temperature: 0.2,
              maxOutputTokens: 4096,
              responseMimeType: "application/json",
              responseJsonSchema: geminiSchema(learning.schema),
            },
          }),
        },
      );
      if (!response.ok)
        throw new TranslationError(
          response.status === 429
            ? "GEMINI_QUOTA_EXCEEDED"
            : response.status === 401 || response.status === 403
              ? "GEMINI_ACCESS_DENIED"
              : "GEMINI_UNAVAILABLE",
        );
      const reader = response.body?.getReader();
      if (!reader) throw new TranslationError("MODEL_INVALID_RESPONSE");
      const chunks: Uint8Array[] = [];
      let size = 0;
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        size += value.byteLength;
        if (size > 128000) {
          await reader.cancel();
          throw new TranslationError("MODEL_INVALID_RESPONSE");
        }
        chunks.push(value);
      }
      let parsed: z.infer<typeof envelope>;
      try {
        parsed = envelope.parse(
          JSON.parse(Buffer.concat(chunks).toString("utf8")),
        );
      } catch {
        throw new TranslationError("MODEL_INVALID_RESPONSE");
      }
      const candidate = parsed.candidates[0];
      if (candidate.finishReason !== "STOP" || !candidate.content)
        throw new TranslationError("MODEL_INVALID_RESPONSE");
      const text = candidate.content.parts
        .filter((part) => !part.thought)
        .map((part) => part.text ?? "")
        .join("");
      return learning.parse(text, {
        provider: this.name,
        model: parsed.modelVersion ?? GEMINI_MODEL,
        modelDigest: null,
      });
    } catch (error) {
      if (error instanceof TranslationError) throw error;
      throw new TranslationError(
        signal.aborted ? "TRANSLATION_TIMEOUT" : "GEMINI_UNAVAILABLE",
      );
    }
  }
}
