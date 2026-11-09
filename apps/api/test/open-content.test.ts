import { expect, it, vi } from "vitest";
import { createHash } from "node:crypto";
import { downloadSource } from "../src/content/download";
import corpus from "../src/content/corpus.json";
import { validateCorpus, verifySourceText } from "../src/content/validation";
import { openChallenges } from "../src/games/open-content";

it("loads reviewed reusable excerpts as attributed listening and speaking tasks", () => {
  const reviewed = validateCorpus(corpus);
  expect(reviewed.excerpts.length).toBeGreaterThanOrEqual(40);
  expect(new Set(reviewed.sources.map((source) => source.provider)).size).toBe(
    3,
  );
  expect(openChallenges).toHaveLength(reviewed.excerpts.length * 2);
  for (const excerpt of reviewed.excerpts) {
    expect(excerpt.sha256).toBe(
      createHash("sha256").update(excerpt.text).digest("hex"),
    );
    const listening = openChallenges.find(
      (round) => round.id === `open-${excerpt.id}-listen-v1`,
    )!;
    const speaking = openChallenges.find(
      (round) => round.id === `open-${excerpt.id}-speak-v1`,
    )!;
    expect(listening.answer).toBe(excerpt.text);
    expect(listening.source?.author).toBeTruthy();
    expect(listening.source?.licenseUrl).toMatch(/^https:/);
    expect(speaking.criteria?.length).toBeGreaterThan(0);
    expect(speaking.audioText.length).toBeLessThanOrEqual(600);
    expect(listening.level).toBe(excerpt.level);
  }
});
it("rejects changed content, duplicate excerpts, missing credit and restricted licenses", () => {
  for (const modify of [
    (value: typeof corpus) => {
      value.excerpts[0].text += " Changed.";
    },
    (value: typeof corpus) => {
      value.excerpts.push({ ...value.excerpts[0], id: "duplicate" });
    },
    (value: typeof corpus) => {
      value.sources[0].author = "";
    },
    (value: typeof corpus) => {
      value.sources[0].license = "CC-BY-NC-3.0";
    },
    (value: typeof corpus) => {
      value.sources[0].fetchUrl = "https://example.com/unreviewed";
    },
    (value: typeof corpus) => {
      value.excerpts[0].level = 11;
    },
  ]) {
    const value = structuredClone(corpus);
    modify(value);
    expect(() => validateCorpus(value)).toThrow();
  }
});
it("verifies Tatoeba license and approval as well as text, and excludes HTML scripts", () => {
  const data = validateCorpus(corpus);
  const source = data.sources.find((source) => source.provider === "Tatoeba")!;
  const excerpts = data.excerpts.filter(
    (excerpt) => excerpt.sourceId === source.id,
  );
  const sentence = {
    id: Number(source.id.replace("tatoeba-", "")),
    text: excerpts[0].text,
    lang: "eng",
    license: "CC0 1.0",
    owner: source.author,
    is_unapproved: false,
  };
  expect(() =>
    verifySourceText(source, excerpts, JSON.stringify(sentence)),
  ).not.toThrow();
  expect(() =>
    verifySourceText(
      source,
      excerpts,
      JSON.stringify({ ...sentence, license: "PROBLEM" }),
    ),
  ).toThrow();
  expect(() =>
    verifySourceText(
      source,
      excerpts,
      JSON.stringify({ ...sentence, is_unapproved: true }),
    ),
  ).toThrow();
  const voa = data.sources.find(
    (source) => source.provider === "VOA Learning English",
  )!;
  const lines = data.excerpts.filter((excerpt) => excerpt.sourceId === voa.id);
  expect(() =>
    verifySourceText(
      voa,
      lines,
      `<script>${lines.map((line) => line.text).join(" ")}</script>`,
    ),
  ).toThrow();
});

it("verifies multi-page story excerpts without treating Markdown separators as spoken text", () => {
  const data = validateCorpus(corpus);
  const story = data.sources.find((source) => source.id === "asp-0087")!;
  const lines = data.excerpts.filter(
    (excerpt) => excerpt.sourceId === story.id,
  );
  const markdown = lines
    .map((line) => line.text.replaceAll(". ", ".\n##\n"))
    .join("\n");
  expect(() =>
    verifySourceText(
      story,
      lines,
      `${markdown}\n* License: [CC-BY]\n* Text: ${story.author}\n`,
    ),
  ).not.toThrow();
});

it("retries transient source connections without accepting permanent failures", async () => {
  const fetcher = vi
    .fn()
    .mockRejectedValueOnce(new TypeError("Connection timeout"))
    .mockResolvedValueOnce(new Response("verified text"));
  expect(
    await downloadSource(
      "https://api.tatoeba.org/v1/sentences/12335770",
      fetcher,
    ),
  ).toBe("verified text");
  expect(fetcher).toHaveBeenCalledTimes(2);
  const missing = vi.fn().mockResolvedValue(new Response("", { status: 404 }));
  await expect(
    downloadSource("https://api.tatoeba.org/v1/sentences/12335770", missing),
  ).rejects.toThrow("404");
  expect(missing).toHaveBeenCalledTimes(1);
});
