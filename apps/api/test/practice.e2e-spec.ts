import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let itemId: string;
let lookupId: string;
let failedId: string;
async function client(
  method: "get" | "post",
  path: string,
  body?: Record<string, unknown>,
  subject = "practice-a",
) {
  const call = request(app.getHttpServer())
    [method](path)
    .set("authorization", `Bearer ${await issuer.token(subject)}`);
  return body === undefined ? call : call.send(body);
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "practice-a" },
  });
  const item = await db.prisma.learningItem.create({
    data: {
      userId: user.id,
      text: "available",
      normalized: "available",
      sourceLanguage: "auto",
      targetLanguage: "zh-Hans",
    },
  });
  itemId = item.id;
  const lookup = await db.prisma.lookupEvent.create({
    data: {
      userId: user.id,
      candidateId: itemId,
      eventId: randomUUID(),
      payloadHash: "fixture",
      text: "available",
      sourceLanguage: "auto",
      detectedSourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: "succeeded",
      meaning: "有空的",
    },
  });
  lookupId = lookup.id;
  failedId = (
    await db.prisma.lookupEvent.create({
      data: {
        userId: user.id,
        candidateId: itemId,
        eventId: randomUUID(),
        payloadHash: "fixture",
        text: "available",
        sourceLanguage: "auto",
        targetLanguage: "zh-Hans",
        translationStatus: "failed",
      },
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
function attempt(mode = "listening") {
  return {
    eventId: randomUUID(),
    lookupId,
    mode,
    selfCheck: "needs-practice",
    plays: 1,
    completedPlays: 1,
    revealed: true,
    replayed: mode === "speaking",
    response: "free to talk",
    durationMs: 0,
  };
}
it("offers_only_owned_successful_encounters_and_resolves_detected_language", async () => {
  const result = await client(
    "get",
    `/v1/practice/${itemId}?lookup=${lookupId}`,
  );
  expect(result.status).toBe(200);
  expect(result.body).toMatchObject({
    lookupId,
    sourceText: "available",
    translation: "有空的",
    language: "en",
    recent: [],
  });
  expect(
    (
      await client(
        "get",
        `/v1/practice/${itemId}?lookup=${lookupId}`,
        undefined,
        "practice-b",
      )
    ).status,
  ).toBe(404);
  expect(
    (await client("get", `/v1/practice/${itemId}?lookup=${failedId}`)).status,
  ).toBe(409);
});
it("lists_successful_owned_vocabulary_even_after_a_failed_retry", async () => {
  const result = await client(
    "get",
    "/v1/practice?q=avail&targetLanguage=zh-Hans",
  );
  expect(result.status).toBe(200);
  expect(result.body.items).toEqual([
    {
      id: itemId,
      text: "available",
      sourceLanguage: "auto",
      targetLanguage: "zh-Hans",
      lookupId,
    },
  ]);
  expect((await client("get", "/v1/practice?q=missing")).body.items).toEqual(
    [],
  );
  expect(
    (await client("get", "/v1/practice", undefined, "practice-b")).body.items,
  ).toEqual([]);
  expect(
    (await client("get", "/v1/practice?targetLanguage=invalid")).status,
  ).toBe(400);
});
it("builds_and_saves_owned_English_scenarios_with_server_snapshots", async () => {
  for (const scenario of ["introductions", "making-plans", "asking-for-help"]) {
    const task = await client(
      "get",
      `/v1/practice/${itemId}?lookup=${lookupId}&scenario=${scenario}`,
    );
    expect(task.status).toBe(200);
    expect(task.body).toMatchObject({
      lookupId,
      language: "en",
      expression: "available",
      scenario: { id: scenario },
    });
    expect(task.body.scenario.goal).toBeTruthy();
    expect(task.body.sourceText).not.toBe("available");
    const input = { ...attempt(), scenario };
    const path = `/v1/practice/${itemId}/attempts`;
    const saved = await client("post", path, input);
    expect(saved.status).toBe(201);
    expect(saved.body.scenario).toBe(scenario);
    expect((await client("post", path, input)).body.id).toBe(saved.body.id);
    expect(
      (await client("post", path, { ...input, scenario: undefined })).status,
    ).toBe(409);
    const row = await db.prisma.practiceAttempt.findUniqueOrThrow({
      where: { id: saved.body.id },
    });
    expect(row.task).toMatchObject({
      scenario: task.body.scenario,
      sourceText: task.body.sourceText,
    });
    expect(
      (
        await client(
          "get",
          `/v1/practice/${itemId}?lookup=${lookupId}&scenario=${scenario}`,
          undefined,
          "practice-b",
        )
      ).status,
    ).toBe(404);
  }
  expect(await db.prisma.reviewCard.count()).toBe(0);
  expect(
    (
      await client(
        "get",
        `/v1/practice/${itemId}?lookup=${failedId}&scenario=introductions`,
      )
    ).status,
  ).toBe(409);
  expect(
    (await client("get", `/v1/practice/${itemId}?scenario=unknown`)).status,
  ).toBe(400);
  expect(
    (
      await client("post", `/v1/practice/${itemId}/attempts`, {
        ...attempt(),
        scenario: "unknown",
      })
    ).status,
  ).toBe(400);
  await db.prisma.lookupEvent.update({
    where: { id: lookupId },
    data: { detectedSourceLanguage: "vi" },
  });
  expect(
    (
      await client(
        "get",
        `/v1/practice/${itemId}?lookup=${lookupId}&scenario=introductions`,
      )
    ).status,
  ).toBe(409);
  expect(
    (
      await client("post", `/v1/practice/${itemId}/attempts`, {
        ...attempt(),
        scenario: "introductions",
      })
    ).status,
  ).toBe(409);
  await db.prisma.lookupEvent.update({
    where: { id: lookupId },
    data: { detectedSourceLanguage: null },
  });
  expect(
    (
      await client(
        "get",
        `/v1/practice/${itemId}?lookup=${lookupId}&scenario=introductions`,
      )
    ).status,
  ).toBe(409);
  await db.prisma.lookupEvent.update({
    where: { id: lookupId },
    data: { detectedSourceLanguage: "en" },
  });
  expect(
    (
      await client("post", `/v1/practice/${itemId}/attempts`, {
        ...attempt(),
        scenario: "introductions",
        task: { sourceText: "untrusted" },
      })
    ).status,
  ).toBe(400);
});
it("grades_dictation_after_submission_and_keeps_retries_and_FSRS_separate", async () => {
  const body = {
    ...attempt(),
    activity: "dictation",
    selfCheck: undefined,
    revealed: false,
    response: "AVAILABLE!",
  };
  const path = `/v1/practice/${itemId}/attempts`;
  const results = await Promise.all([
    client("post", path, body),
    client("post", path, body),
  ]);
  expect(results.map((result) => result.status)).toEqual([201, 201]);
  expect(results[0].body).toEqual(results[1].body);
  expect(results[0].body.feedback).toMatchObject({
    score: 100,
    transcript: "available",
    correctWords: 1,
    totalWords: 1,
  });
  expect(results[0].body.selfCheck).toBeUndefined();
  expect(
    (await client("post", path, { ...body, response: "different" })).status,
  ).toBe(409);
  expect((await client("post", path, body, "practice-b")).status).toBe(404);
  for (const patch of [
    { revealed: true },
    { completedPlays: 0 },
    { response: "..." },
    { mode: "speaking" },
    { feedback: { score: 100 } },
  ]) {
    expect(
      (await client("post", path, { ...body, ...patch, eventId: randomUUID() }))
        .status,
    ).toBe(400);
  }
  expect(await db.prisma.reviewEvent.count()).toBe(0);
  const row = await db.prisma.practiceAttempt.findUniqueOrThrow({
    where: { id: results[0].body.id },
  });
  expect(row.task).toMatchObject({ feedback: results[0].body.feedback });
});
it("saves_once_under_concurrent_retries_and_rejects_changed_event_payloads", async () => {
  const body = attempt();
  const path = `/v1/practice/${itemId}/attempts`;
  const results = await Promise.all([
    client("post", path, body),
    client("post", path, body),
  ]);
  expect(results.map((r) => r.status)).toEqual([201, 201]);
  expect(results[0].body.id).toBe(results[1].body.id);
  expect(
    (await client("post", path, { ...body, selfCheck: "comfortable" })).status,
  ).toBe(409);
  const task = await client("get", `/v1/practice/${itemId}?lookup=${lookupId}`);
  expect(
    task.body.recent.filter(
      (row: { scenario?: string; feedback?: unknown }) =>
        !row.scenario && !row.feedback,
    ),
  ).toHaveLength(1);
  expect(task.body.recent[0]).toMatchObject({
    mode: "listening",
    selfCheck: "needs-practice",
  });
  expect(task.body.recent[0].userId).toBeUndefined();
  expect(await db.prisma.reviewCard.count()).toBe(0);
  expect(await db.prisma.reviewEvent.count()).toBe(0);
});
it("rejects_unplayed_listening_unrecorded_speaking_audio_and_unowned_submissions", async () => {
  const path = `/v1/practice/${itemId}/attempts`;
  for (const body of [
    { ...attempt(), completedPlays: 0 },
    { ...attempt("speaking"), durationMs: 0 },
    { ...attempt("speaking"), durationMs: 2500, replayed: false },
    { ...attempt(), audio: "forbidden" },
    { ...attempt(), response: "x".repeat(2001) },
  ]) {
    expect((await client("post", path, body)).status).toBe(400);
  }
  expect((await client("post", path, attempt(), "practice-b")).status).toBe(
    404,
  );
  const spoken = await client("post", path, {
    ...attempt("speaking"),
    durationMs: 2500,
  });
  expect(spoken.status).toBe(201);
  expect(spoken.body.mode).toBe("speaking");
});
it("deletes_practice_attempts_with_their_vocabulary", async () => {
  await db.prisma.learningItem.delete({ where: { id: itemId } });
  const result = await client(
    "get",
    `/v1/practice/${itemId}?lookup=${lookupId}`,
  );
  expect(result.status).toBe(404);
  const rows = await db.prisma.$queryRawUnsafe<{ count: bigint }[]>(
    'SELECT COUNT(*) AS count FROM "PracticeAttempt"',
  );
  expect(Number(rows[0].count)).toBe(0);
});
