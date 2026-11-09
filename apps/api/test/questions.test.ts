import { expect, it } from "vitest";
import { vocabularyQuestion } from "../src/practice/questions";
it("uses saved context and makes the expected response explicit at each difficulty", () => {
  const first = vocabularyQuestion(
    "available",
    "Are you available for a meeting tomorrow?",
    1,
  );
  expect(first.prompt).toContain("Are you available for a meeting tomorrow?");
  expect(first.criteria).toHaveLength(2);
  const harder = vocabularyQuestion("available", undefined, 3);
  expect(harder.prompt).toContain("friend wants to meet");
  expect(harder.criteria.join(" ")).toContain("Compare two options");
  expect(
    [...vocabularyQuestion("x".repeat(200), "a ".repeat(300), 3).prompt].length,
  ).toBeLessThanOrEqual(600);
});
it("does not present a one-word example as a conversation context", () => {
  expect(vocabularyQuestion("academic", "academic", 1).prompt).toContain(
    "classmate",
  );
  expect(vocabularyQuestion("help", undefined, 1).prompt).toContain("politely");
  expect(vocabularyQuestion("unknown", undefined, 1).prompt).toContain(
    "common meaning",
  );
});
