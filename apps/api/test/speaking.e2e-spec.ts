import { afterAll, beforeAll, expect, it, vi } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import { FixtureSpeechProvider } from "../src/speech/gemini-speech.provider";
import { AssessmentFailure } from "../src/practice/speaking.provider";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let itemId: string;
let lookupId: string;
let audio: string;
let savedUpload: ReturnType<typeof input>;
const assessment = {
  speechDetected: true,
  transcript: "I am available on Saturday.",
  scores: {
    clarity: 80,
    fluency: 70,
    grammar: 90,
    vocabulary: 80,
    taskResponse: 80,
  },
  overall: 80,
  rubricVersion: 1 as const,
  model: "fixture",
  strengths: ["Clear sentence."],
  improvements: ["Ask a closing question."],
  suggestedResponse: "I am available on Saturday. Does that work for you?",
};
const assess = vi.fn(
  async (_input: import("../src/practice/speaking.provider").SpeakingInput) =>
    assessment,
);
async function post(body: object, subject = "speaker-a") {
  return request(app.getHttpServer())
    .post(`/v1/practice/${itemId}/speaking-assessment`)
    .set("authorization", `Bearer ${await issuer.token(subject)}`)
    .send(body);
}
function input() {
  return {
    eventId: randomUUID(),
    lookupId,
    audio,
    mimeType: "audio/wav",
    replayed: false,
    revealed: false,
    durationMs: 2000,
  };
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "speaker-a" },
  });
  const item = await db.prisma.learningItem.create({
    data: {
      userId: user.id,
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
        userId: user.id,
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
  audio = (await new FixtureSpeechProvider().generate()).audio;
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
it("checks_ownership_media_and_bounds_before_calling_Gemini", async () => {
  expect((await post(input(), "speaker-b")).status).toBe(404);
  for (const patch of [
    { consent: false },
    { audio: "not audio" },
    { audio: Buffer.from("plain text").toString("base64") },
    { durationMs: 60001 },
    { difficulty: 4 },
    { score: 100 },
    { audio: Buffer.alloc(2097153).toString("base64") },
  ])
    expect((await post({ ...input(), ...patch })).status).toBe(400);
  expect(assess).not.toHaveBeenCalled();
});
it("coalesces_concurrent_retries_stores_feedback_without_audio_and_keeps_FSRS_unchanged", async () => {
  const body = input();
  savedUpload = body;
  const results = await Promise.all([post(body), post(body)]);
  expect(results.map((value) => value.status)).toEqual([201, 201]);
  expect(results[0].body).toEqual(results[1].body);
  expect(results[0].body.speakingAssessment).toEqual(assessment);
  expect(assess).toHaveBeenCalledTimes(1);
  expect(assess.mock.calls[0][0].task.sourceText).toContain("available");
  expect((await post({ ...body, revealed: true })).status).toBe(409);
  const row = await db.prisma.practiceAttempt.findUniqueOrThrow({
    where: { id: results[0].body.id },
  });
  expect(JSON.stringify(row)).not.toContain(audio);
  expect(row.task).toMatchObject({ speakingAssessment: assessment });
  expect(await db.prisma.reviewCard.count()).toBe(0);
  expect(await db.prisma.reviewEvent.count()).toBe(0);
});
it("leaves_failed_provider_results_unsaved_and_allows_same_event_retry", async () => {
  const body = input();
  assess.mockRejectedValueOnce(new AssessmentFailure(429));
  expect((await post(body)).status).toBe(429);
  expect(await db.prisma.practiceAttempt.count()).toBe(1);
  expect((await post(body)).status).toBe(201);
  expect(await db.prisma.practiceAttempt.count()).toBe(2);
});

it("limits_new_assessments_but_keeps_completed_results_available", async () => {
  const calls = assess.mock.calls.length;
  expect((await post(input())).status).toBe(429);
  expect((await post(savedUpload)).status).toBe(201);
  expect(assess).toHaveBeenCalledTimes(calls);
  await db.prisma.lookupEvent.update({
    where: { id: lookupId },
    data: { detectedSourceLanguage: "vi" },
  });
  expect((await post(input())).status).toBe(409);
  await db.prisma.lookupEvent.update({
    where: { id: lookupId },
    data: { detectedSourceLanguage: "en" },
  });
});
it("does_not_resurrect_deleted_vocabulary_after_the_model_finishes", async () => {
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject: "speaker-delete" },
  });
  const item = await db.prisma.learningItem.create({
    data: {
      userId: user.id,
      text: "available",
      normalized: "available",
      sourceLanguage: "en",
      targetLanguage: "en",
    },
  });
  const lookup = await db.prisma.lookupEvent.create({
    data: {
      userId: user.id,
      candidateId: item.id,
      eventId: randomUUID(),
      payloadHash: "fixture",
      text: "available",
      sourceLanguage: "en",
      targetLanguage: "en",
      meaning: "free",
      translationStatus: "succeeded",
    },
  });
  let release: ((value: typeof assessment) => void) | undefined;
  assess.mockImplementationOnce(
    () =>
      new Promise((resolve) => {
        release = resolve;
      }),
  );
  const result = request(app.getHttpServer())
    .post(`/v1/practice/${item.id}/speaking-assessment`)
    .set("authorization", `Bearer ${await issuer.token("speaker-delete")}`)
    .send({ ...input(), lookupId: lookup.id })
    .then((value) => value);
  await vi.waitFor(() => expect(release).toBeTypeOf("function"));
  await db.prisma.learningItem.delete({ where: { id: item.id } });
  release!(assessment);
  expect((await result).status).toBe(404);
  expect(
    await db.prisma.practiceAttempt.count({ where: { userId: user.id } }),
  ).toBe(0);
});

