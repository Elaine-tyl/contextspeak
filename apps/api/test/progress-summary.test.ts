import { expect, it } from "vitest";
import { summarize } from "../src/progress/summary";
const scores = {
  clarity: 90,
  fluency: 85,
  grammar: 50,
  vocabulary: 85,
  taskResponse: 90,
};
it("does not invent weaknesses from one task, repeated or assisted answers", () => {
  const sample = {
    exercise: "same",
    score: 80,
    independent: true,
    scores,
    at: 1,
  };
  expect(summarize([sample, { ...sample, at: 2 }], 0).focus).toBeNull();
  expect(
    summarize([sample, { ...sample, exercise: "other", independent: false }], 0)
      .focus,
  ).toBeNull();
  expect(
    summarize([sample, { ...sample, exercise: "other" }], 0).focus,
  ).toMatchObject({ dimension: "grammar", evidenceCount: 2 });
});
it("keeps no score distinct from zero and filters invalid marks", () => {
  expect(summarize([], 2)).toMatchObject({
    average: null,
    scored: 0,
    unscorable: 2,
  });
  expect(
    summarize(
      [
        { exercise: "x", score: 0, independent: true, at: 1 },
        { exercise: "bad", score: 120, independent: true, at: 2 },
      ],
      0,
    ),
  ).toMatchObject({ scored: 1, average: 0 });
});
