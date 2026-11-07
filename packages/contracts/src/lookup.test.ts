import { expect, it } from "vitest";
import { translationSchema } from "./lookup";
it("accepts_sentences_unicode_and_auto_source_but_rejects_auto_target_and_oversized_text", () => {
  const value = {
    text: "你".repeat(2000),
    sourceLanguage: "auto",
    targetLanguage: "en",
  };
  expect(translationSchema.safeParse(value).success).toBe(true);
  expect(
    translationSchema.safeParse({ ...value, text: "你".repeat(2001) }).success,
  ).toBe(false);
  expect(
    translationSchema.safeParse({ ...value, targetLanguage: "auto" }).success,
  ).toBe(false);
  expect(
    translationSchema.safeParse({ ...value, sourceLanguage: "../secret" })
      .success,
  ).toBe(false);
});
