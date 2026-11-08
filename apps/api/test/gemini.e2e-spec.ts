import { beforeAll, afterAll, it, expect } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { GeminiTranslationProvider } from "../src/library/gemini.provider";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
let db: Awaited<ReturnType<typeof startDatabase>>,
  issuer: Awaited<ReturnType<typeof startIssuer>>,
  app: Awaited<ReturnType<typeof createApp>>;
let calls = 0;
let quotaExhausted = false;
const input = {
  text: "library",
  sourceLanguage: "en",
  targetLanguage: "zh-Hans",
  context: "I borrowed a book from the library.",
};
const answer = {
  selection: "library",
  sourceLanguage: "en",
  targetLanguage: "zh-Hans",
  kind: "word",
  meaning: "图书馆",
  definition: "通常指图书馆，也可指藏书或程序库。当前场景指借阅书籍的图书馆。",
  partOfSpeech: "名词",
  notes: null,
  collocations: [{ source: "library card", translation: "借书证" }],
  examples: [
    { source: "The library is open.", translation: "图书馆开门了。" },
    { source: "I have a library card.", translation: "我有借书证。" },
  ],
};
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  const provider = new GeminiTranslationProvider({
    apiKey: "fake-key-for-integration",
    fetcher: (async () => {
      calls++;
      if (quotaExhausted) return new Response("{}", { status: 429 });
      return new Response(
        JSON.stringify({
          candidates: [
            {
              finishReason: "STOP",
              content: { parts: [{ text: JSON.stringify(answer) }] },
            },
          ],
          modelVersion: "gemini-3.1-flash-lite",
        }),
      );
    }) as typeof fetch,
  });
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    provider,
  });
});
afterAll(async () => {
  await app?.close();
  await db?.close();
  await issuer?.close();
});
it("stores_one_Gemini_explanation_for_owned_history_and_result_without_exposing_the_key", async () => {
  const token = await issuer.token("gemini-learner");
  const client = (method: "get" | "post" | "put", path: string) =>
    request(app.getHttpServer())
      [method](path)
      .set("authorization", `Bearer ${token}`);
  await client("put", "/v1/settings")
    .send({ enabled: true, excludedOrigins: [] })
    .expect(200);
  const receipt = (
    await client("post", "/v1/lookups")
      .send({ ...input, eventId: randomUUID() })
      .expect(201)
  ).body;
  let result: any;
  for (let i = 0; i < 50; i++) {
    result = (
      await client("get", `/v1/lookups/${receipt.lookupId}/translation`).expect(
        200,
      )
    ).body;
    if (result.translationStatus !== "pending") break;
    await new Promise((r) => setTimeout(r, 50));
  }
  expect(result.translationStatus).toBe("succeeded");
  expect(result.learningDetails).toMatchObject({
    definition: answer.definition,
    examples: answer.examples,
    modelDigest: null,
  });
  const history = (await client("get", "/v1/lookups").expect(200)).body
    .items[0];
  expect(history.learningDetails).toEqual(result.learningDetails);
  expect(calls).toBe(1);
  expect(JSON.stringify({ history, result })).not.toContain(
    "fake-key-for-integration",
  );
  const other = await issuer.token("other");
  await request(app.getHttpServer())
    .get(`/v1/lookups/${receipt.lookupId}/translation`)
    .set("authorization", `Bearer ${other}`)
    .expect(404);
});

it("reuses_owned_translations_and_keeps_vocabulary_readable_during_quota_exhaustion", async () => {
  const token = await issuer.token("cached-learner");
  const client = (method: "get" | "post" | "put", path: string) =>
    request(app.getHttpServer())
      [method](path)
      .set("authorization", `Bearer ${token}`);
  await client("put", "/v1/settings")
    .send({ enabled: true, excludedOrigins: [] })
    .expect(200);
  const capture = async (context = input.context) =>
    (
      await client("post", "/v1/lookups")
        .send({ ...input, context, eventId: randomUUID() })
        .expect(201)
    ).body;
  async function completed(id: string) {
    for (let i = 0; i < 100; i++) {
      const result = (
        await client("get", `/v1/lookups/${id}/translation`).expect(200)
      ).body;
      if (result.translationStatus !== "pending") return result;
      await new Promise((r) => setTimeout(r, 25));
    }
    throw new Error("Translation remained pending");
  }
  const before = calls;
  const [first, second] = await Promise.all([capture(), capture()]);
  const [a, b] = await Promise.all([
    completed(first.lookupId),
    completed(second.lookupId),
  ]);
  expect(a.translationStatus).toBe("succeeded");
  expect(b.learningDetails).toEqual(a.learningDetails);
  expect(calls - before).toBe(1);
  quotaExhausted = true;
  const transient = (
    await client("post", "/v1/translations").send(input).expect(201)
  ).body;
  expect(transient.learningDetails).toEqual(a.learningDetails);
  const third = await capture();
  expect((await completed(third.lookupId)).translationStatus).toBe("succeeded");
  expect(calls - before).toBe(1);
  const missing = await capture("A library of reusable code.");
  expect(await completed(missing.lookupId)).toMatchObject({
    translationStatus: "failed",
    errorCode: "GEMINI_QUOTA_EXCEEDED",
  });
  // Reading stored vocabulary must never ask the exhausted provider to translate.
  const afterQuota = calls;
  const history = (await client("get", "/v1/lookups").expect(200)).body.items;
  expect(history).toHaveLength(4);
  expect(
    history.filter((r: any) => r.translationStatus === "succeeded"),
  ).toHaveLength(3);
  expect((await completed(first.lookupId)).learningDetails).toEqual(
    a.learningDetails,
  );
  const candidates = (await client("get", "/v1/learning-items").expect(200))
    .body.items;
  expect(candidates).toHaveLength(1);
  expect(candidates[0].encounterCount).toBe(4);
  expect(calls).toBe(afterQuota);
  // Another user cannot consume this user's cached explanation.
  const other = await issuer.token("uncached-learner");
  await request(app.getHttpServer())
    .post("/v1/translations")
    .set("authorization", `Bearer ${other}`)
    .send(input)
    .expect(503);
  quotaExhausted = false;
  await client("post", `/v1/lookups/${missing.lookupId}/retry`).expect(201);
  expect((await completed(missing.lookupId)).translationStatus).toBe(
    "succeeded",
  );
  expect(
    (await client("get", "/v1/lookups").expect(200)).body.items,
  ).toHaveLength(4);
});
