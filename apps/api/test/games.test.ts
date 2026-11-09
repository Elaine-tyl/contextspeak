import { expect, it } from "vitest";
import { challenges, gameProgress, publicRound } from "../src/games/catalog";
it("unlocks only after three different passing rounds, independently per skill", () => {
  const ids = challenges
    .filter((round) => round.track === "listening" && round.level === 1)
    .slice(0, 3)
    .map((round) => round.id);
  expect(gameProgress([ids[0], ids[0], ids[0]]).listening.level).toBe(1);
  expect(gameProgress(ids.slice(0, 2)).listening.level).toBe(1);
  expect(gameProgress(ids)).toEqual({
    listening: { level: 2, passed: [3, 0, 0] },
    speaking: { level: 1, passed: [0, 0, 0] },
  });
  const second = challenges
    .filter((round) => round.track === "listening" && round.level === 2)
    .slice(0, 3)
    .map((round) => round.id);
  expect(gameProgress(second).listening.level).toBe(1);
  expect(gameProgress([...ids, ...second]).listening.level).toBe(3);
});
it("has immutable distinct rounds with answer-free public content and enough challenges per level", () => {
  expect(challenges).toHaveLength(27);
  expect(new Set(challenges.map((round) => round.id)).size).toBe(27);
  for (const round of challenges) {
    expect(round.audioText.length).toBeLessThanOrEqual(2000);
    expect(publicRound(round)).not.toHaveProperty("answer");
    expect(publicRound(round)).not.toHaveProperty("explanation");
    if (round.choices)
      expect(round.choices.some((choice) => choice.id === round.answer)).toBe(
        true,
      );
  }
});
