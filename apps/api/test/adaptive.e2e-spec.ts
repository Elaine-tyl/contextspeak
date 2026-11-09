import { afterAll, beforeAll, expect, it, vi } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import {
  FixtureSpeakingProvider,
  type SpeakingInput,
} from "../src/practice/speaking.provider";
import { FixtureSpeechProvider } from "../src/speech/gemini-speech.provider";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let userId: string;
let itemId: string;
let lookupId: string;
const assess = vi.fn((input: SpeakingInput) =>
  new FixtureSpeakingProvider().assess(input),
);
async function get(path: string, subject = "adaptive-a") {
  return request(app.getHttpServer())
    .get(path)
    .set("authorization", `Bearer ${await issuer.token(subject)}`);
}
async function seed(
  roundId: string,
  score: number | null,
  index: number,
  speaking = false,
) {
  const assessment = speaking
    ? await new FixtureSpeakingProvider().assess()
    : undefined;
  return db.prisma.gameAttempt.create({
    data: {
      userId,
      eventId: randomUUID(),
      roundId,
      score,
      payloadHash: "fixture",
      payload: {},
      feedback: {
        independent: true,
        explanation: "Fixture",
        ...(assessment ? { speaking: assessment } : {}),
      },
      createdAt: new Date(Date.now() + index * 1000),
    },
  });
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  userId = (
    await db.prisma.user.create({
      data: { issuer: issuer.issuer, subject: "adaptive-a" },
    })
  ).id;
  const item = await db.prisma.learningItem.create({
    data: {
      userId,
      text: "available",
      normalized: "available",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
    },
  });
  itemId = item.id;
  lookupId = (
    await db.prisma.lookupEvent.create({
      data: {
        userId,
        candidateId: itemId,
        eventId: randomUUID(),
        payloadHash: "fixture",
        text: "available",
        sourceLanguage: "en",
        targetLanguage: "zh-Hans",
        translationStatus: "succeeded",
        meaning: "有空的",
      },
    })
  ).id;
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    speakingProvider: { assess },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("recommends from owned independent outcomes while preserving earned unlocks", async () => {
  for (let i = 0; i < 3; i++) await seed(`listen-1-${i}-v2`, 90, i);
  const strong = (await get("/v1/games")).body;
  expect(strong.recommendations.listening.level).toBe(2);
  expect(strong.progress.listening.level).toBe(2);
  expect(
    (await get("/v1/games", "adaptive-b")).body.recommendations.listening.level,
  ).toBe(1);
  await seed("listen-2-0-v2", 40, 10);
  await seed("listen-2-1-v2", 45, 11);
  const struggling = (await get("/v1/games")).body;
  expect(struggling.recommendations.listening.level).toBe(1);
  expect(struggling.progress.listening.level).toBe(2);
  expect(struggling.recommendations.speaking.level).toBe(1);
});
it("ignores uncertain speaking scores and reconstructs the difficulty actually shown to the learner", async () => {
  for (let i = 0; i < 3; i++) await seed(`speak-1-${i}-v2`, 80, i, true);
  const task = (await get(`/v1/practice/${itemId}?lookup=${lookupId}`)).body;
  expect(task.difficulty).toBe(2);
  expect(task.criteria).toHaveLength(3);
  const noisy = await seed("speak-2-0-v2", 100, 20, true);
  const base = await new FixtureSpeakingProvider().assess();
  await db.prisma.gameAttempt.update({
    where: { id: noisy.id },
    data: {
      feedback: {
        independent: true,
        explanation: "Limited audio",
        speaking: {
          ...base,
          audioQuality: "limited",
          overall: null,
          scores: null,
        },
      },
    },
  });
  expect((await get("/v1/games")).body.recommendations.speaking.level).toBe(2);
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const result = await request(app.getHttpServer())
    .post(`/v1/practice/${itemId}/speaking-assessment`)
    .set("authorization", `Bearer ${await issuer.token("adaptive-a")}`)
    .send({
      eventId: randomUUID(),
      lookupId,
      audio,
      mimeType: "audio/wav",
      durationMs: 1000,
      difficulty: 1,
      replayed: false,
      revealed: true,
    });
  expect(result.status).toBe(201);
  expect(assess.mock.calls[0][0].task.difficulty).toBe(1);
  expect(assess.mock.calls[0][0].task.criteria).toHaveLength(2);
  const saved = await db.prisma.practiceAttempt.findUniqueOrThrow({
    where: { id: result.body.id },
  });
  expect(saved.task).toMatchObject({ difficulty: 1, independent: false });
});

it("does not let repeated or unscorable recordings push qualifying evidence out of the window", async () => {
  const base = await new FixtureSpeakingProvider().assess();
  for (const { limited, independent } of [
    { limited: false, independent: false },
    { limited: true, independent: false },
    { limited: false, independent: true },
  ]) {
    await db.prisma.gameAttempt.createMany({
      data: Array.from({ length: 35 }, (_, index) => ({
        userId,
        eventId: randomUUID(),
        roundId: "speak-1-0-v2",
        score: limited ? null : 80,
        payloadHash: "fixture",
        payload: {},
        feedback: {
          independent,
          explanation: "Repeat fixture",
          speaking: {
            ...base,
            ...(limited
              ? { audioQuality: "limited", overall: null, scores: null }
              : {}),
          },
        },
        createdAt: new Date(Date.now() + (100 + index) * 1000),
      })),
    });
    expect((await get("/v1/games")).body.recommendations.speaking.level).toBe(
      2,
    );
  }
});
it("allows a delayed independent retry to requalify, but not an immediate repeat", async () => {
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "spaced-a" },
  });
  const base = await new FixtureSpeakingProvider().assess();
  await db.prisma.gameAttempt.create({
    data: {
      userId: user.id,
      eventId: randomUUID(),
      roundId: "speak-1-0-v2",
      score: 40,
      payloadHash: "fixture",
      payload: {},
      feedback: {
        independent: true,
        explanation: "Earlier work",
        speaking: base,
      },
      createdAt: new Date(Date.now() - 2 * 86400000),
    },
  });
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const post = async () =>
    request(app.getHttpServer())
      .post("/v1/games/attempts")
      .set("authorization", `Bearer ${await issuer.token("spaced-a")}`)
      .send({
        eventId: randomUUID(),
        roundId: "speak-1-0-v2",
        completedPlays: 1,
        audio,
        mimeType: "audio/wav",
        durationMs: 1000,
      });
  expect((await post()).body.feedback.independent).toBe(true);
  expect((await post()).body.feedback.independent).toBe(false);
});

it("does not treat capitalization changes of the same saved item as new independent work", async () => {
  const lookup = await db.prisma.lookupEvent.create({
    data: {
      userId,
      candidateId: itemId,
      eventId: randomUUID(),
      payloadHash: "fixture",
      text: "Available",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: "succeeded",
      meaning: "有空的",
    },
  });
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const value = await request(app.getHttpServer())
    .post(`/v1/practice/${itemId}/speaking-assessment`)
    .set("authorization", `Bearer ${await issuer.token("adaptive-a")}`)
    .send({
      eventId: randomUUID(),
      lookupId: lookup.id,
      audio,
      mimeType: "audio/wav",
      durationMs: 1000,
      difficulty: 1,
      replayed: false,
      revealed: false,
    });
  expect(value.status).toBe(201);
  const row = await db.prisma.practiceAttempt.findUniqueOrThrow({
    where: { id: value.body.id },
  });
  expect(row.task).toMatchObject({ independent: false });
});
