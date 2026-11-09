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
        source: "Tatoeba",
        topic: "Asking for help",
      })
    ).body.rounds.every(
      (round: { source: { provider: string }; topic: string }) =>
        round.source.provider === "Tatoeba" &&
        round.topic === "Asking for help",
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
  const run = (await start("dictation", 1, { source: "Tatoeba" }, "level-low"))
    .body;
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
  expect((await start("picture", 2, {}, "level-legacy")).status).toBe(201);
  expect(
    await db.prisma.gameAttempt.count({
      where: { userId: user.id, levelSessionId: null },
    }),
  ).toBe(3);
});
