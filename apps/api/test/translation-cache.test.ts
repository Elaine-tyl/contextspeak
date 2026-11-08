import { expect, it } from "vitest";
import { TranslationCache } from "../src/library/translation-cache";
const input = {
  text: "bank",
  context: "The river bank.",
  sourceLanguage: "en",
  targetLanguage: "zh-Hans",
};
const result = { meaning: "河岸", provider: "gemini" };
it("reuses_success_and_coalesces_concurrent_requests_without_sharing_mutable_results", async () => {
  const cache = new TranslationCache();
  let calls = 0;
  const load = async () => {
    calls++;
    return { ...result };
  };
  const [a, b] = await Promise.all([
    cache.translate("a", input, load),
    cache.translate("a", input, load),
  ]);
  a.meaning = "changed";
  expect(b.meaning).toBe("河岸");
  expect(
    await cache.translate(
      "a",
      { ...input, sourceUrl: "https://example.com/" },
      load,
    ),
  ).toEqual(result);
  expect(calls).toBe(1);
});
it("isolates_users_context_languages_and_exact_case", async () => {
  const cache = new TranslationCache();
  let calls = 0;
  const load = async () => {
    calls++;
    return result;
  };
  await cache.translate("a", input, load);
  await cache.translate("b", input, load);
  for (const change of [
    { context: "A savings bank." },
    { targetLanguage: "fr" },
    { sourceLanguage: "auto" },
    { text: "Bank" },
  ])
    await cache.translate("a", { ...input, ...change }, load);
  expect(calls).toBe(6);
});
it("expires_results_and_evicts_the_least_recently_used_entry", async () => {
  let now = 0;
  let calls = 0;
  const cache = new TranslationCache({
    ttlMs: 100,
    maxEntries: 2,
    now: () => now,
  });
  const load = async () => {
    calls++;
    return result;
  };
  await cache.translate("a", input, load);
  await cache.translate("b", input, load);
  await cache.translate("a", input, load);
  await cache.translate("c", input, load);
  await cache.translate("b", input, load);
  expect(calls).toBe(4);
  now = 100;
  await cache.translate("b", input, load);
  expect(calls).toBe(5);
});
it("does_not_cache_errors_or_keep_a_rejected_inflight_request", async () => {
  const cache = new TranslationCache();
  let calls = 0;
  const load = async () => {
    if (++calls === 1) throw new Error("quota");
    return result;
  };
  await expect(cache.translate("a", input, load)).rejects.toThrow("quota");
  expect(await cache.translate("a", input, load)).toEqual(result);
  expect(calls).toBe(2);
});
