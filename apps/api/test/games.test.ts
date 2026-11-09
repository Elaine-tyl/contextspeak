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
    listening: { level: 2, passed: [3, ...Array(9).fill(0)] },
    speaking: { level: 1, passed: Array(10).fill(0) },
  });
  const second = challenges
    .filter((round) => round.track === "listening" && round.level === 2)
    .slice(0, 3)
    .map((round) => round.id);
  expect(gameProgress(second).listening.level).toBe(1);
  expect(gameProgress([...ids, ...second]).listening.level).toBe(3);
});
it("has immutable distinct rounds with answer-free public content and enough challenges per level", () => {
  expect(challenges).toHaveLength(83);
  expect(new Set(challenges.map((round) => round.id)).size).toBe(83);
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

it("offers ten gradual levels and preserves legacy completion access", () => {
  for (let level = 1; level <= 10; level++) {
    for (const track of ["listening", "speaking"]) {
      expect(
        challenges.filter(
          (round) => round.level === level && round.track === track,
        ).length,
      ).toBeGreaterThanOrEqual(3);
    }
  }
  expect(
    new Set(
      challenges
        .filter((round) => round.kind === "comprehension" && round.level <= 7)
        .map((round) => round.answer),
    ).size,
  ).toBeGreaterThanOrEqual(3);
  const beginner = challenges.filter((round) => round.level === 1);
  expect(
    beginner
      .filter((round) => round.kind === "dictation")
      .every((round) => round.audioText.split(/\s+/).length <= 3),
  ).toBe(true);
  expect(challenges.find((round) => round.id === "listen-1-0-v1")?.level).toBe(
    8,
  );
  expect(
    gameProgress(["listen-1-0-v1"]).listening.level,
  ).toBeGreaterThanOrEqual(8);
  expect(
    gameProgress(challenges.map((round) => round.id)).listening.level,
  ).toBe(10);
});
