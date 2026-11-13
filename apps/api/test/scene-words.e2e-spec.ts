import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { gameModes, speechInputSchema } from "@contextspeak/contracts";
import { createApp } from "../src/app";
import { activeChallenges, publicRound } from "../src/games/catalog";
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
async function call(path: string, body?: object, user = "scene-player") {
  const agent = request(app.getHttpServer());
  return (body ? agent.post(path).send(body) : agent.get(path)).set(
    "authorization",
    `Bearer ${await issuer.token(user)}`,
  );
}
it("has thirty original, speakable room challenges and keeps word notes private until grading", () => {
  expect(gameModes.some((mode) => mode.kind === "scene-words")).toBe(true);
  const rounds = activeChallenges.filter(
    (round) => round.kind === "scene-words",
  );
  expect(rounds).toHaveLength(30);
  for (let level = 1; level <= 10; level++)
    expect(rounds.filter((round) => round.level === level)).toHaveLength(3);
  for (const round of rounds) {
    expect(round.scene?.room).toBeTruthy();
    expect(round.gapAnswers).toHaveLength(1);
    expect(round.gapText).toHaveLength(2);
    expect(round.gapText!.join(round.gapAnswers![0])).toBe(round.audioText);
    expect(
      speechInputSchema.safeParse({ text: round.audioText, language: "en" })
        .success,
    ).toBe(true);
    expect(round.sceneWordNotes!.word.toLowerCase()).toBe(
      round.gapAnswers![0].toLowerCase(),
    );
    expect(publicRound(round)).not.toHaveProperty("sceneWordNotes");
    expect(publicRound(round)).not.toHaveProperty("gapAnswers");
  }
});
it("requires an owned level and completed audio, saves once and unlocks only the scene mode", async () => {
  const opened = await call("/v1/games/levels", {
    eventId: randomUUID(),
    kind: "scene-words",
    level: 1,
  });
  expect(opened.status).toBe(201);
  expect(opened.body.rounds).toHaveLength(3);
  const first = activeChallenges.find(
    (round) => round.id === opened.body.rounds[0].id,
  )!;
  const body = {
    eventId: randomUUID(),
    levelSessionId: opened.body.id,
    roundId: first.id,
    completedPlays: 1,
    gapAnswers: first.gapAnswers,
  };
  expect(
    (await call("/v1/games/attempts", { ...body, completedPlays: 0 })).status,
  ).toBe(400);
  expect(
    (await call("/v1/games/attempts", { ...body, levelSessionId: undefined }))
      .status,
  ).toBe(400);
  expect((await call("/v1/games/attempts", body, "other-player")).status).toBe(
    404,
  );
  const saved = await call("/v1/games/attempts", body);
  expect(saved.status).toBe(201);
  expect(saved.body.score).toBe(100);
  expect(saved.body.feedback.sceneWordNotes).toEqual(first.sceneWordNotes);
  expect((await call("/v1/games/attempts", body)).body.id).toBe(saved.body.id);
  expect(
    (await call("/v1/games/attempts", { ...body, gapAnswers: ["changed"] }))
      .status,
  ).toBe(409);
  for (const round of opened.body.rounds.slice(1)) {
    const secret = activeChallenges.find((item) => item.id === round.id)!;
    expect(
      (
        await call("/v1/games/attempts", {
          ...body,
          eventId: randomUUID(),
          roundId: round.id,
          gapAnswers: secret.gapAnswers,
        })
      ).body.score,
    ).toBe(100);
  }
  const complete = await call(`/v1/games/levels/${opened.body.id}`);
  expect(complete.body).toMatchObject({
    completed: true,
    score: 100,
    nextLevel: 2,
  });
  expect(
    (
      await call("/v1/games/levels", {
        eventId: randomUUID(),
        kind: "scene-words",
        level: 2,
      })
    ).status,
  ).toBe(201);
  expect(
    (
      await call("/v1/games/levels", {
        eventId: randomUUID(),
        kind: "reading-question",
        level: 2,
      })
    ).status,
  ).toBe(409);
});

it("starts scene words at level one even when legacy listening is advanced", async () => {
  const subject = "scene-legacy-player";
  const user = await db.prisma.user.create({
    data: { issuer: issuer.issuer, subject },
  });
  await db.prisma.gameAttempt.create({
    data: {
      userId: user.id,
      eventId: randomUUID(),
      roundId: "listen-1-0-v1",
      score: 100,
      payloadHash: "legacy",
      payload: {},
      feedback: {},
    },
  });
  const catalog = await call("/v1/games", undefined, subject);
  expect(catalog.body.progress.listening.level).toBeGreaterThanOrEqual(8);
  expect(catalog.body.modeProgress["scene-words"].level).toBe(1);
  expect(
    (
      await call(
        "/v1/games/levels",
        {
          eventId: randomUUID(),
          kind: "scene-words",
          level: 2,
        },
        subject,
      )
    ).status,
  ).toBe(409);
});
