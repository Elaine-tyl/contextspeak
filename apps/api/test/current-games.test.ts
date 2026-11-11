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
        !round.id.endsWith("-speak-v2"),
    )
    .sort((a, b) => (a.id < b.id ? -1 : 1))
    .map(({ archived, ...round }) => round);
  expect(history).toHaveLength(215);
  expect(
    createHash("sha256").update(JSON.stringify(history)).digest("hex"),
  ).toBe("1dfaa9f1fd4094e87a21fd3601350c91cd0aeaa80021f5134741655a0bb80dc4");
});

it("offers genuinely harder original tasks in all seven other modes at all ten levels", () => {
  const originals = activeChallenges.filter(
    (round) => round.kind !== "gap-fill" && !round.source,
  );
  expect(originals).toHaveLength(90);
  for (const mode of gameModes.filter((mode) => mode.kind !== "gap-fill")) {
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
  expect(answer("game-picture-1-0-v3").answer).toBe("bridge");
  expect(answer("game-riddle-1-0-v3").answer).toBe("reservation");
  expect(
    new Set(
      originals
        .filter((round) => round.kind === "riddle")
        .map((round) => round.answer),
    ).size,
  ).toBe(10);
  expect(
    new Set(
      originals
        .filter((round) => round.kind === "picture")
        .map((round) => round.answer),
    ).size,
  ).toBe(4);
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

it("requires revised-plan and multiple-constraint inference in advanced picture rounds", () => {
  const byLevel = (level: number) =>
    activeChallenges.find(
      (round) => round.kind === "picture" && round.level === level,
    )!;
  expect(byLevel(8)).toMatchObject({ answer: "book" });
  expect(byLevel(8).audioText).toContain("It has since moved inside");
  expect(byLevel(9).audioText).toContain("cannot operate those vehicles");
  expect(byLevel(9).audioText).toContain("only a supporting measure");
  expect(byLevel(10).audioText).toContain(
    "funding rules now exclude construction and printed publicity",
  );
  for (const level of [8, 9, 10])
    expect(
      byLevel(level).audioText.split(/\s+/u).length,
    ).toBeGreaterThanOrEqual(50);
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
