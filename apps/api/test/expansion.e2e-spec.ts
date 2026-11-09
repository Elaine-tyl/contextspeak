import { afterAll, beforeAll, expect, it, vi } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let lookupId: string;
let userId: string;
const details = {
  kind: "word",
  definition: "A shape",
  partOfSpeech: "noun",
  notes: null,
  collocations: [],
  examples: [
    { source: "Fill out the form.", translation: "Fill out the form." },
  ],
  version: 1,
  model: "fixture",
  modelDigest: null,
  promptVersion: 2,
};
const expand = vi.fn(async () => ({
  synonyms: [
    {
      term: "shape",
      difference: "Refers to appearance, not a document.",
      example: {
        source: "It has a round shape.",
        translation: "It has a round shape.",
      },
    },
  ],
  lookalikes: [
    {
      term: "from",
      difference: "A preposition, not a noun.",
      example: { source: "I am from here.", translation: "I am from here." },
    },
  ],
  model: "fixture-expansion",
}));
const translate = vi.fn();
const input = { text: "form", sourceLanguage: "en", targetLanguage: "en" };
async function post(body: object, subject = "expansion-a") {
  return request(app.getHttpServer())
    .post("/v1/expansions")
    .set("authorization", `Bearer ${await issuer.token(subject)}`)
    .send(body);
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  userId = (
    await db.prisma.user.create({
      data: {
        issuer: issuer.issuer,
        subject: "expansion-a",
        collectionEnabled: true,
      },
    })
  ).id;
  const item = await db.prisma.learningItem.create({
    data: {
      userId,
      text: "form",
      normalized: "form",
      sourceLanguage: "en",
      targetLanguage: "en",
    },
  });
  lookupId = (
    await db.prisma.lookupEvent.create({
      data: {
        userId,
        candidateId: item.id,
        eventId: randomUUID(),
        payloadHash: "fixture",
        ...input,
        meaning: "A document",
        translationStatus: "succeeded",
        detectedSourceLanguage: "en",
        learningDetails: details,
      },
    })
  ).id;
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    provider: { translate },
    expansionProvider: { expand },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("enriches one owned saved result without translating again or auto-enrolling related words", async () => {
  expect(
    (await post({ input, meaning: "forged", lookupId }, "expansion-b")).status,
  ).toBe(404);
  const values = await Promise.all([
    post({ input, meaning: "forged", lookupId }),
    post({ input, meaning: "forged", lookupId }),
  ]);
  expect(values.map((value) => value.status)).toEqual([201, 201]);
  expect(expand).toHaveBeenCalledTimes(1);
  expect(translate).not.toHaveBeenCalled();
  const saved = await db.prisma.lookupEvent.findUniqueOrThrow({
    where: { id: lookupId },
  });
  expect(saved.meaning).toBe("A document");
  expect(saved.learningDetails).toMatchObject({
    ...details,
    expansion: { model: "fixture-expansion" },
  });
  await post({ input, meaning: "forged", lookupId });
  expect(expand).toHaveBeenCalledTimes(1);
  expect(await db.prisma.reviewCard.count()).toBe(0);
});
it("keeps collection-off expansion transient and preserves the base result on provider failure", async () => {
  const count = await db.prisma.lookupEvent.count();
  expect((await post({ input, meaning: "A document" })).status).toBe(201);
  expect(await db.prisma.lookupEvent.count()).toBe(count);
  expand.mockRejectedValueOnce(new Error("quota"));
  expect(
    (await post({ input: { ...input, text: "farm" }, meaning: "A place" }))
      .status,
  ).toBe(503);
  expect(
    (await db.prisma.lookupEvent.findUniqueOrThrow({ where: { id: lookupId } }))
      .meaning,
  ).toBe("A document");
});
