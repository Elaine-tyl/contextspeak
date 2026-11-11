import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import { challenges } from "../src/games/catalog";
import { FixtureSpeechProvider } from "../src/speech/gemini-speech.provider";
import { FixtureSpeakingProvider } from "../src/practice/speaking.provider";
let limited = false;
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
async function call(path: string, body?: object, subject = "level-a") {
  const agent = request(app.getHttpServer());
  return (body ? agent.post(path).send(body) : agent.get(path)).set(
    "authorization",
    `Bearer ${await issuer.token(subject)}`,
  );
}
async function start(
  kind = "dictation",
  level = 1,
  extra = {},
  subject = "level-a",
) {
  return call(
    "/v1/games/levels",
    { eventId: randomUUID(), kind, level, ...extra },
    subject,
  );
}
async function answer(
  run: { id: string; rounds: { id: string }[] },
  index: number,
  response?: string,
  subject = "level-a",
) {
  const round = challenges.find((value) => value.id === run.rounds[index].id)!;
  return call(
    "/v1/games/attempts",
    {
      eventId: randomUUID(),
      levelSessionId: run.id,
      roundId: round.id,
      completedPlays: 1,
      response: response ?? round.audioText,
    },
    subject,
  );
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    speakingProvider: {
      assess: async () => {
        const value = await new FixtureSpeakingProvider().assess();
        return limited
          ? {
              ...value,
              audioQuality: "limited" as const,
              overall: null,
              scores: null,
            }
          : value;
      },
    },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("creates owned immutable runs, filters themes and sources and makes starts replay safe", async () => {
  const body = { eventId: randomUUID(), kind: "dictation", level: 1 };
  const first = await call("/v1/games/levels", body);
  expect(first.status).toBe(201);
  expect(first.body.rounds).toHaveLength(3);
  expect(first.body.score).toBeNull();
  expect((await call("/v1/games/levels", body)).body.id).toBe(first.body.id);
  expect((await call("/v1/games/levels", { ...body, level: 2 })).status).toBe(
    409,
  );
  expect(
    (await call(`/v1/games/levels/${first.body.id}`, undefined, "level-b"))
      .status,
  ).toBe(404);
  expect((await start("dictation", 2)).status).toBe(409);
  expect(
    (
      await start("dictation", 1, {
        source: "VOA Learning English",
        topic: "News · education",
      })
    ).body.rounds.every(
      (round: { source: { provider: string }; topic: string }) =>
        round.source.provider === "VOA Learning English" &&
        round.topic === "News · education",
    ),
  ).toBe(true);
  expect((await start("dictation", 1, { topic: "invented" })).status).toBe(400);
  expect((await start("dictation", 1, { score: 100 })).status).toBe(400);
});
it("only completes after ordered saved answers and unlocks this mode without erasing retries", async () => {
  const run = (await start()).body;
  expect((await answer(run, 1)).status).toBe(409);
  expect((await answer(run, 0, undefined, "level-b")).status).toBe(404);
  expect((await answer(run, 0)).status).toBe(201);
  expect((await call(`/v1/games/levels/${run.id}`)).body.completed).toBe(false);
  expect((await answer(run, 0)).status).toBe(409);
  await answer(run, 1);
  await answer(run, 2);
  const saved = (await call(`/v1/games/levels/${run.id}`)).body;
  expect(saved).toMatchObject({
    completed: true,
    score: 100,
    passed: true,
    nextLevel: 2,
  });
  expect(saved.receipts).toHaveLength(3);
  expect((await start("dictation", 2)).status).toBe(201);
  expect((await start("picture", 2)).status).toBe(409);
  const retry = (await start()).body;
  expect(retry.id).not.toBe(run.id);
  expect((await call(`/v1/games/levels/${run.id}`)).body.score).toBe(100);
});
it("does not pass incomplete or low-scoring levels and isolates theme/source selection", async () => {
  const run = (
    await start("dictation", 1, { source: "VOA Learning English" }, "level-low")
  ).body;
  for (let i = 0; i < run.rounds.length; i++)
    await answer(run, i, "wrong answer", "level-low");
  expect(
    (await call(`/v1/games/levels/${run.id}`, undefined, "level-low")).body,
  ).toMatchObject({
    completed: true,
    score: 0,
    passed: false,
    nextLevel: null,
  });
  expect((await start("dictation", 2, {}, "level-low")).status).toBe(409);
});

it("keeps uncertain audio unscored and completes the highest level without Next", async () => {
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const run = (await start("riddle", 1, {}, "level-limited")).body;
  limited = true;
  try {
    expect(
      (
        await call(
          "/v1/games/attempts",
          {
            eventId: randomUUID(),
            levelSessionId: run.id,
            roundId: run.rounds[0].id,
            completedPlays: 1,
            audio,
            mimeType: "audio/wav",
            durationMs: 1000,
          },
          "level-limited",
        )
      ).status,
    ).toBe(201);
  } finally {
    limited = false;
  }
  expect(
    (await call(`/v1/games/levels/${run.id}`, undefined, "level-limited")).body,
  ).toMatchObject({
    completed: true,
    score: null,
    passed: false,
    nextLevel: null,
  });
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "level-final" },
  });
  await db.prisma.gameLevelSession.createMany({
    data: Array.from({ length: 9 }, (_, index) => ({
      userId: user.id,
      eventId: randomUUID(),
      kind: "pk",
      level: index + 1,
      roundIds: [],
      score: 80,
      completedAt: new Date(),
    })),
  });
  const final = (await start("pk", 10, {}, "level-final")).body;
  await call(
    "/v1/games/attempts",
    {
      eventId: randomUUID(),
      levelSessionId: final.id,
      roundId: final.rounds[0].id,
      completedPlays: 1,
      audio,
      mimeType: "audio/wav",
      durationMs: 1000,
    },
    "level-final",
  );
  expect(
    (await call(`/v1/games/levels/${final.id}`, undefined, "level-final")).body,
  ).toMatchObject({
    completed: true,
    score: 80,
    passed: true,
    nextLevel: null,
  });
});
it("coalesces replayed answers, rejects another answer for the same slot and deletes owned runs", async () => {
  const run = (await start("dictation", 1, {}, "level-concurrent")).body;
  const body = {
    eventId: randomUUID(),
    levelSessionId: run.id,
    roundId: run.rounds[0].id,
    completedPlays: 1,
    response: run.rounds[0].audioText,
  };
  const replay = await Promise.all([
    call("/v1/games/attempts", body, "level-concurrent"),
    call("/v1/games/attempts", body, "level-concurrent"),
  ]);
  expect(replay.map((value) => value.status)).toEqual([201, 201]);
  expect(replay[0].body.id).toBe(replay[1].body.id);
  const next = {
    ...body,
    eventId: randomUUID(),
    roundId: run.rounds[1].id,
    response: run.rounds[1].audioText,
  };
  const racing = await Promise.all([
    call("/v1/games/attempts", next, "level-concurrent"),
    call(
      "/v1/games/attempts",
      { ...next, eventId: randomUUID() },
      "level-concurrent",
    ),
  ]);
  expect(racing.map((value) => value.status).sort()).toEqual([201, 409]);
  const owner = await db.prisma.user.findUniqueOrThrow({
    where: {
      issuer_subject: { issuer: issuer.issuer, subject: "level-concurrent" },
    },
  });
  await db.prisma.user.delete({ where: { id: owner.id } });
  expect(
    await db.prisma.gameLevelSession.count({ where: { id: run.id } }),
  ).toBe(0);
  expect(
    await db.prisma.gameAttempt.count({ where: { levelSessionId: run.id } }),
  ).toBe(0);
});

