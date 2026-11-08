import { beforeAll, afterAll, expect, it } from "vitest";
import request from "supertest";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let calls = 0;
let quota = false;
async function speak(text: string, subject = "speech-a", extra = {}) {
  return request(app.getHttpServer())
    .post("/v1/speech")
    .set("authorization", `Bearer ${await issuer.token(subject)}`)
    .send({ text, language: "en", ...extra });
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    speechProvider: {
      async generate() {
        calls++;
        if (quota) throw Object.assign(new Error("quota"), { status: 429 });
        await new Promise((resolve) => setTimeout(resolve, 10));
        return { audio: "fixture", mimeType: "audio/wav" as const };
      },
    },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("coalesces_repeated_speech_and_keeps_cached_audio_available_during_quota", async () => {
  const results = await Promise.all([speak("academic"), speak("academic")]);
  expect(results.map((result) => result.status)).toEqual([201, 201]);
  expect(calls).toBe(1);
  expect(results[0].body.audio).toBe("fixture");
  quota = true;
  expect((await speak("academic")).status).toBe(201);
  expect(calls).toBe(1);
  expect((await speak("new sentence")).status).toBe(429);
  expect((await speak("academic")).status).toBe(201);
  quota = false;
  expect((await speak("new sentence")).status).toBe(201);
});
it("does_not_share_another_learners_audio_or_accept_unbounded_requests", async () => {
  const before = calls;
  expect((await speak("academic", "speech-b")).status).toBe(201);
  expect(calls).toBe(before + 1);
  expect((await speak("x".repeat(601))).status).toBe(400);
  expect(
    (await speak("academic", "speech-a", { language: "auto" })).status,
  ).toBe(400);
  expect(
    (await speak("academic", "speech-a", { voice: "untrusted" })).status,
  ).toBe(400);
  expect(
    (
      await request(app.getHttpServer())
        .post("/v1/speech")
        .send({ text: "academic", language: "en" })
    ).status,
  ).toBe(401);
});
