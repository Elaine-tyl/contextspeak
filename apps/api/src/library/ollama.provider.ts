import { prepareLearning } from "./ai-learning";
import { z } from "zod";
import {
  aiLanguages,
  translationSchema,
  type TranslationInput,
} from "@contextspeak/contracts";
import {
  TranslationError,
  type TranslationProvider,
  type TranslationResult,
} from "./translation.provider";
export const LOCAL_AI_MODEL = "qwen3:4b";
export const LOCAL_AI_DIGEST =
  "359d7dd4bcdab3d86b87d73ac27966f4dbb9f5efdfcc75d34a8764a09474fae7";
const envelope = z.object({
  message: z.object({ content: z.string().max(64000) }),
  done: z.boolean().optional(),
});
// All fields are bounded and untrusted model output is rendered only as text.
export class OllamaTranslationProvider implements TranslationProvider {
  readonly name = "ollama-local";
  readonly maxSelectionCharacters = 600;
  private fetcher: typeof fetch;
  private url: string;
  private timeoutMs: number;
  private verifiedUntil = 0;
  private pending = 0;
  private tail: Promise<unknown> = Promise.resolve();
  constructor(
    options: { url?: string; fetcher?: typeof fetch; timeoutMs?: number } = {},
  ) {
    this.fetcher = options.fetcher ?? fetch;
    this.url = options.url ?? "http://127.0.0.1:11434";
    this.timeoutMs = options.timeoutMs ?? 90000;
    const url = new URL(this.url);
    if (
      !["127.0.0.1", "localhost", "[::1]"].includes(url.hostname) ||
      url.protocol !== "http:" ||
      url.username ||
      url.password
    )
      throw new Error("Local Ollama loopback URL required");
  }
  async languages() {
    return aiLanguages;
  }
  async translate(input: TranslationInput): Promise<TranslationResult> {
    if (
      !aiLanguages.some((x) => x.code === input.targetLanguage) ||
      (input.sourceLanguage !== "auto" &&
        !aiLanguages.some((x) => x.code === input.sourceLanguage))
    )
      throw new TranslationError("UNSUPPORTED_LANGUAGE");
    const value = translationSchema.parse(input);
    if (Array.from(value.text).length > this.maxSelectionCharacters)
      throw new TranslationError("AI_SELECTION_TOO_LONG");
    if (this.pending >= 4) throw new TranslationError("MODEL_BUSY");
    this.pending++;
    // Serialize inference on a 16GB development machine; queue time counts toward the deadline.
    const signal = AbortSignal.timeout(this.timeoutMs);
    const run = this.tail
      .catch(() => {})
      .then(() => this.generate(value, signal));
    this.tail = run;
    try {
      return await run;
    } catch (error) {
      if (error instanceof TranslationError) throw error;
      throw new TranslationError(
        signal.aborted ? "TRANSLATION_TIMEOUT" : "MODEL_UNAVAILABLE",
      );
    } finally {
      this.pending--;
    }
  }
  private async json(path: string, signal: AbortSignal, body?: unknown) {
    signal.throwIfAborted();
    const response = await this.fetcher(this.url + path, {
      method: body ? "POST" : "GET",
      headers: { "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined,
      signal,
      redirect: "error",
    });
    if (!response.ok) throw new TranslationError("MODEL_UNAVAILABLE");
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
    try {
      return JSON.parse(Buffer.concat(chunks).toString("utf8"));
    } catch {
      throw new TranslationError("MODEL_INVALID_RESPONSE");
    }
  }
  private async generate(
    input: TranslationInput,
    signal: AbortSignal,
  ): Promise<TranslationResult> {
    if (Date.now() > this.verifiedUntil) {
      const tags = await this.json("/api/tags", signal);
      if (
        !Array.isArray(tags.models) ||
        !tags.models.some(
          (m: any) => m.name === LOCAL_AI_MODEL && m.digest === LOCAL_AI_DIGEST,
        )
      )
        throw new TranslationError("MODEL_MISMATCH");
      this.verifiedUntil = Date.now() + 60000;
    }
    const learning = prepareLearning(input);
    const raw = await this.json("/api/chat", signal, {
      model: LOCAL_AI_MODEL,
      stream: false,
      think: false,
      format: learning.schema,
      options: { temperature: 0, num_predict: 4096, num_ctx: 8192 },
      messages: [
        { role: "system", content: learning.prompt },
        {
          role: "user",
          content: JSON.stringify(learning.data),
        },
      ],
    });
    try {
      const outer = envelope.parse(raw);
      if (outer.done === false) throw new Error("Incomplete");
      return learning.parse(outer.message.content, {
        provider: this.name,
        model: LOCAL_AI_MODEL,
        modelDigest: LOCAL_AI_DIGEST,
      });
    } catch {
      throw new TranslationError("MODEL_INVALID_RESPONSE");
    }
  }
}