it("saves_four_owned_dialogue_turns_with_followups_and_prevents_cross_scenario_or_fifth_turns", async () => {
  const clock = vi.spyOn(Date, "now");
  let now = new Date().getTime() + 120000;
  clock.mockImplementation(() => now);
  const calls = assess.mock.calls.length;
  let previousTurnId: string | undefined;
  try {
    for (let turn = 1; turn <= 4; turn++) {
      now += 61000;
      assess.mockResolvedValueOnce({
        ...assessment,
        nextTurn: turn < 4 ? `Follow-up ${turn}: where shall we meet?` : null,
      } as typeof assessment);
      const body = {
        ...input(),
        scenario: "making-plans",
        conversation: true,
        ...(previousTurnId ? { previousTurnId } : {}),
      };
      const response = await post(body);
      expect(response.status).toBe(201);
      expect(response.body.dialogue.turn).toBe(turn);
      expect(response.body.dialogue.history).toHaveLength(turn - 1);
      const passed = (assess.mock.calls.at(-1) as unknown as any[])[0];
      expect(passed.dialogue.turn).toBe(turn);
      if (turn > 1) {
        expect(passed.task.sourceText).toContain(`Follow-up ${turn - 1}`);
        expect(passed.dialogue.history.at(-1).response).toBe(
          assessment.transcript,
        );
      }
      expect((await post(body)).body.id).toBe(response.body.id);
      expect(
        (await post({ ...body, eventId: randomUUID() }, "speaker-b")).status,
      ).toBe(404);
      previousTurnId = response.body.id;
    }
    expect(assess.mock.calls.length - calls).toBe(4);
    const body = {
      ...input(),
      scenario: "making-plans",
      conversation: true,
      previousTurnId,
    };
    expect((await post(body)).status).toBe(409);
    expect((await post({ ...body, scenario: "asking-for-help" })).status).toBe(
      409,
    );
    expect((await post({ ...body, previousTurnId: randomUUID() })).status).toBe(
      404,
    );
    expect((await post({ ...body, conversation: undefined })).status).toBe(400);
    expect(assess.mock.calls.length - calls).toBe(4);
  } finally {
    clock.mockRestore();
  }
});