it("preserves access earned by legacy unscoped game results", async () => {
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "level-legacy" },
  });
  await db.prisma.gameAttempt.createMany({
    data: [0, 1, 2].map((index) => ({
      userId: user.id,
      eventId: randomUUID(),
      roundId: `listen-1-${index}-v2`,
      score: 100,
      payloadHash: "legacy",
      payload: {},
      feedback: {},
    })),
  });
  const value = await call("/v1/games", undefined, "level-legacy");
  expect(value.body.modeProgress.picture.level).toBe(2);
  expect(value.body.modeProgress["gap-fill"].level).toBe(1);
  expect((await start("picture", 2, {}, "level-legacy")).status).toBe(201);
  expect(
    await db.prisma.gameAttempt.count({
      where: { userId: user.id, levelSessionId: null },
    }),
  ).toBe(3);
});

// Simulate a persisted v1 session to protect saved runs during content upgrades.
async function archivedGapRun(level: number, subject: string) {
  const started = await start("gap-fill", level, {}, subject);
  expect(started.status).toBe(201);
  expect(
    started.body.rounds.every((round: { id: string }) =>
      round.id.endsWith("-v3"),
    ),
  ).toBe(true);
  await db.prisma.gameLevelSession.update({
    where: { id: started.body.id },
    data: {
      roundIds: [0, 1, 2].map((index) => `gap-fill-${level}-${index}-v1`),
    },
  });
  const restored = await call(
    `/v1/games/levels/${started.body.id}`,
    undefined,
    subject,
  );
  expect(restored.status).toBe(200);
  expect(
    restored.body.rounds.every(
      (round: { archived?: boolean }) => round.archived,
    ),
  ).toBe(true);
  return restored;
}

