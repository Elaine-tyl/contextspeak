import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import { challenges, publicRound } from "../src/games/catalog";
import { gameModes } from "@contextspeak/contracts";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
async function call(path: string, body?: object, subject = "reader") {
  const agent = request(app.getHttpServer());
  return (body ? agent.post(path).send(body) : agent.get(path)).set(
    "authorization",
    `Bearer ${await issuer.token(subject)}`,
  );
}
async function start(kind: string, level = 1) {
  return call("/v1/games/levels", { eventId: randomUUID(), kind, level });
}
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

it("lets a new learner start every advertised game with available first-level questions", async () => {
  for (const mode of gameModes) {
    const run = await call(
      "/v1/games/levels",
      {
        eventId: randomUUID(),
        kind: mode.kind,
        level: 1,
      },
      "new-game-player",
    );
    expect(run.status, mode.kind).toBe(201);
    expect(run.body.rounds.length, mode.kind).toBeGreaterThan(0);
    expect(run.body.rounds.length, mode.kind).toBeLessThanOrEqual(3);
    expect(
      run.body.rounds.every(
        (round: any) => round.kind === mode.kind && round.level === 1,
      ),
    ).toBe(true);
  }
});

it("publishes graded text games at all ten levels without solutions or ordered reading text", async () => {
  const catalog = await call("/v1/games");
  expect(catalog.status).toBe(200);
  const publicPassages = new Set(
    catalog.body.rounds
      .filter((round: any) => round.passage)
      .map((round: any) => round.passage.replace(/\s+/gu, " ").trim()),
  );
  for (const round of challenges.filter(
    (round) => round.kind === "reading-order",
  )) {
    expect(publicPassages.has(round.answer.replace(/\s+/gu, " ").trim())).toBe(
      false,
    );
  }
  for (const kind of [
    "word-meaning",
    "word-context",
    "reading-question",
    "reading-order",
  ]) {
    for (let level = 1; level <= 10; level++) {
      const rounds = catalog.body.rounds.filter(
        (round: any) => round.kind === kind && round.level === level,
      );
      expect(rounds.length, `${kind} ${level}`).toBeGreaterThanOrEqual(3);
      for (const round of rounds) {
        expect(round.audioText).toBe(round.recall ? round.passage : "");
        expect(round).not.toHaveProperty("answer");
        expect(round).not.toHaveProperty("explanation");
        if (kind === "reading-order") {
          expect(round).not.toHaveProperty("passage");
          expect(round.tiles.length).toBeGreaterThanOrEqual(3);
        } else expect(round.passage.length).toBeGreaterThan(0);
      }
    }
  }
});
it("grades vocabulary with zero plays, saves once, respects ownership and unlocks only that mode", async () => {
  const first = await start("word-meaning");
  expect(first.status).toBe(201);
  expect(first.body.rounds).toHaveLength(3);
  const round = challenges.find(
    (value) => value.id === first.body.rounds[0].id,
  )!;
  const body = {
    eventId: randomUUID(),
    levelSessionId: first.body.id,
    roundId: round.id,
    completedPlays: 0,
    choice: round.answer,
  };
  expect(
    (await call("/v1/games/attempts", { ...body, levelSessionId: undefined }))
      .status,
  ).toBe(400);
  const saved = await call("/v1/games/attempts", body);
  expect(saved.body.score).toBe(100);
  expect((await call("/v1/games/attempts", body)).body.id).toBe(saved.body.id);
  expect(
    (await call("/v1/games/attempts", { ...body, choice: "changed" })).status,
  ).toBe(409);
  expect(
    (await call(`/v1/games/levels/${first.body.id}`, undefined, "other-reader"))
      .status,
  ).toBe(404);
  for (const item of first.body.rounds.slice(1)) {
    const secret = challenges.find((value) => value.id === item.id)!;
    expect(
      (
        await call("/v1/games/attempts", {
          ...body,
          eventId: randomUUID(),
          roundId: secret.id,
          choice: secret.answer,
        })
      ).body.score,
    ).toBe(100);
  }
  const complete = await call(`/v1/games/levels/${first.body.id}`);
  expect(complete.body).toMatchObject({
    completed: true,
    score: 100,
    passed: true,
    nextLevel: 2,
  });
  expect((await start("word-meaning", 2)).status).toBe(201);
  expect((await start("word-context", 2)).status).toBe(409);
  expect((await start("reading-question", 2)).status).toBe(409);
  const catalog = await call("/v1/games");
  expect(catalog.body.progress.listening.level).toBe(1);
  expect(catalog.body.progress.speaking.level).toBe(1);
  expect(await db.prisma.reviewEvent.count()).toBe(0);
});
it("marks context choices and reading gaps server-side without a grading provider", async () => {
  for (const kind of ["word-context", "reading-question"]) {
    const run = (await start(kind)).body;
    const secret = challenges.find((value) => value.id === run.rounds[0].id)!;
    const body = {
      eventId: randomUUID(),
      levelSessionId: run.id,
      roundId: secret.id,
      completedPlays: secret.recall ? 1 : 0,
    };
    expect(
      (await call("/v1/games/attempts", { ...body, choice: "forged" })).status,
    ).toBe(400);
    const correct = await call("/v1/games/attempts", {
      ...body,
      ...(secret.recall
        ? { gapAnswers: secret.gapAnswers }
        : { choice: secret.answer }),
    });
    expect(correct.status).toBe(201);
    expect(correct.body.score).toBe(100);
    expect(correct.body.feedback.explanation).toBe(secret.explanation);
  }
});
it("checks paragraph order rather than awarding marks for the same words in a wrong sequence", async () => {
  const run = (await start("reading-order")).body;
  const secret = challenges.find((value) => value.id === run.rounds[0].id)!;
  const body = {
    eventId: randomUUID(),
    levelSessionId: run.id,
    roundId: secret.id,
    completedPlays: 0,
  };
  expect(publicRound(secret)).not.toHaveProperty("answer");
  const incorrect = await call("/v1/games/attempts", {
    ...body,
    response: secret.tiles!.join(" "),
  });
  expect(incorrect.status).toBe(201);
  expect(incorrect.body.score).toBe(0);
  const retry = (await start("reading-order")).body;
  const correct = await call("/v1/games/attempts", {
    ...body,
    eventId: randomUUID(),
    levelSessionId: retry.id,
    response: secret.answer,
  });
  expect(correct.body.score).toBe(100);
  // Existing audio exercises must still reject zero-play answers.
  const audioRun = (await start("dictation")).body;
  const audio = challenges.find((value) => value.id === audioRun.rounds[0].id)!;
  expect(
    (
      await call("/v1/games/attempts", {
        ...body,
        eventId: randomUUID(),
        levelSessionId: audioRun.id,
        roundId: audio.id,
        response: audio.answer,
      })
    ).status,
  ).toBe(400);
});

