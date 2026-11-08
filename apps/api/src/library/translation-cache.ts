import { createHash } from "node:crypto";
import type { TranslationInput } from "@contextspeak/contracts";
import type { TranslationResult } from "./translation.provider";

// Process-local only: restarting after a model/prompt change invalidates all entries.
export class TranslationCache {
  private entries = new Map<
    string,
    { expiresAt: number; result: TranslationResult }
  >();
  private pending = new Map<string, Promise<TranslationResult>>();
  private ttlMs: number;
  private maxEntries: number;
  private now: () => number;
  constructor(
    options: { ttlMs?: number; maxEntries?: number; now?: () => number } = {},
  ) {
    this.ttlMs = options.ttlMs ?? 24 * 60 * 60 * 1000;
    this.maxEntries = options.maxEntries ?? 500;
    this.now = options.now ?? Date.now;
  }
  async translate(
    scope: string,
    input: TranslationInput,
    load: () => Promise<TranslationResult>,
  ): Promise<TranslationResult> {
    // Preserve case and context: the same spelling can have different meanings.
    // Gemini never receives the page URL, so it is intentionally excluded.
    const key = createHash("sha256")
      .update(
        JSON.stringify([
          scope,
          input.text,
          input.context ?? "",
          input.sourceLanguage,
          input.targetLanguage,
        ]),
      )
      .digest("hex");
    const cached = this.entries.get(key);
    if (cached) {
      this.entries.delete(key);
      if (cached.expiresAt > this.now()) {
        this.entries.set(key, cached); // Refresh LRU order, not the expiry.
        return structuredClone(cached.result);
      }
    }
    let work = this.pending.get(key);
    if (!work) {
      work = Promise.resolve()
        .then(load)
        .then((result) => {
          const stored = structuredClone(result);
          this.entries.set(key, {
            expiresAt: this.now() + this.ttlMs,
            result: stored,
          });
          while (this.entries.size > this.maxEntries)
            this.entries.delete(this.entries.keys().next().value!);
          return stored;
        })
        .finally(() => this.pending.delete(key));
      this.pending.set(key, work);
    }
    // Neither callers nor persistence code can mutate the cached result.
    return structuredClone(await work);
  }
}
