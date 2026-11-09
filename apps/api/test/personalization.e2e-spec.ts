import { afterAll, beforeAll, expect, it, vi } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import type { ScenarioEvidence } from "../src/personalization/scenario.provider";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let userId: string;
let evidence: ScenarioEvidence[] = [];
let finish: (() => void) | undefined;
const suggest = vi.fn(async (rows: ScenarioEvidence[]) => {
  evidence = rows;
  if (finish)
    await new Promise<void>((resolve) => {
      finish = resolve;
    });
  return {
    model: "test-model",
    suggestions: [
      {
        scenario: "making-plans" as const,
        confidence: 0.7,
        reason: "Several excerpts mention meeting.",
        evidenceIds: [rows[0].id],
      },
    ],
  };
});
async function call(
  path = "",
  method: "get" | "put" | "post" = "get",
  body?: unknown,
  subject = "personal-a",
) {
  return request(app.getHttpServer())
    [method]("/v1/personalization" + path)
    .set("authorization", `Bearer ${await issuer.token(subject)}`)
    .send(body as object);
}
async function lookup(index: number, context: string) {
  const item = await db.prisma.learningItem.create({
    data: {
      userId,
      text: index ? `unknownword${index}` : "available",
      normalized: `personal-${index}`,
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
    },
  });
  return db.prisma.lookupEvent.create({
    data: {
      userId,
      candidateId: item.id,
      eventId: randomUUID(),
      payloadHash: "test",
      text: item.text,
      context,
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: "succeeded",
      meaning: "test",
    },
  });
}
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
  userId = (
    await db.prisma.user.create({
      data: { issuer: issuer.issuer, subject: "personal-a" },
    })
  ).id;
  app = await createApp({
    prisma: db.prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    scenarioProvider: { suggest },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("requires authentication and returns sparse evidence without making model calls", async () => {
  expect(
    (await request(app.getHttpServer()).get("/v1/personalization")).status,
  ).toBe(401);
  expect((await call()).body.profile.goals).toEqual([]);
  await lookup(0, "We could meet for coffee on the coming weekend.");
  expect((await call("/suggestions", "post")).status).toBe(400);
  expect(suggest).not.toHaveBeenCalled();
});
it("keeps confirmed goals, counts distinct contexts and exposes owned priority provenance", async () => {
  expect(
    (await call("/goals", "put", { goals: ["making-plans", "making-plans"] }))
      .status,
  ).toBe(200);
  expect((await call("/goals", "put", { goals: ["doctor"] })).status).toBe(400);
  for (let i = 1; i <= 9; i++)
    await lookup(
      i,
      `We could meet at a different cafe on weekend number ${i}.`,
    );
  await lookup(10, "We could meet at a different cafe on weekend number 1.");
  const value = (await call()).body;
  expect(value.contextCount).toBe(10);
  expect(value.profile.goals).toEqual(["making-plans"]);
  expect(
    value.priorities.find((x: { word: string }) => x.word === "available")
      .frequency.rank,
  ).toBeGreaterThan(0);
  expect(
    value.priorities.find((x: { word: string }) => x.word === "unknownword1")
      .signals.frequency,
  ).toBeNull();
  expect(
    (await call("", "get", undefined, "personal-b")).body.priorities,
  ).toEqual([]);
});
it("links suggestions to owned excerpts, caches unchanged history and never sets goals automatically", async () => {
  expect((await call("/suggestions", "post")).status).toBe(201);
  const value = (await call()).body;
  expect(value.profile.suggestions[0].evidenceIds).toEqual([evidence[0].id]);
  expect(value.evidence[0].id).toBe(evidence[0].id);
  expect(value.profile.goals).toEqual(["making-plans"]);
  expect((await call("/suggestions", "post")).status).toBe(201);
  expect(suggest).toHaveBeenCalledTimes(1);
  await call("/goals", "put", { goals: [], dismissed: ["making-plans"] });
  expect((await call()).body.profile.dismissed).toEqual(["making-plans"]);
});
it("rejects foreign evidence IDs without persisting them", async () => {
  for (let i = 20; i < 30; i++)
    await lookup(
      i,
      `Please help me with a completely new problem number ${i}.`,
    );
  suggest.mockResolvedValueOnce({
    model: "test-model",
    suggestions: [
      {
        scenario: "making-plans",
        confidence: 0.7,
        reason: "test",
        evidenceIds: [randomUUID()],
      },
    ],
  });
  expect((await call("/suggestions", "post")).status).toBe(503);
  expect((await call()).body.profile.model).toBe("test-model");
});
it("discards model replies when collection consent changes while the call is in flight", async () => {
  const before = suggest.mock.calls.length;
  finish = () => {};
  const pending = call("/suggestions", "post");
  try {
    await vi.waitFor(() => expect(suggest).toHaveBeenCalledTimes(before + 1));
    await db.prisma.user.update({
      where: { id: userId },
      data: { consentVersion: { increment: 1 } },
    });
  } finally {
    finish!();
    finish = undefined;
  }
  expect((await pending).status).toBe(409);
});
it("coalesces analysis while preserving a concurrently confirmed goal", async () => {
  const before = suggest.mock.calls.length;
  finish = () => {};
  const first = call("/suggestions", "post");
  try {
    await vi.waitFor(() => expect(suggest).toHaveBeenCalledTimes(before + 1));
    await call("/goals", "put", { goals: ["asking-for-help"] });
    for (let i = 40; i < 50; i++)
      await lookup(i, `I need help finding a new place with number ${i}.`);
    const second = call("/suggestions", "post");
    await new Promise((resolve) => setTimeout(resolve, 20));
    finish!();
    finish = undefined;
    expect((await first).body.goals).toEqual(["asking-for-help"]);
    expect((await second).body.goals).toEqual(["asking-for-help"]);
    expect(suggest).toHaveBeenCalledTimes(before + 1);
  } finally {
    finish?.();
    finish = undefined;
  }
});
it("keeps contexts captured during a slow analysis eligible for the next request", async () => {
  const before = suggest.mock.calls.length;
  expect((await call("/suggestions", "post")).status).toBe(201);
  expect(suggest).toHaveBeenCalledTimes(before + 1);
  expect(evidence.every((row) => row.context.includes("new place"))).toBe(true);
});
it("uses only different independent reliable practice tasks for a vocabulary gap", async () => {
  const selected = (await call()).body.priorities[0];
  const row = await db.prisma.lookupEvent.findUniqueOrThrow({
    where: { id: selected.lookupId },
  });
  for (const [index, task] of [
    { independent: true, exerciseKey: "same", feedback: { score: 20 } },
    { independent: true, exerciseKey: "same", feedback: { score: 20 } },
    { independent: false, exerciseKey: "hinted", feedback: { score: 0 } },
    {
      independent: true,
      exerciseKey: "uncertain",
      speakingAssessment: {
        rubricVersion: 2,
        audioQuality: "unclear",
        overall: 10,
      },
    },
  ].entries())
    await db.prisma.practiceAttempt.create({
      data: {
        userId,
        eventId: randomUUID(),
        lookupId: row.id,
        payloadHash: "test",
        payload: { mode: "listening" },
        task,
      },
    });
  let item = (await call()).body.priorities.find(
    (value: { id: string }) => value.id === selected.id,
  );
  expect(item.practiceEvidence).toBe(1);
  expect(item.signals.gap).toBeNull();
  await db.prisma.practiceAttempt.create({
    data: {
      userId,
      eventId: randomUUID(),
      lookupId: row.id,
      payloadHash: "test",
      payload: { mode: "listening" },
      task: {
        independent: true,
        exerciseKey: "different",
        feedback: { score: 40 },
      },
    },
  });
  item = (await call()).body.priorities.find(
    (value: { id: string }) => value.id === selected.id,
  );
  expect(item.practiceEvidence).toBe(2);
  expect(item.signals.gap).toBeCloseTo(0.7);
});