it("checks missing words server-side, saves replay-safe feedback and unlocks only this mode", async () => {
  const started = await archivedGapRun(1, "gap-player");
  const run = started.body;
  expect(run.rounds).toHaveLength(3);
  const body = {
    eventId: randomUUID(),
    levelSessionId: run.id,
    roundId: run.rounds[0].id,
    completedPlays: 1,
    gapAnswers: ["  TEA!  "],
  };
  expect((await call("/v1/games/attempts", body, "gap-other")).status).toBe(
    404,
  );
  for (const patch of [
    { completedPlays: 0 },
    { gapAnswers: [] },
    { gapAnswers: ["tea", "extra"] },
    { gapAnswers: [""] },
    { score: 100 },
    { response: "I'd like tea, please." },
    { choice: "tea" },
  ]) {
    expect(
      (await call("/v1/games/attempts", { ...body, ...patch }, "gap-player"))
        .status,
    ).toBe(400);
  }
  const first = await call("/v1/games/attempts", body, "gap-player");
  expect(first.status).toBe(201);
  expect(first.body).toMatchObject({
    score: 100,
    feedback: {
      correctAnswer: "I'd like tea, please.",
      gapFill: {
        score: 100,
        words: [{ answer: "TEA!", expected: "tea", correct: true }],
      },
    },
  });
  expect((await call("/v1/games/attempts", body, "gap-player")).body.id).toBe(
    first.body.id,
  );
  expect(
    (
      await call(
        "/v1/games/attempts",
        { ...body, gapAnswers: ["coffee"] },
        "gap-player",
      )
    ).status,
  ).toBe(409);
  for (const [index, word] of [
    [1, "noon"],
    [2, "bus"],
  ] as const) {
    expect(
      (
        await call(
          "/v1/games/attempts",
          {
            ...body,
            eventId: randomUUID(),
            roundId: run.rounds[index].id,
            gapAnswers: [word],
          },
          "gap-player",
        )
      ).status,
    ).toBe(201);
  }
  const saved = (
    await call(`/v1/games/levels/${run.id}`, undefined, "gap-player")
  ).body;
  expect(saved).toMatchObject({
    score: 100,
    completed: true,
    passed: true,
    nextLevel: 2,
  });
  expect(saved.receipts[0].feedback).toEqual(first.body.feedback);
  const retry = await start("gap-fill", 1, {}, "gap-player");
  expect(retry.status).toBe(201);
  expect(
    retry.body.rounds.every(
      (round: { id: string; archived?: boolean }) =>
        round.id.endsWith("-v3") && !round.archived,
    ),
  ).toBe(true);
  expect((await start("gap-fill", 2, {}, "gap-player")).status).toBe(201);
  expect((await start("dictation", 2, {}, "gap-player")).status).toBe(409);
  const progress = (await call("/v1/progress", undefined, "gap-player")).body;
  expect(progress.games).toContainEqual({
    kind: "gap-fill",
    label: "Missing words",
    track: "listening",
    level: 2,
  });
});

it("scores only the blank words, with equal partial credit and ordered corrections", async () => {
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "gap-marks" },
  });
  await db.prisma.gameLevelSession.createMany({
    data: [1, 2, 3].map((level) => ({
      userId: user.id,
      eventId: randomUUID(),
      kind: "gap-fill",
      level,
      roundIds: [],
      score: 100,
      completedAt: new Date(),
    })),
  });
  for (const [words, score] of [
    [["OAT!", "insted"], 50],
    [["instead", "oat"], 0],
    [["oat milk", "instead"], 50],
  ] as const) {
    const started = await archivedGapRun(4, "gap-marks");
    const run = started.body;
    const result = await call(
      "/v1/games/attempts",
      {
        eventId: randomUUID(),
        levelSessionId: run.id,
        roundId: run.rounds[0].id,
        completedPlays: 1,
        gapAnswers: words,
      },
      "gap-marks",
    );
    expect(result.status).toBe(201);
    expect(result.body.score).toBe(score);
    expect(
      result.body.feedback.gapFill.words.map(
        (word: { expected: string }) => word.expected,
      ),
    ).toEqual(["oat", "instead"]);
  }
});

it("requires a missing-words level session so legacy submissions cannot unlock other modes", async () => {
  for (const [index, word] of ["tea", "noon", "bus"].entries()) {
    const result = await call(
      "/v1/games/attempts",
      {
        eventId: randomUUID(),
        roundId: `gap-fill-1-${index}-v1`,
        gapAnswers: [word],
        completedPlays: 1,
      },
      "gap-unscoped",
    );
    expect(result.status).toBe(400);
  }
  const catalog = (await call("/v1/games", undefined, "gap-unscoped")).body;
  expect(catalog.modeProgress["gap-fill"].level).toBe(1);
  expect(catalog.modeProgress.dictation.level).toBe(1);
  expect(catalog.modeProgress.picture.level).toBe(1);
});

