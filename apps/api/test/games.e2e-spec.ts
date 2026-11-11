import { afterAll, beforeAll, expect, it, vi } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import { challenges, activeChallenges } from "../src/games/catalog";
import { FixtureSpeechProvider } from "../src/speech/gemini-speech.provider";
import {
  FixtureSpeakingProvider,
  AssessmentFailure,
  type SpeakingInput,
} from "../src/practice/speaking.provider";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
const assess = vi.fn(async (input: SpeakingInput) =>
  new FixtureSpeakingProvider().assess(input),
);
async function post(body: object, subject = "gamer-a") {
  return request(app.getHttpServer())
    .post("/v1/games/attempts")
    .set("authorization", `Bearer ${await issuer.token(subject)}`)
    .send(body);
}
async function catalog(subject = "gamer-a") {
  return request(app.getHttpServer())
    .get("/v1/games")
    .set("authorization", `Bearer ${await issuer.token(subject)}`);
}
function answer(roundId: string, response?: string) {
  return {
    eventId: randomUUID(),
    roundId,
    completedPlays: 1,
    ...(response ? { response } : {}),
  };
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
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
it("requires authentication, hides solutions and rejects forged marks and locked levels", async () => {
  expect((await request(app.getHttpServer()).get("/v1/games")).status).toBe(
    401,
  );
  const value = await catalog();
  expect(value.status).toBe(200);
  expect(value.body.rounds).toHaveLength(activeChallenges.length);
  expect(value.body.rounds[0]).not.toHaveProperty("answer");
  const imported = value.body.rounds.find(
    (round: { id: string }) =>
      round.id === "open-voa-news-academic-writing-listen-v1",
  );
  expect(imported.source).toMatchObject({
    provider: "VOA Learning English",
    license: "Public-domain-VOA",
    author: "Dan Friedell",
  });
  expect(imported).not.toHaveProperty("answer");
  expect(value.body.progress.listening.level).toBe(1);
  expect(
    (await post({ ...answer("listen-1-0-v2", "text"), score: 100 })).status,
  ).toBe(400);
  expect((await post(answer("listen-2-0-v2", "text"))).status).toBe(409);
  expect(
    (await post({ ...answer("listen-1-0-v2", "text"), completedPlays: 0 }))
      .status,
  ).toBe(400);
  expect((await post(answer("unknown", "text"))).status).toBe(404);
  expect(await db.prisma.gameAttempt.count()).toBe(0);
});
it("coalesces events and unlocks only distinct passing challenges for the same user", async () => {
  const first = challenges.find((round) => round.id === "listen-1-0-v2")!;
  const body = answer(first.id, first.audioText);
  const values = await Promise.all([post(body), post(body)]);
  expect(values.map((value) => value.status)).toEqual([201, 201]);
  expect(values[0].body).toEqual(values[1].body);
  expect(values[0].body.score).toBe(100);
  expect((await post({ ...body, response: "changed" })).status).toBe(409);
  await post(answer(first.id, first.audioText));
  expect((await catalog()).body.progress.listening.passed[0]).toBe(1);
  for (const id of ["listen-1-1-v2", "listen-1-2-v2"]) {
    const round = challenges.find((value) => value.id === id)!;
    expect((await post(answer(id, round.audioText))).status).toBe(201);
  }
  expect((await catalog()).body.progress.listening.level).toBe(2);
  expect((await catalog()).body.progress.speaking.level).toBe(1);
  expect((await catalog("gamer-b")).body.progress.listening.level).toBe(1);
  expect((await post(answer("listen-2-0-v2", "wrong"), "gamer-b")).status).toBe(
    409,
  );
  expect((await post(answer("listen-2-0-v2", "wrong"))).status).toBe(201);
  expect((await catalog()).body.progress.listening.level).toBe(2);
  expect(await db.prisma.reviewEvent.count()).toBe(0);
});
it("grades picture and comprehension choices from server-owned answers", async () => {
  expect(
    (await post({ ...answer("picture-1-v2"), choice: "not-an-option" })).status,
  ).toBe(400);
  const wrong = await post({ ...answer("picture-1-v2"), choice: "book" });
  expect(wrong.body).toMatchObject({
    score: 0,
    feedback: { correct: false, correctAnswer: "Umbrella" },
  });
  const correct = await post({ ...answer("question-1-v2"), choice: "station" });
  expect(correct.body).toMatchObject({
    score: 100,
    feedback: { correct: true },
  });
});
it("assesses recordings once, stores no raw audio and returns cached PK receipts", async () => {
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const body = {
    ...answer("speak-1-2-v2"),
    audio,
    mimeType: "audio/wav",
    durationMs: 1000,
  };
  const values = await Promise.all([post(body), post(body)]);
  expect(values.map((value) => value.status)).toEqual([201, 201]);
  expect(values[0].body).toEqual(values[1].body);
  expect(assess).toHaveBeenCalledTimes(1);
  expect(values[0].body).toMatchObject({
    score: 80,
    feedback: { opponentScore: 43 },
  });
  const row = await db.prisma.gameAttempt.findUniqueOrThrow({
    where: { id: values[0].body.id },
  });
  expect(JSON.stringify(row)).not.toContain(audio);
  expect((row.payload as any).audioHash).toBeTypeOf("string");
  expect((await post(body)).body.id).toBe(row.id);
  expect(assess).toHaveBeenCalledTimes(1);
  expect((await post({ ...body, durationMs: 2000 })).status).toBe(409);
  const other = await post(body, "gamer-b");
  expect(other.status).toBe(201);
  expect(other.body.id).not.toBe(row.id);
});
it("does not save provider failures, and silence cannot unlock or win", async () => {
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const body = {
    ...answer("speak-1-0-v2"),
    audio,
    mimeType: "audio/wav",
    durationMs: 1000,
  };
  assess.mockRejectedValueOnce(new AssessmentFailure(429));
  expect((await post(body)).status).toBe(429);
  const base = await new FixtureSpeakingProvider().assess();
  assess.mockResolvedValueOnce({
    ...base,
    speechDetected: false,
    transcript: "",
    overall: null,
    scores: null,
  });
  const silent = await post(body);
  expect(silent.status).toBe(201);
  expect(silent.body.score).toBeNull();
  expect((await catalog()).body.progress.speaking.passed[0]).toBe(1);
});
it("rejects malformed recording payloads before a provider call", async () => {
  const calls = assess.mock.calls.length;
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  for (const patch of [
    { audio: "bad" },
    { audio: Buffer.from("not a recording").toString("base64") },
    { durationMs: 60001 },
    { audio: Buffer.alloc(2097153).toString("base64") },
    { mimeType: "text/plain" },
  ]) {
    expect(
      (
        await post({
          ...answer("speak-1-0-v2"),
          audio,
          mimeType: "audio/wav",
          durationMs: 1000,
          ...patch,
        })
      ).status,
    ).toBe(400);
  }
  expect(assess.mock.calls.length).toBe(calls);
});

it("does_not_recreate_deleted_accounts_after_AI_finishes_and_cascades_saved_games", async () => {
  await catalog("gamer-delete");
  const user = await db.prisma.user.findUniqueOrThrow({
    where: {
      issuer_subject: { issuer: issuer.issuer, subject: "gamer-delete" },
    },
  });
  const base = await new FixtureSpeakingProvider().assess();
  let release: ((value: typeof base) => void) | undefined;
  assess.mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        release = resolve;
      }),
  );
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const result = post(
    {
      ...answer("speak-1-0-v2"),
      audio,
      mimeType: "audio/wav",
      durationMs: 1000,
    },
    "gamer-delete",
  );
  await vi.waitFor(() => expect(release).toBeTypeOf("function"));
  await db.prisma.user.delete({ where: { id: user.id } });
  release!(base);
  expect((await result).status).toBe(404);
  expect(
    await db.prisma.gameAttempt.count({ where: { userId: user.id } }),
  ).toBe(0);
  const other = await db.prisma.user.findUniqueOrThrow({
    where: { issuer_subject: { issuer: issuer.issuer, subject: "gamer-b" } },
  });
  expect(
    await db.prisma.gameAttempt.count({ where: { userId: other.id } }),
  ).toBeGreaterThan(0);
  await db.prisma.user.delete({ where: { id: other.id } });
  expect(
    await db.prisma.gameAttempt.count({ where: { userId: other.id } }),
  ).toBe(0);
  expect(await db.prisma.gameAttempt.count()).toBeGreaterThan(0);
});

it("grades and saves an imported excerpt with the same replay-safe practice flow", async () => {
  const body = answer("open-tatoeba-12335770-listen-v1", "Help is available.");
  const first = await post(body, "open-content-a");
  expect(first.status).toBe(201);
  expect(first.body.score).toBe(100);
  expect((await post(body, "open-content-a")).body.id).toBe(first.body.id);
  const row = await db.prisma.gameAttempt.findUniqueOrThrow({
    where: { id: first.body.id },
  });
  expect(row.roundId).toBe(body.roundId);
});

it("assesses imported open speaking questions without inventing a single correct answer", async () => {
  const audio = (await new FixtureSpeechProvider().generate()).audio;
  const result = await post(
    {
      ...answer("open-tatoeba-12335770-speak-v1"),
      audio,
      mimeType: "audio/wav",
      durationMs: 1000,
    },
    "open-speaking-a",
  );
  expect(result.status).toBe(201);
  expect(result.body.score).toBe(80);
  expect(result.body.feedback.speaking.rubricVersion).toBe(2);
  expect(result.body.feedback).not.toHaveProperty("correctAnswer");
});
