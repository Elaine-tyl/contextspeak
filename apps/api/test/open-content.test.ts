import { expect, it, vi } from "vitest";
import { createHash } from "node:crypto";
import { downloadSource } from "../src/content/download";
import corpus from "../src/content/corpus.json";
import {
  validateCorpus,
  verifySourceText,
  textDigest,
} from "../src/content/validation";
import { openChallenges } from "../src/games/open-content";

it("loads reviewed reusable excerpts as attributed listening and speaking tasks", () => {
  const reviewed = validateCorpus(corpus);
  expect(reviewed.excerpts.length).toBeGreaterThanOrEqual(40);
  expect(new Set(reviewed.sources.map((source) => source.provider)).size).toBe(
    3,
  );
  expect(openChallenges).toHaveLength(
    reviewed.excerpts.reduce(
      (count, excerpt) =>
        count +
        (excerpt.gapTemplate ? 3 : 2) +
        (reviewed.sources.find((source) => source.id === excerpt.sourceId)
          ?.publishedAt
          ? 1
          : 0),
      0,
    ),
  );
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

it("pins the reviewed news authorship and rejects changed gap templates or unreviewed news", () => {
  const source = {
    id: "voa-writing-ai-7498850",
    provider: "VOA Learning English",
    title: "How International Students Should Think about Writing, AI",
    url: "https://learningenglish.voanews.com/a/how-international-students-should-think-about-writing-ai/7498850.html",
    fetchUrl:
      "https://learningenglish.voanews.com/a/how-international-students-should-think-about-writing-ai/7498850.html",
    author: "Dan Friedell",
    publishedAt: "2024-02-24",
    license: "Public-domain-VOA",
    licenseUrl: "https://learningenglish.voanews.com/p/6861.html",
  };
  const text =
    "Students must learn academic writing and research methods when moving from high school to college.";
  const excerpt = {
    id: "voa-news-academic-writing",
    sourceId: source.id,
    text,
    level: 1,
    topic: "News · education",
    question: "What do new students need to learn?",
    sha256: textDigest(text),
    gapTemplate:
      "Students must learn [academic] writing and [research] methods when moving from high school to college.",
  };
  const snapshot = {
    ...corpus,
    sources: [
      ...corpus.sources.filter((value) => value.id !== source.id),
      source,
    ],
    excerpts: [
      ...corpus.excerpts.filter((value) => value.id !== excerpt.id),
      excerpt,
    ],
  };
  const reviewed = validateCorpus(snapshot);
  const news = reviewed.sources.find((value) => value.id === source.id)!;
  const line = reviewed.excerpts.find((value) => value.id === excerpt.id)!;
  expect(() =>
    verifySourceText(
      news,
      [line],
      `<p>${text}</p><p>Dan Friedell wrote this story for Learning English.</p>`,
    ),
  ).not.toThrow();
  expect(() =>
    verifySourceText(
      news,
      [line],
      `<p>${text}</p><p>Adapted from the Associated Press.</p>`,
    ),
  ).toThrow();
  for (const modified of [
    {
      ...snapshot,
      sources: [
        ...snapshot.sources.filter((value) => value.id !== source.id),
        { ...source, author: "Reuters" },
      ],
    },
    {
      ...snapshot,
      sources: [
        ...snapshot.sources.filter((value) => value.id !== source.id),
        {
          ...source,
          fetchUrl:
            "https://learningenglish.voanews.com/a/unreviewed/12345.html",
        },
      ],
    },
    {
      ...snapshot,
      excerpts: [
        ...snapshot.excerpts.filter((value) => value.id !== excerpt.id),
        { ...excerpt, gapTemplate: "Students study [science] and [research]." },
      ],
    },
    {
      ...snapshot,
      excerpts: [
        ...snapshot.excerpts.filter((value) => value.id !== excerpt.id),
        {
          ...excerpt,
          gapTemplate:
            "[Students must] learn academic writing and research methods when moving from high school to college.",
        },
      ],
    },
  ])
    expect(() => validateCorpus(modified)).toThrow();
});

it("preserves punctuation beside inline news markup during live source verification", () => {
  const data = validateCorpus(corpus);
  const source = data.sources.find(
    (value) => value.id === "voa-writing-ai-7498850",
  )!;
  const excerpt = data.excerpts.find(
    (value) => value.id === "voa-news-discovery",
  )!;
  expect(() =>
    verifySourceText(
      source,
      [excerpt],
      `<p>${excerpt.text.replace("journal.", "<strong>journal</strong>.")}</p><p>Dan Friedell wrote this story for Learning English.</p>`,
    ),
  ).not.toThrow();
});

it("accepts only reviewed staff-authored news and keeps syndicated photo credit separate", () => {
  const fixtures = [
    [
      "voa-ancient-ai-6488034",
      "ai-used-to-fill-in-missing-words-in-ancient-writings/6488034.html",
      "2022-03-16",
      "DeepMind, the University of Oxford, the University of Venice and Nature",
      "The system is designed to help [historians] [restore] the writings and identify when and where they were written.",
    ],
    [
      "voa-worker-ai-7296498",
      "study-finds-ai-tools-help-but-also-hurt-worker-performance/7296498.html/",
      "2023-10-04",
      "Harvard Business School",
      "It involved more than 700 business [advisors], called [consultants], from Massachusetts-based Boston Consulting Group.",
    ],
    [
      "voa-carbon-fuel-5090884",
      "researchers-find-way-to-turn-carbon-dioxide-into-valuable-fuel/5090884.html",
      "2019-09-22",
      "Rice University, the Brookhaven National Laboratory and Nature Energy",
      "Scientists blame [greenhouse] gases for being a major cause of [climate] change around the world.",
    ],
  ];
  for (const [id, path, publishedAt, basis, gapTemplate] of fixtures) {
    const url = `https://learningenglish.voanews.com/a/${path}`;
    const source = {
      id,
      provider: "VOA Learning English",
      title: "Reviewed news",
      url,
      fetchUrl: url,
      author: "Bryan Lynn",
      publishedAt,
      license: "Public-domain-VOA",
      licenseUrl: "https://learningenglish.voanews.com/p/6861.html",
    };
    const text = gapTemplate.replace(/\[([^\]]+)\]/gu, "$1");
    const excerpt = {
      id: `${id}-test`,
      sourceId: id,
      text,
      gapTemplate,
      sha256: textDigest(text),
      question: "What is the report about?",
      level: 2,
      topic: "News · technology",
    };
    const snapshot = { ...corpus, sources: [source], excerpts: [excerpt] };
    const checked = validateCorpus(snapshot);
    const byline = `Bryan Lynn wrote this story for VOA Learning English, based on reports from ${basis}.`;
    // A third-party photograph is not imported and does not change the text byline.
    expect(() =>
      verifySourceText(
        checked.sources[0],
        checked.excerpts,
        `<p>Photo: Reuters</p><p>${text}</p><p>${byline}</p>`,
      ),
    ).not.toThrow();
    expect(() =>
      verifySourceText(
        checked.sources[0],
        checked.excerpts,
        `<p>${text}</p><p>Bryan Lynn adapted this story from Reuters.</p>`,
      ),
    ).toThrow();
    for (const patch of [
      { author: "Reuters" },
      { publishedAt: "2025-01-01" },
      { fetchUrl: url.replace(/\d+\.html/, "000.html") },
      { id: "unreviewed-news" },
      { url: `${url}?unreviewed=1`, fetchUrl: `${url}?unreviewed=1` },
    ])
      expect(() =>
        validateCorpus({ ...snapshot, sources: [{ ...source, ...patch }] }),
      ).toThrow();
  }
});
