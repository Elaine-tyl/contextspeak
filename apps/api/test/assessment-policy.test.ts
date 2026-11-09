import { expect, it } from "vitest";
import { assessmentScore } from "../src/practice/assessment-policy";
const scores = {
  clarity: 90,
  fluency: 90,
  grammar: 90,
  vocabulary: 90,
  taskResponse: 20,
};
const evidence = Object.keys(scores).map((dimension) => ({
  dimension,
  quote: "I like bananas",
  explanation: "Observed in the response.",
}));
const model = {
  speechDetected: true,
  transcript: "I like bananas.",
  audioQuality: "clear",
  scores,
  evidence,
} as any;
it("caps fluent off-topic replies rather than averaging them into a pass", () => {
  expect(assessmentScore(model)).toBe(40);
});
it("withholds marks for limited audio and rejects fabricated or duplicate evidence", () => {
  expect(assessmentScore({ ...model, audioQuality: "limited" })).toBeNull();
  expect(assessmentScore({ ...model, speechDetected: false })).toBeNull();
  expect(() =>
    assessmentScore({
      ...model,
      evidence: evidence.map((item) => ({ ...item, quote: "not spoken" })),
    }),
  ).toThrow();
  expect(() =>
    assessmentScore({
      ...model,
      evidence: [...evidence.slice(0, 4), evidence[0]],
    }),
  ).toThrow();
});

it("requires transcript grounding even for quoted audio observations", () => {
  expect(() =>
    assessmentScore({
      ...model,
      evidence: evidence.map((item, index) =>
        index === 0 ? { ...item, quote: "invented words" } : item,
      ),
    }),
  ).toThrow();
});
