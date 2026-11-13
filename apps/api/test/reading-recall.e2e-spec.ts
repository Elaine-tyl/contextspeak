import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import {
  challenges,
  activeChallenges,
  publicRound,
} from "../src/games/catalog";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
beforeAll(async () => {
  db = await startDatabase();
  issuer = await startIssuer();
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
async function call(path: string, body?: object, user = "recall-reader") {
  const agent = request(app.getHttpServer());
  return (body ? agent.post(path).send(body) : agent.get(path)).set(
    "authorization",
    `Bearer ${await issuer.token(user)}`,
  );
}
it("publishes keyword-recall editions at all levels while retaining saved MCQ editions", () => {
  const readings = activeChallenges.filter(
    (round) => round.kind === "reading-question",
  );
  expect(readings.length).toBeGreaterThanOrEqual(30);
  for (const round of readings) {
    expect(round).toHaveProperty("recall", true);
    expect(round.audioText).toBe(round.passage);
    expect(round.gapAnswers!.length).toBeGreaterThanOrEqual(3);
    expect(round.gapAnswers!.length).toBeLessThanOrEqual(6);
    expect(
      round.gapText!.reduce(
        (text, part, index) => text + part + (round.gapAnswers![index] ?? ""),
        "",
      ),
    ).toBe(round.passage);
    expect(publicRound(round)).not.toHaveProperty("gapAnswers");
    expect(round).not.toHaveProperty("choices");
    const original = challenges.find(
      (old) => old.id === round.id.replace(/-v2$/, "-v1"),
    )!;
    expect(original).toMatchObject({ archived: true, audioText: "" });
    expect(original.choices!.length).toBeGreaterThan(1);
  }
});
it("requires narration and an owned run, grades each gap and deduplicates retries", async () => {
  const opened = await call("/v1/games/levels", {
    eventId: randomUUID(),
    kind: "reading-question",
    level: 1,
  });
  expect(opened.status).toBe(201);
  const round = challenges.find(
    (item) => item.id === opened.body.rounds[0].id,
  )!;
  expect(round).toHaveProperty("recall", true);
  const body = {
    eventId: randomUUID(),
    levelSessionId: opened.body.id,
    roundId: round.id,
    completedPlays: 1,
    gapAnswers: round.gapAnswers,
  };
  expect(
    (await call("/v1/games/attempts", { ...body, completedPlays: 0 })).status,
  ).toBe(400);
  expect(
    (await call("/v1/games/attempts", { ...body, levelSessionId: undefined }))
      .status,
  ).toBe(400);
  expect((await call("/v1/games/attempts", body, "other-reader")).status).toBe(
    404,
  );
  expect(
    (await call("/v1/games/attempts", { ...body, gapAnswers: ["short"] }))
      .status,
  ).toBe(400);
  const partial = {
    ...body,
    gapAnswers: round.gapAnswers!.map((word, i) =>
      i ? word.toUpperCase() : "wrong",
    ),
  };
  const saved = await call("/v1/games/attempts", partial);
  expect(saved.status).toBe(201);
  expect(saved.body.score).toBe(
    Math.round(
      ((round.gapAnswers!.length - 1) / round.gapAnswers!.length) * 100,
    ),
  );
  expect(saved.body.feedback.gapFill.words[0]).toMatchObject({
    correct: false,
    expected: round.gapAnswers![0],
  });
  expect((await call("/v1/games/attempts", partial)).body.id).toBe(
    saved.body.id,
  );
  expect((await call("/v1/games/attempts", body)).status).toBe(409);
});

it("resumes and scores a saved multiple-choice edition without requiring new recall audio", async () => {
  await call("/v1/games", undefined, "legacy-reader");
  const user = await db.prisma.user.findFirstOrThrow({
    where: { subject: "legacy-reader" },
  });
  const original = challenges.find(
    (round) => round.id === "game-reading-question-1-0-v1",
  )!;
  const run = await db.prisma.gameLevelSession.create({
    data: {
      userId: user.id,
      eventId: randomUUID(),
      kind: "reading-question",
      level: 1,
      roundIds: [original.id],
    },
  });
  const resumed = await call(
    `/v1/games/levels/${run.id}`,
    undefined,
    "legacy-reader",
  );
  expect(resumed.status).toBe(200);
  expect(resumed.body.rounds[0]).not.toHaveProperty("recall");
  const saved = await call(
    "/v1/games/attempts",
    {
      eventId: randomUUID(),
      levelSessionId: run.id,
      roundId: original.id,
      completedPlays: 0,
      choice: original.answer,
    },
    "legacy-reader",
  );
  expect(saved.status).toBe(201);
  expect(saved.body.score).toBe(100);
});
