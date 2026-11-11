import { expect, it } from "vitest";
import { challenges, publicRound } from "../src/games/catalog";

it("offers complete sentences with gradually more missing words at every level", () => {
  for (let level = 1; level <= 10; level++) {
    const rounds = challenges.filter(
      (round) =>
        round.kind === "gap-fill" &&
        round.level === level &&
        !round.archived &&
        !round.source,
    );
    expect(rounds).toHaveLength(4);
    const count = Math.min(6, 2 + Math.floor((level - 1) / 2));
    for (const round of rounds) {
      expect(round.gapAnswers).toHaveLength(count);
      expect(round.audioText.split(/\s+/u).length).toBeGreaterThanOrEqual(
        6 + 2 * level,
      );
      expect(round.gapText).toHaveLength(count + 1);
      const sentence = round
        .gapText!.map((text, index) => text + (round.gapAnswers![index] ?? ""))
        .join("");
      expect(sentence).toBe(round.audioText);
      expect(sentence).toBe(round.answer);
      expect(round.gapAnswers!.every((word) => !/\s/u.test(word))).toBe(true);
      expect(publicRound(round)).not.toHaveProperty("gapAnswers");
      expect(publicRound(round)).not.toHaveProperty("answer");
    }
  }
});

it("keeps earlier questions immutable while academic and credited news gaps are available", () => {
  expect(
    challenges.find((round) => round.id === "gap-fill-1-0-v1"),
  ).toMatchObject({
    archived: true,
    answer: "I'd like tea, please.",
    gapAnswers: ["tea"],
  });
  expect(
    challenges.find((round) => round.id === "gap-fill-4-0-v1"),
  ).toMatchObject({
    archived: true,
    gapAnswers: ["oat", "instead"],
  });
  for (let level = 1; level <= 10; level++)
    expect(
      challenges.some(
        (round) =>
          round.kind === "gap-fill" &&
          round.level === level &&
          round.topic === "Academic study" &&
          !round.archived,
      ),
    ).toBe(true);
  const news = challenges.filter(
    (round) =>
      round.kind === "gap-fill" &&
      round.source?.provider === "VOA Learning English",
  );
  expect(news).toHaveLength(18);
  const education = news.filter(
    (round) => round.source?.id === "voa-writing-ai-7498850",
  );
  expect(education).toHaveLength(6);
  expect(
    education.every(
      (round) =>
        round.source?.author === "Dan Friedell" &&
        round.source?.publishedAt === "2024-02-24",
    ),
  ).toBe(true);
});

it("targets intermediate-to-advanced vocabulary without changing saved v2 questions", () => {
  expect(
    challenges.find((round) => round.id === "gap-fill-1-0-v2"),
  ).toMatchObject({
    archived: true,
    gapAnswers: ["coffee", "oat"],
    answer: "Could I get a coffee with oat milk to go, please?",
  });
  expect(
    challenges.find((round) => round.id === "gap-fill-1-0-v3"),
  ).toMatchObject({ gapAnswers: ["ingredients", "allergens"] });
  expect(
    challenges.find((round) => round.id === "gap-fill-4-2-v3"),
  ).toMatchObject({
    gapAnswers: ["disruption", "alternative", "reimbursement"],
  });
  expect(
    challenges.find((round) => round.id === "gap-fill-10-3-v3"),
  ).toMatchObject({
    gapAnswers: [
      "methodological",
      "confounding",
      "generalizability",
      "replication",
      "corroborating",
      "tentative",
    ],
  });
  const current = challenges.filter(
    (round) => round.kind === "gap-fill" && !round.archived && !round.source,
  );
  expect(current.every((round) => round.id.endsWith("-v3"))).toBe(true);
  expect(
    current
      .flatMap((round) => round.gapAnswers!)
      .some((word) => ["coffee", "tea", "oat", "bus"].includes(word)),
  ).toBe(false);
});

it("provides credited technology, work and environment reports alongside education news", () => {
  const news = challenges.filter(
    (round) => round.kind === "gap-fill" && round.source && !round.archived,
  );
  expect(new Set(news.map((round) => round.source!.id)).size).toBe(4);
  for (const topic of [
    "News · technology",
    "News · work",
    "News · environment",
  ])
    expect(news.filter((round) => round.topic === topic)).toHaveLength(4);
});