it("starts current academic and news gaps, keeps hidden answers private and grades all six blanks", async () => {
  const subject = "gap-current";
  const academic = await start(
    "gap-fill",
    1,
    { topic: "Academic study" },
    subject,
  );
  expect(academic.status).toBe(201);
  expect(academic.body.rounds).toHaveLength(1);
  expect(academic.body.rounds[0]).toMatchObject({
    id: "gap-fill-1-3-v3",
    topic: "Academic study",
  });
  expect(academic.body.rounds[0]).not.toHaveProperty("gapAnswers");
  expect(academic.body.rounds[0]).not.toHaveProperty("answer");
  const news = await start(
    "gap-fill",
    1,
    { topic: "News · education", source: "VOA Learning English" },
    subject,
  );
  expect(news.status).toBe(201);
  expect(news.body.rounds[0].source).toMatchObject({
    author: "Dan Friedell",
    publishedAt: "2024-02-24",
  });
  const user = await db.prisma.user.findUniqueOrThrow({
    where: { issuer_subject: { issuer: issuer.issuer, subject } },
  });
  await db.prisma.gameLevelSession.createMany({
    data: Array.from({ length: 9 }, (_, index) => ({
      userId: user.id,
      eventId: randomUUID(),
      kind: "gap-fill",
      level: index + 1,
      roundIds: [],
      score: 100,
      completedAt: new Date(),
    })),
  });
  const advanced = await start(
    "gap-fill",
    10,
    { topic: "Academic study" },
    subject,
  );
  expect(advanced.status).toBe(201);
  const result = await call(
    "/v1/games/attempts",
    {
      eventId: randomUUID(),
      levelSessionId: advanced.body.id,
      roundId: advanced.body.rounds[0].id,
      completedPlays: 1,
      gapAnswers: [
        "methodological",
        "confounding",
        "generalizability",
        "replication",
        "corroborating",
        "wrong",
      ],
    },
    subject,
  );
  expect(result.status).toBe(201);
  expect(result.body.score).toBe(83);
  expect(result.body.feedback.gapFill.words[5]).toEqual({
    answer: "wrong",
    expected: "tentative",
    correct: false,
  });
});

it("reopens v2 runs with their original answers while Retry starts v3", async () => {
  const subject = "gap-v2-existing";
  const fresh = await start("gap-fill", 1, { topic: "Café" }, subject);
  expect(fresh.status).toBe(201);
  await db.prisma.gameLevelSession.update({
    where: { id: fresh.body.id },
    data: { roundIds: ["gap-fill-1-0-v2"] },
  });
  const restored = await call(
    `/v1/games/levels/${fresh.body.id}`,
    undefined,
    subject,
  );
  expect(restored.body.rounds[0]).toMatchObject({
    id: "gap-fill-1-0-v2",
    archived: true,
    audioText: "Could I get a coffee with oat milk to go, please?",
  });
  const receipt = await call(
    "/v1/games/attempts",
    {
      eventId: randomUUID(),
      levelSessionId: restored.body.id,
      roundId: "gap-fill-1-0-v2",
      gapAnswers: ["coffee", "oat"],
      completedPlays: 1,
    },
    subject,
  );
  expect(receipt.status).toBe(201);
  expect(receipt.body.score).toBe(100);
  const retry = await start("gap-fill", 1, { topic: "Café" }, subject);
  expect(retry.body.rounds[0].id).toBe("gap-fill-1-0-v3");
  const saved = await call(
    `/v1/games/levels/${restored.body.id}`,
    undefined,
    subject,
  );
  expect(saved.body.receipts[0]).toEqual(receipt.body);
});

it("keeps archived foundational sessions scorable while new starts exclude their source", async () => {
  const subject = "archived-foundation";
  const started = await start("dictation", 1, {}, subject);
  await db.prisma.gameLevelSession.update({
    where: { id: started.body.id },
    data: {
      source: "Tatoeba",
      topic: "Asking for help",
      roundIds: ["open-tatoeba-12335770-listen-v1"],
    },
  });
  const restored = (
    await call(`/v1/games/levels/${started.body.id}`, undefined, subject)
  ).body;
  expect(restored.rounds[0]).toMatchObject({
    archived: true,
    audioText: "Help is available.",
  });
  const receipt = await answer(restored, 0, "Help is available.", subject);
  expect(receipt.status).toBe(201);
  expect(receipt.body.score).toBe(100);
  expect(
    (await start("dictation", 1, { source: "Tatoeba" }, subject)).status,
  ).toBe(400);
  const current = await start("dictation", 1, {}, subject);
  expect(current.body.rounds[0].id).toBe("game-dictation-1-0-v3");
  const saved = (
    await call(`/v1/games/levels/${restored.id}`, undefined, subject)
  ).body;
  expect(saved.receipts[0]).toEqual(receipt.body);
  expect((await start("dictation", 2, {}, subject)).status).toBe(201);
});
