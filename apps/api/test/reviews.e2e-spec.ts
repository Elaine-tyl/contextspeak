import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let owner: string;
let itemId: string;
let otherItemId: string;
let untranslatedId: string;
async function client(
  method: "get" | "post",
  path: string,
  subject = "review-a",
  body?: Record<string, unknown>,
) {
  const call = request(app.getHttpServer())
    [method](path)
    .set("authorization", `Bearer ${await issuer.token(subject)}`);
  return body === undefined ? call : call.send(body);
}
async function seed(text: string, translated = true) {
  const item = await db.prisma.learningItem.create({
    data: {
      userId: owner,
      text,
      normalized: text.toLowerCase(),
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
    },
  });
  await db.prisma.lookupEvent.create({
    data: {
      userId: owner,
      candidateId: item.id,
      eventId: randomUUID(),
      payloadHash: "fixture",
      text,
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: translated ? "succeeded" : "failed",
      meaning: translated ? "有空的" : null,
      context: "Are you available?",
    },
  });
  return item.id;
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  owner = (
    await db.prisma.user.create({
      data: { issuer: issuer.issuer, subject: "review-a" },
    })
  ).id;
  itemId = await seed("available");
  otherItemId = await seed("ready");
  untranslatedId = await seed("unknown", false);
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
  });
});
afterAll(async () => {
  await app?.close();
  await db?.close();
  await issuer?.close();
});
it("requires_an_owned_successful_translation_and_enrolls_only_once", async () => {
  expect(
    (await client("post", `/v1/reviews/${itemId}/enroll`, "review-b")).status,
  ).toBe(404);
  expect(
    (await client("post", `/v1/reviews/${untranslatedId}/enroll`)).status,
  ).toBe(409);
  const first = await client("post", `/v1/reviews/${itemId}/enroll`);
  expect(first.status).toBe(201);
  expect((await client("post", `/v1/reviews/${itemId}/enroll`)).body).toEqual(
    first.body,
  );
  const queue = await client("get", "/v1/reviews");
  expect(queue.body.dueCount).toBe(1);
  expect(queue.body.items[0]).toMatchObject({
    id: itemId,
    prompt: "available",
    answer: "有空的",
    version: 0,
  });
  expect((await client("get", "/v1/reviews", "review-b")).body.totalCount).toBe(
    0,
  );
});
it("schedules_once_and_rejects_stale_early_or_changed_replays", async () => {
  const body = { eventId: randomUUID(), version: 0, rating: "good" };
  const graded = await client(
    "post",
    `/v1/reviews/${itemId}/rate`,
    "review-a",
    body,
  );
  expect(graded.status).toBe(201);
  expect(graded.body.version).toBe(1);
  expect(new Date(graded.body.dueAt).getTime()).toBeGreaterThan(Date.now());
  expect(
    (await client("post", `/v1/reviews/${itemId}/rate`, "review-a", body)).body,
  ).toEqual(graded.body);
  expect(
    (
      await client("post", `/v1/reviews/${itemId}/rate`, "review-a", {
        ...body,
        rating: "easy",
      })
    ).status,
  ).toBe(409);
  expect(
    (
      await client("post", `/v1/reviews/${itemId}/rate`, "review-a", {
        ...body,
        eventId: randomUUID(),
      })
    ).status,
  ).toBe(409);
  expect(
    (
      await client("post", `/v1/reviews/${itemId}/rate`, "review-a", {
        ...body,
        eventId: randomUUID(),
        version: 1,
      })
    ).status,
  ).toBe(409);
  expect(
    (await client("post", `/v1/reviews/${itemId}/rate`, "review-b", body))
      .status,
  ).toBe(404);
  const queue = (await client("get", "/v1/reviews")).body;
  expect(queue.dueCount).toBe(0);
  expect(queue.nextDueAt).toBe(graded.body.dueAt);
  expect(
    (
      await client("post", `/v1/reviews/${itemId}/rate`, "review-a", {
        ...body,
        rating: "mastered",
      })
    ).status,
  ).toBe(400);
});
it("serializes_competing_ratings_and_pause_resume_preserves_progress", async () => {
  await client("post", `/v1/reviews/${otherItemId}/enroll`);
  const responses = await Promise.all(
    ["again", "easy"].map((rating) =>
      client("post", `/v1/reviews/${otherItemId}/rate`, "review-a", {
        eventId: randomUUID(),
        version: 0,
        rating,
      }),
    ),
  );
  expect(responses.map((response) => response.status).sort()).toEqual([
    201, 409,
  ]);
  expect(
    (await client("post", `/v1/reviews/${otherItemId}/pause`)).status,
  ).toBe(201);
  expect((await client("get", "/v1/reviews")).body.totalCount).toBe(1);
  expect(
    (await client("post", `/v1/reviews/${otherItemId}/enroll`)).body.version,
  ).toBe(1);
});

it("replaying_the_same_event_concurrently_adds_one_review_log", async () => {
  const id = await seed("steady");
  await client("post", `/v1/reviews/${id}/enroll`);
  const input = { eventId: randomUUID(), version: 0, rating: "hard" };
  const [first, second] = await Promise.all([
    client("post", `/v1/reviews/${id}/rate`, "review-a", input),
    client("post", `/v1/reviews/${id}/rate`, "review-a", input),
  ]);
  expect(first.status).toBe(201);
  expect(second.body).toEqual(first.body);
  expect(await db.prisma.reviewEvent.count({ where: { itemId: id } })).toBe(1);
  expect(
    (await db.prisma.reviewCard.findUniqueOrThrow({ where: { itemId: id } }))
      .version,
  ).toBe(1);
});
