import { expect, it } from "vitest";
import { gradeDictation } from "../src/practice/dictation";
it("ignores_case_punctuation_and_spacing_but_preserves_word_order", () => {
  expect(
    gradeDictation("I'm available, at five!", "IM   available at five.", "en"),
  ).toMatchObject({
    score: 100,
    correctWords: 4,
    totalWords: 4,
    missing: [],
    extra: [],
    replacements: [],
  });
  expect(
    gradeDictation("meet me tomorrow", "tomorrow meet me", "en").score,
  ).toBeLessThan(100);
});
it("explains_missing_extra_and_replaced_words_without_inflating_partial_scores", () => {
  expect(
    gradeDictation("Are you available for coffee", "Are you ready for", "en"),
  ).toMatchObject({
    score: 60,
    correctWords: 3,
    totalWords: 5,
    missing: ["coffee"],
    replacements: [{ expected: "available", actual: "ready" }],
  });
  expect(gradeDictation("hello", "hello hello hello", "en")).toMatchObject({
    score: 0,
    extra: ["hello", "hello"],
  });
  expect(gradeDictation("hello", "...", "en").score).toBe(0);
});
it("segments_non_space_languages_and_preserves_diacritics", () => {
  expect(gradeDictation("你好，世界！", "你好 世界", "zh-Hans").score).toBe(
    100,
  );
  expect(gradeDictation("café", "cafe", "fr").score).toBe(0);
  expect(gradeDictation("Xin chào!", "xin chào", "vi").score).toBe(100);
});
it("reserves_100_for_a_transcript_without_word_errors", () => {
  const expected = Array(201).fill("a").join(" ");
  const response = [...Array(200).fill("a"), "b"].join(" ");
  expect(gradeDictation(expected, response, "en")).toMatchObject({
    score: 99,
    replacements: [{ expected: "a", actual: "b" }],
  });
});