it("includes ten levels of real text puzzles without publishing crossword answers or summary order", async () => {
  const catalog = (await call("/v1/games")).body;
  const publishedWords = new Set(
    catalog.rounds
      .filter((round: any) => round.kind === "word-meaning")
      .map((round: any) => round.passage.toLowerCase().replace(/[^a-z]/gu, "")),
  );
  for (const round of challenges.filter(
    (round) => round.kind === "word-scramble",
  ))
    expect(publishedWords.has(round.answer)).toBe(false);
  for (const kind of [
    "word-scramble",
    "mini-crossword",
    "evidence-hunt",
    "sentence-puzzle",
  ]) {
    for (let level = 1; level <= 10; level++) {
      expect(
        catalog.rounds.filter(
          (round: any) => round.kind === kind && round.level === level,
        ),
      ).toHaveLength(3);
    }
  }
  for (const secret of challenges.filter((round) =>
    ["mini-crossword", "sentence-puzzle"].includes(round.kind),
  )) {
    const publicTask = catalog.rounds.find(
      (round: any) => round.id === secret.id,
    );
    expect(publicTask).not.toHaveProperty("gapAnswers");
    expect(publicTask).not.toHaveProperty("answer");
    if (secret.kind === "sentence-puzzle") {
      expect(publicTask.passage).not.toContain(secret.answer);
      expect(secret.tiles!.join(" ")).not.toBe(secret.answer);
    } else {
      const entries = (publicTask as any).crossword;
      expect(entries).toHaveLength(2);
      const occupied = new Map<string, string>();
      let crossings = 0;
      entries.forEach((entry: any, word: number) => {
        expect(entry.length).toBe(secret.gapAnswers![word].length);
        for (let letter = 0; letter < entry.length; letter++) {
          const key = `${entry.row + (entry.direction === "down" ? letter : 0)}:${entry.column + (entry.direction === "across" ? letter : 0)}`;
          const expected = secret.gapAnswers![word][letter];
          if (occupied.has(key)) {
            expect(occupied.get(key)).toBe(expected);
            crossings++;
          }
          occupied.set(key, expected);
        }
      });
      expect(crossings).toBe(1);
    }
  }
});
it("grades new letter, crossword, evidence and summary puzzles without audio", async () => {
  for (const kind of [
    "word-scramble",
    "mini-crossword",
    "evidence-hunt",
    "sentence-puzzle",
  ]) {
    const run = (await start(kind)).body;
    expect(run.rounds).toHaveLength(3);
    const secret = challenges.find((round) => round.id === run.rounds[0].id)!;
    const answer =
      kind === "mini-crossword"
        ? { gapAnswers: secret.gapAnswers }
        : kind === "evidence-hunt"
          ? { choice: secret.answer }
          : { response: secret.answer };
    const body = {
      eventId: randomUUID(),
      levelSessionId: run.id,
      roundId: secret.id,
      completedPlays: 0,
      ...answer,
    };
    const result = await call("/v1/games/attempts", body);
    expect(result.status).toBe(201);
    expect(result.body.score).toBe(100);
    expect((await call("/v1/games/attempts", body)).body.id).toBe(
      result.body.id,
    );
  }
});
it("marks crossword mistakes per word and rejects incomplete crossword submissions", async () => {
  const run = (await start("mini-crossword")).body;
  const secret = challenges.find((round) => round.id === run.rounds[0].id)!;
  const body = {
    eventId: randomUUID(),
    levelSessionId: run.id,
    roundId: secret.id,
    completedPlays: 0,
  };
  expect(
    (
      await call("/v1/games/attempts", {
        ...body,
        gapAnswers: [secret.gapAnswers![0]],
      })
    ).status,
  ).toBe(400);
  const result = await call("/v1/games/attempts", {
    ...body,
    gapAnswers: [secret.gapAnswers![0], "wrong"],
  });
  expect(result.body.score).toBe(50);
  expect(
    result.body.feedback.gapFill.words.map((word: any) => word.correct),
  ).toEqual([true, false]);
});

it("starts and grades a licensed story recall run with attribution", async () => {
  const opened = await call(
    "/v1/games/levels",
    {
      eventId: randomUUID(),
      kind: "reading-question",
      level: 1,
      source: "African Storybook",
    },
    "story-reader",
  );
  expect(opened.status).toBe(201);
  expect(opened.body.rounds).toHaveLength(3);
  for (const round of opened.body.rounds) {
    expect(round.source).toMatchObject({
      author: "Cornelius Gulere",
      license: "CC-BY-3.0",
    });
    expect(round).not.toHaveProperty("answer");
    const secret = challenges.find((item) => item.id === round.id)!;
    const result = await call(
      "/v1/games/attempts",
      {
        eventId: randomUUID(),
        levelSessionId: opened.body.id,
        roundId: round.id,
        completedPlays: 1,
        gapAnswers: secret.gapAnswers,
      },
      "story-reader",
    );
    expect(result.status).toBe(201);
    expect(result.body.score).toBe(100);
  }
  expect(
    (
      await call(
        `/v1/games/levels/${opened.body.id}`,
        undefined,
        "story-reader",
      )
    ).body,
  ).toMatchObject({ completed: true, score: 100, nextLevel: 2 });
});
