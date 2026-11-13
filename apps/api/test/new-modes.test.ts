import { expect, it } from "vitest";
import {
  gameLevelStartSchema,
  gameModes,
  pictureIds,
  pictureLibrary,
} from "@contextspeak/contracts";
import { activeChallenges, publicRound } from "../src/games/catalog";
import { gradeQuickFire, gradeSpelling, shuffle } from "../src/games/new-modes";

const kinds = [
  "spelling",
  "word-order",
  "minimal-pairs",
  "true-false",
] as const;

it("offers three reviewed rounds at every level of each new mode", () => {
  for (const kind of kinds) {
    expect(gameModes.some((mode) => mode.kind === kind)).toBe(true);
    expect(
      gameLevelStartSchema.safeParse({
        eventId: crypto.randomUUID(),
        kind,
        level: 1,
      }).success,
    ).toBe(true);
    for (let level = 1; level <= 10; level++) {
      const rounds = activeChallenges.filter(
        (round) => round.kind === kind && round.level === level,
      );
      expect(rounds, `${kind} Level ${level}`).toHaveLength(3);
      for (const round of rounds) {
        expect(round.track).toBe("listening");
        expect(publicRound(round)).not.toHaveProperty("answer");
        expect(new Set(rounds.map((value) => value.id)).size).toBe(3);
      }
    }
  }
});

it("spelling tiles contain every letter and add decoys from level 4", () => {
  for (const round of activeChallenges.filter(
    (round) => round.kind === "spelling",
  )) {
    const tiles = [...round.tiles!];
    for (const letter of round.answer.toLowerCase()) {
      const at = tiles.indexOf(letter);
      expect(at, `${round.id} needs ${letter}`).toBeGreaterThanOrEqual(0);
      tiles.splice(at, 1);
    }
    expect(tiles).toHaveLength(round.level <= 3 ? 0 : round.level <= 6 ? 2 : 3);
    expect(round.tiles!.join("")).not.toBe(round.answer.toLowerCase());
  }
});

it("sentence tiles rebuild the sentence, with one unneeded word from level 8", () => {
  for (const round of activeChallenges.filter(
    (round) => round.kind === "word-order",
  )) {
    const words = round.answer.replace(/\.$/, "").split(" ");
    expect(round.tiles!.length).toBe(words.length + (round.level >= 8 ? 1 : 0));
    const tiles = round.tiles!.map((tile) => tile.toLowerCase());
    for (const word of words) expect(tiles).toContain(word.toLowerCase());
    expect(round.tiles!.join(" ").toLowerCase()).not.toBe(
      words.join(" ").toLowerCase(),
    );
  }
});

it("sound detective grows from 2 to 4 similar words", () => {
  for (const round of activeChallenges.filter(
    (round) => round.kind === "minimal-pairs",
  )) {
    expect(round.choices).toHaveLength(
      round.level <= 3 ? 2 : round.level <= 6 ? 3 : 4,
    );
    expect(round.choices!.some((choice) => choice.id === round.answer)).toBe(
      true,
    );
  }
});

it("quick fire shows a library picture and tightens the timer", () => {
  for (const round of activeChallenges.filter(
    (round) => round.kind === "true-false",
  )) {
    expect(pictureIds).toContain(round.picture);
    expect(round.timeLimit).toBe(
      round.level <= 3 ? 15 : round.level <= 6 ? 10 : 7,
    );
    expect(["true", "false"]).toContain(round.answer);
  }
  expect(gradeQuickFire(true, 4000, 7)).toBe(100);
  expect(gradeQuickFire(true, 9000, 7)).toBe(60);
  expect(gradeQuickFire(true, undefined, 7)).toBe(60);
  expect(gradeQuickFire(false, 1000, 7)).toBe(0);
});

it("grades spelling by letters and keeps shuffles deterministic", () => {
  expect(gradeSpelling("umbrella", "Umbrella")).toBe(100);
  expect(gradeSpelling("umbrella", "umbrela")).toBe(88);
  expect(gradeSpelling("cat", "dog")).toBe(0);
  expect(shuffle([1, 2, 3, 4, 5], "seed")).toEqual(
    shuffle([1, 2, 3, 4, 5], "seed"),
  );
});

it("uses only pictures that exist in the shared library", () => {
  for (const round of activeChallenges)
    for (const choice of round.choices ?? [])
      if (choice.picture)
        expect(pictureLibrary[choice.picture], choice.picture).toBeDefined();
});
