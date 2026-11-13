import { expect, it } from "vitest";
import corpus from "../src/content/corpus.json";
import { validateCorpus, verifySourceText } from "../src/content/validation";
import { activeChallenges, publicRound } from "../src/games/catalog";

it("offers credited, offline reading packs with three questions at each selected difficulty", () => {
  const reviewed = validateCorpus(corpus);
  const readings = activeChallenges.filter((round) =>
    round.id.startsWith("open-reading-"),
  );
  expect(readings).toHaveLength(15);
  for (const level of [1, 3, 5, 7, 9]) {
    const pack = readings.filter((round) => round.level === level);
    expect(pack).toHaveLength(3);
    for (const round of pack) {
      expect(round.kind).toBe("reading-question");
      expect(round.recall).toBe(true);
      expect(round.audioText).toBe(round.passage);
      expect(round.passage!.length).toBeGreaterThan(350);
      const source = reviewed.sources.find(
        (source) => source.id === round.source!.id,
      )!;
      expect(round.source!.url).toBe(source.url);
      expect(["CC-BY-3.0", "Public-domain-VOA"]).toContain(
        round.source!.license,
      );
      expect(round.gapAnswers!.length).toBeGreaterThanOrEqual(3);
      expect(publicRound(round)).not.toHaveProperty("gapAnswers");
      expect(publicRound(round)).not.toHaveProperty("answer");
      expect(publicRound(round).source).not.toHaveProperty("fetchUrl");
      const excerpts = reviewed.excerpts.filter((excerpt) =>
        source.id === "asp-0001"
          ? excerpt.id === "asp-0001-full"
          : excerpt.sourceId === source.id,
      );
      expect(round.passage).toBe(
        excerpts.map((excerpt) => excerpt.text).join("\n\n"),
      );
    }
  }
});

it("pins and attributes the new complete story for source verification", () => {
  const reviewed = validateCorpus(corpus);
  const source = reviewed.sources.find((source) => source.id === "asp-0001")!;
  const excerpt = reviewed.excerpts.find(
    (excerpt) => excerpt.id === "asp-0001-full",
  )!;
  expect(() =>
    verifySourceText(
      source,
      [excerpt],
      `# Story\n##\n${excerpt.text}\n* License: [CC-BY]\n* Text: Cornelius Gulere\n`,
    ),
  ).not.toThrow();
  expect(() =>
    verifySourceText(source, [excerpt], "Unlicensed replacement"),
  ).toThrow();
});
