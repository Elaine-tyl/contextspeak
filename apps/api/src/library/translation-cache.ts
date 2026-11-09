import { createHash } from "node:crypto";
import type { TranslationInput } from "@contextspeak/contracts";
import type { TranslationResult } from "./translation.provider";

// Process-local only: restarting after a model/prompt change invalidates all entries.
export class TranslationCache {
  private entries = new Map<
    string,
    { scope: string; expiresAt: number; result: TranslationResult }
  >();
  private pending = new Map<
    string,
    { scope: string; work: Promise<TranslationResult> }
  >();
  private retain: (scope: string, keep: () => void) => Promise<void>;
  private ttlMs: number;
  private maxEntries: number;
  private now: () => number;
  constructor(
    options: {
      ttlMs?: number;
      maxEntries?: number;
      now?: () => number;
      retain?: (scope: string, keep: () => void) => Promise<void>;
    } = {},
  ) {
    this.ttlMs = options.ttlMs ?? 24 * 60 * 60 * 1000;
    this.maxEntries = options.maxEntries ?? 500;
    this.now = options.now ?? Date.now;
    this.retain =
      options.retain ??
      (async (_scope, keep) => {
        keep();
      });
  }
  forget(scope: string) {
    for (const [key, entry] of this.entries)
      if (entry.scope === scope) this.entries.delete(key);
    for (const [key, entry] of this.pending)
      if (entry.scope === scope) this.pending.delete(key);
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
    let pending = this.pending.get(key);
    if (!pending) {
      const work = Promise.resolve()
        .then(load)
        .then(async (result) => {
          const stored = structuredClone(result);
          await this.retain(scope, () => {
            if (this.pending.get(key)?.work !== work) return;
            this.entries.set(key, {
              scope,
              expiresAt: this.now() + this.ttlMs,
              result: stored,
            });
            while (this.entries.size > this.maxEntries)
              this.entries.delete(this.entries.keys().next().value!);
          });
          return stored;
        })
        .finally(() => {
          if (this.pending.get(key)?.work === work) this.pending.delete(key);
        });
      pending = { scope, work };
      this.pending.set(key, pending);
    }
    return structuredClone(await pending.work);
  }
}
