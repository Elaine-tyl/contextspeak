import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import { FixtureSpeakingProvider } from "../src/practice/speaking.provider";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let userId: string;
async function get(subject = "progress-a") {
  return request(app.getHttpServer())
    .get("/v1/progress")
    .set("authorization", `Bearer ${await issuer.token(subject)}`);
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  userId = (
    await db.prisma.user.create({
      data: { issuer: issuer.issuer, subject: "progress-a" },
    })
  ).id;
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("requires auth and returns honest empty owned progress", async () => {
  expect((await request(app.getHttpServer()).get("/v1/progress")).status).toBe(
    401,
  );
  const value = await get();
  expect(value.status).toBe(200);
  expect(value.body.vocabulary).toMatchObject({
    saved: 0,
    active: 0,
    paused: 0,
    due: 0,
    reviews: 0,
  });
  expect(value.body.listening).toMatchObject({ scored: 0, average: null });
  expect(value.body.speaking).toMatchObject({
    scored: 0,
    average: null,
    focus: null,
  });
  expect(value.body.games).toHaveLength(7);
});
it("counts owned due reviews, recent difficulty and actual per-mode unlocks", async () => {
  for (const [index, active] of [true, false].entries()) {
    const item = await db.prisma.learningItem.create({
      data: {
        userId,
        text: `word-${index}`,
        normalized: `word-${index}`,
        sourceLanguage: "en",
        targetLanguage: "zh-Hans",
      },
    });
    await db.prisma.reviewCard.create({
      data: {
        itemId: item.id,
        userId,
        prompt: item.text,
        answer: "meaning",
        active,
        dueAt: new Date(Date.now() - 1000),
        schedule: {},
      },
    });
    await db.prisma.reviewEvent.create({
      data: {
        userId,
        eventId: randomUUID(),
        itemId: item.id,
        rating: "again",
        version: 1,
        dueAt: new Date(),
        log: {},
        createdAt: new Date(Date.now() - (index ? 40 : 1) * 86400000),
      },
    });
  }
  await db.prisma.gameLevelSession.create({
    data: {
      userId,
      eventId: randomUUID(),
      kind: "dictation",
      level: 1,
      roundIds: [],
      score: 90,
      completedAt: new Date(),
    },
  });
  const value = (await get()).body;
  expect(value.vocabulary).toMatchObject({
    saved: 2,
    active: 1,
    paused: 1,
    due: 1,
    reviews: 2,
    recentRatings: { again: 1, hard: 0, good: 0, easy: 0 },
  });
  expect(value.vocabulary.difficult).toHaveLength(1);
  expect(
    value.games.find((mode: { kind: string }) => mode.kind === "dictation")
      .level,
  ).toBe(2);
  expect(
    value.games.find((mode: { kind: string }) => mode.kind === "picture").level,
  ).toBe(1);
  expect((await get("progress-b")).body.vocabulary.saved).toBe(0);
});
it("uses reliable speech, withholds repeated or assisted weakness claims, and filters noisy floods before sampling", async () => {
  const base = await new FixtureSpeakingProvider().assess();
  for (const [index, independent] of [false, true, true].entries()) {
    const speaking = { ...base, scores: { ...base.scores!, grammar: 50 } };
    await db.prisma.gameAttempt.create({
      data: {
        userId,
        eventId: randomUUID(),
        roundId: `speak-1-${index}-v2`,
        score: 80,
        payloadHash: "fixture",
        payload: {},
        feedback: { independent, speaking },
      },
    });
  }
  await db.prisma.gameAttempt.createMany({
    data: Array.from({ length: 110 }, () => ({
      userId,
      eventId: randomUUID(),
      roundId: "speak-1-0-v2",
      score: null,
      payloadHash: "fixture",
      payload: {},
      feedback: {
        speaking: {
          ...base,
          audioQuality: "limited",
          overall: null,
          scores: null,
        },
      },
    })),
  });
  const value = (await get()).body;
  expect(value.speaking).toMatchObject({
    scored: 3,
    average: 80,
    unscorable: 110,
    focus: { dimension: "grammar", evidenceCount: 2 },
  });
  expect((await get("progress-b")).body.speaking.focus).toBeNull();
});

it("includes scored vocabulary listening but excludes self-checks and obsolete speech rubrics", async () => {
  const item = await db.prisma.learningItem.findFirstOrThrow({
    where: { userId },
  });
  const lookup = await db.prisma.lookupEvent.create({
    data: {
      userId,
      candidateId: item.id,
      eventId: randomUUID(),
      payloadHash: "fixture",
      text: item.text,
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: "succeeded",
      meaning: "meaning",
    },
  });
  for (const task of [
    { expression: item.text, independent: true, feedback: { score: 70 } },
    { expression: item.text, selfCheck: "good" },
  ])
    await db.prisma.practiceAttempt.create({
      data: {
        userId,
        eventId: randomUUID(),
        lookupId: lookup.id,
        payloadHash: "fixture",
        payload: { mode: "listening" },
        task,
      },
    });
  const base = await new FixtureSpeakingProvider().assess();
  await db.prisma.gameAttempt.create({
    data: {
      userId,
      eventId: randomUUID(),
      roundId: "speak-1-0-v2",
      score: 100,
      payloadHash: "fixture",
      payload: {},
      feedback: { speaking: { ...base, rubricVersion: 1 } },
    },
  });
  await db.prisma.gameAttempt.create({
    data: {
      userId,
      eventId: randomUUID(),
      roundId: "listen-1-0-v2",
      score: 100,
      payloadHash: "fixture",
      payload: {},
      feedback: {},
      createdAt: new Date(Date.now() - 40 * 86400000),
    },
  });
  const value = (await get()).body;
  expect(value.listening).toMatchObject({ scored: 1, average: 70 });
  expect(value.speaking.scored).toBe(3);
});
