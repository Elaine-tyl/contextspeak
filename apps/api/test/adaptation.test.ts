import { expect, it } from "vitest";
import {
  recommendPractice,
  practiceExerciseKey,
  type PerformanceSample,
} from "../src/practice/adaptation";
const sample = (
  exercise: string,
  score: number | null,
  level = 1,
  independent = true,
): PerformanceSample => ({ exercise, score, level, independent });
it("needs three different independent results at each prior level to recommend a harder task", () => {
  expect(recommendPractice([]).level).toBe(1);
  expect(recommendPractice([sample("a", 100)]).level).toBe(1);
  expect(recommendPractice([sample("a", 100), sample("b", 90)]).level).toBe(1);
  const passed = [sample("a", 80), sample("b", 90), sample("c", 85)];
  expect(recommendPractice(passed).level).toBe(2);
  expect(
    recommendPractice([sample("a", 100), sample("a", 100), sample("a", 100)])
      .level,
  ).toBe(1);
  expect(
    recommendPractice([
      sample("a", 100, 2),
      sample("b", 100, 2),
      sample("c", 100, 2),
    ]).level,
  ).toBe(1);
  expect(
    recommendPractice([
      ...passed,
      sample("d", 85, 2),
      sample("e", 90, 2),
      sample("f", 80, 2),
    ]).level,
  ).toBe(3);
});
it("does not promote from repeated, assisted or uncertain work and lowers only after repeated difficulty", () => {
  expect(
    recommendPractice([
      sample("a", 100, 1, false),
      sample("b", 100),
      sample("c", 100),
    ]).level,
  ).toBe(1);
  const strong = [sample("a", 90), sample("b", 90), sample("c", 90)];
  expect(recommendPractice([sample("noise", null, 2), ...strong]).level).toBe(
    2,
  );
  expect(recommendPractice([sample("d", 40, 2), ...strong]).level).toBe(2);
  expect(
    recommendPractice([sample("d", 40, 2), sample("e", 50, 2), ...strong])
      .level,
  ).toBe(1);
  expect(
    recommendPractice([sample("d", 40, 2), sample("d", 40, 2), ...strong])
      .level,
  ).toBe(2);
});

it("names a weak dimension only when repeated usable evidence supports it", () => {
  const scores = {
    clarity: 90,
    fluency: 90,
    grammar: 60,
    vocabulary: 90,
    taskResponse: 90,
  };
  expect(
    recommendPractice([{ ...sample("a", 80), scores }]).focus,
  ).toBeUndefined();
  expect(
    recommendPractice([
      { ...sample("a", 80), scores },
      { ...sample("b", 80), scores },
    ]).focus,
  ).toBe("grammar");
});

it("uses one exercise identity across capitalization, whitespace and translation targets", () => {
  expect(practiceExerciseKey("speaking", " Available ", undefined, 1)).toBe(
    practiceExerciseKey("speaking", "available", undefined, 1),
  );
  expect(
    practiceExerciseKey("speaking", "available", "making-plans", 1),
  ).not.toBe(practiceExerciseKey("speaking", "available", undefined, 1));
});
