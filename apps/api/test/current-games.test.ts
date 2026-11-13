import { createHash } from "node:crypto";
import { expect, it } from "vitest";
import { gameModes } from "@contextspeak/contracts";
import {
  challenges,
  activeChallenges,
  publicRound,
} from "../src/games/catalog";

it("keeps every historical non-gap task byte-for-byte apart from its archive flag", () => {
  const history = challenges
    .filter(
      (round) =>
        round.kind !== "gap-fill" &&
        !round.id.startsWith("game-") &&
        // New reading packs and the newly downloaded full story were not in
        // the frozen legacy catalog. Its count and digest remain unchanged.
        !round.id.startsWith("open-reading-") &&
        !round.id.startsWith("open-asp-0001-full-") &&
        !round.id.endsWith("-speak-v2"),
    )
    .sort((a, b) => (a.id < b.id ? -1 : 1))
    .map(({ archived, ...round }) => round);
  expect(history).toHaveLength(215);
  expect(
    createHash("sha256").update(JSON.stringify(history)).digest("hex"),
  ).toBe("1dfaa9f1fd4094e87a21fd3601350c91cd0aeaa80021f5134741655a0bb80dc4");
});

it("offers genuinely harder original v3 tasks in the six original non-picture modes at all ten levels", () => {
  const originals = activeChallenges.filter(
    (round) =>
      round.kind !== "gap-fill" && !round.source && round.id.endsWith("-v3"),
  );
  expect(originals).toHaveLength(80);
  const v3Kinds = [
    "dictation",
    "comprehension",
    "response",
    "riddle",
    "puzzle",
    "pk",
  ];
  for (const mode of gameModes.filter((mode) => v3Kinds.includes(mode.kind))) {
    for (let level = 1; level <= 10; level++) {
      const rounds = originals.filter(
        (round) => round.kind === mode.kind && round.level === level,
      );
      expect(rounds, `${mode.kind} Level ${level}`).toHaveLength(
        mode.kind === "dictation" ? 3 : 1,
      );
      for (const round of rounds) {
        expect(round.id).toMatch(/^game-.*-v3$/);
        expect(round.audioText.split(/\s+/u).length).toBeGreaterThanOrEqual(
          mode.kind === "dictation" ? 12 + level * 2 : 12,
        );
        expect(publicRound(round)).not.toHaveProperty("answer");
        if (round.track === "speaking") {
          expect(round.audioText.length).toBeLessThanOrEqual(600);
          expect(round.criteria!.length).toBeGreaterThanOrEqual(2);
          expect(round.instruction).not.toContain("One word");
        }
      }
    }
  }
  const answer = (id: string) => originals.find((round) => round.id === id)!;
  expect(answer("game-comprehension-1-0-v3").answer).toBe("postpone");
  // v3 picture rounds are archived unchanged so saved receipts keep their meaning.
  const archived = challenges.find(
    (round) => round.id === "game-picture-1-0-v3",
  )!;
  expect(archived).toMatchObject({ archived: true, answer: "bridge" });
  expect(answer("game-riddle-1-0-v3").answer).toBe("reservation");
  expect(
    new Set(
      originals
        .filter((round) => round.kind === "riddle")
        .map((round) => round.answer),
    ).size,
  ).toBe(10);
});

it("archives foundational source rounds and versions news speaking with fair explicit requirements", () => {
  expect(
    activeChallenges.some((round) => round.source?.provider === "Tatoeba"),
  ).toBe(false);
  expect(
    challenges.find((round) => round.id === "open-tatoeba-12335770-listen-v1")
      ?.archived,
  ).toBe(true);
  expect(
    challenges.find((round) => round.id === "speak-1-0-v2")?.archived,
  ).toBe(true);
  const responses = activeChallenges.filter(
    (round) => round.kind === "response" && round.source,
  );
  expect(responses).toHaveLength(18);
  for (const round of responses) {
    expect(round.id).toMatch(/-speak-v2$/);
    expect(round.source?.publishedAt).toBeTruthy();
    expect(round.criteria!.length).toBeGreaterThanOrEqual(2);
    expect(round.audioText.length).toBeLessThanOrEqual(600);
    expect(round.criteria!.join(" ")).not.toContain("No explanation");
  }
  expect(
    activeChallenges.some(
      (round) => round.id === "open-voa-news-analytical-listen-v1",
    ),
  ).toBe(true);
});

it("grows picture rounds from 4 to 6 to a 3 × 3 grid with a new theme each level", () => {
  const themes = new Set<string>();
  for (let level = 1; level <= 10; level++) {
    const rounds = activeChallenges.filter(
      (round) => round.kind === "picture" && round.level === level,
    );
    expect(rounds, `picture Level ${level}`).toHaveLength(3);
    const size = level <= 3 ? 4 : level <= 6 ? 6 : 9;
    for (const round of rounds) {
      expect(round.id).toMatch(/^game-picture-\d+-\d-v4$/);
      expect(round.choices).toHaveLength(size);
      expect(round.choices!.map((choice) => choice.id)).toContain(round.answer);
      expect(
        round.choices!.every((choice) => choice.picture === choice.id),
      ).toBe(true);
      expect(publicRound(round)).not.toHaveProperty("answer");
      // Advanced grids need multi-constraint inference, not just a keyword.
      if (level >= 8)
        expect(round.audioText.split(/\s+/u).length).toBeGreaterThanOrEqual(45);
    }
    expect(new Set(rounds.map((round) => round.answer)).size).toBe(3);
    themes.add(rounds[0].topic!);
  }
  expect(themes.size).toBe(10);
});

it("distinguishes original spoken scenarios from credited real news", () => {
  const originals = activeChallenges.filter(
    (round) =>
      round.track === "speaking" && !round.source && round.kind !== "riddle",
  );
  expect(originals).toHaveLength(30);
  expect(
    originals.every((round) =>
      round.instruction.startsWith("Fictional practice scenario."),
    ),
  ).toBe(true);
  const reports = activeChallenges.filter(
    (round) => round.kind === "response" && round.source,
  );
  expect(
    reports.every(
      (round) => !round.instruction.includes("Fictional practice scenario."),
    ),
  ).toBe(true);
});
