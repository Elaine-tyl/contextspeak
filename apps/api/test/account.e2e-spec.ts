import { afterAll, beforeAll, expect, it, vi } from "vitest";
import request from "supertest";
import { mkdtemp, readdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startOidcIssuer } from "../../../tests/helpers/oidc-issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startOidcIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
let directory: string;
const web = "http://127.0.0.1:3000";
function agent() {
  return request.agent(app.getHttpServer());
}
async function login(subject: string, client = agent()) {
  const start = await client.get("/auth/login").expect(302);
  const url = new URL(start.headers.location);
  url.searchParams.set("test_user", subject);
  const response = await fetch(url, { redirect: "manual" });
  const callback = new URL(response.headers.get("location")!);
  await client.get(callback.pathname + callback.search).expect(302);
  const csrf = (await client.get("/auth/session").expect(200)).body.csrfToken;
  const user = await db.prisma.user.findUniqueOrThrow({
    where: { issuer_subject: { issuer: issuer.issuer, subject } },
  });
  return { client, csrf, user };
}
beforeAll(async () => {
  issuer = await startOidcIssuer();
  db = await startDatabase();
  directory = await mkdtemp(join(tmpdir(), "cs-account-audio-"));
  app = await createApp({
    prisma: db.prisma,
    speechCacheDir: directory,
    oidc: {
      ...issuer,
      webOrigin: web,
      apiOrigin: "http://127.0.0.1:4318",
      allowLoopbackHttp: true,
    },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
  if (directory) await rm(directory, { recursive: true, force: true });
});
it("exports a complete owned snapshot without sessions or another account's data", async () => {
  const a = await login("export-a");
  const b = await login("export-b");
  await db.prisma.learningItem.createMany({
    data: Array.from({ length: 205 }, (_, i) => ({
      userId: a.user.id,
      text: `word ${i}`,
      normalized: `word ${i}`,
      sourceLanguage: "en",
      targetLanguage: "zh",
    })),
  });
  await db.prisma.learningItem.create({
    data: {
      userId: b.user.id,
      text: "OTHER-PRIVATE",
      normalized: "other",
      sourceLanguage: "en",
      targetLanguage: "zh",
    },
  });
  for (const { user } of [a, b]) {
    const item = await db.prisma.learningItem.findFirstOrThrow({
      where: { userId: user.id },
    });
    const text = user.id === a.user.id ? "my answer" : "OTHER-PRIVATE";
    await db.prisma.user.update({
      where: { id: user.id },
      data: { learningProfile: { goal: text } },
    });
    const lookup = await db.prisma.lookupEvent.create({
      data: {
        userId: user.id,
        eventId: "lookup",
        payloadHash: "internal",
        candidateId: item.id,
        text,
        sourceLanguage: "en",
        targetLanguage: "zh",
      },
    });
    await db.prisma.reviewCard.create({
      data: {
        itemId: item.id,
        userId: user.id,
        prompt: text,
        answer: text,
        dueAt: new Date(),
        schedule: {},
      },
    });
    await db.prisma.reviewEvent.create({
      data: {
        userId: user.id,
        eventId: "review",
        itemId: item.id,
        rating: "good",
        version: 1,
        dueAt: new Date(),
        log: { text },
      },
    });
    await db.prisma.practiceAttempt.create({
      data: {
        userId: user.id,
        eventId: "practice",
        lookupId: lookup.id,
        payloadHash: "internal",
        payload: { response: text },
        task: { prompt: text },
      },
    });
    const level = await db.prisma.gameLevelSession.create({
      data: {
        userId: user.id,
        eventId: "level",
        kind: "reading",
        level: 1,
        roundIds: ["round"],
        topic: text,
      },
    });
    await db.prisma.gameAttempt.create({
      data: {
        userId: user.id,
        eventId: "game",
        levelSessionId: level.id,
        roundId: "round",
        payloadHash: "internal",
        payload: { response: text },
        feedback: { message: text },
      },
    });
    await db.prisma.speechAsset.create({
      data: {
        userId: user.id,
        key: user.id.replaceAll("-", "").padEnd(64, "0"),
        bytes: 20,
        requestHash: "internal",
        digest: "internal",
      },
    });
    await db.prisma.extensionPairing.create({
      data: {
        userId: user.id,
        deviceHash: user.id,
        userCodeHash: user.id,
        expiresAt: new Date(Date.now() + 60000),
      },
    });
  }
  const result = await a.client
    .get("/v1/account/export")
    .set("X-CSRF-Token", a.csrf)
    .expect(200);
  expect(result.headers["content-disposition"]).toContain("attachment");
  expect(result.headers["cache-control"]).toBe("no-store");
  const data =
    typeof result.body.schemaVersion === "number"
      ? result.body
      : JSON.parse(result.text);
  expect(data.schemaVersion).toBe(1);
  expect(data.account.subject).toBe("export-a");
  expect(data.learningItems).toHaveLength(205);
  expect(data.account.learningProfile).toEqual({ goal: "my answer" });
  for (const name of [
    "lookups",
    "reviewCards",
    "reviewEvents",
    "practiceAttempts",
    "gameAttempts",
    "gameLevels",
    "speechAssets",
  ])
    expect(data[name], name).toHaveLength(1);

  expect(JSON.stringify(data)).not.toMatch(
    /OTHER-PRIVATE|tokenHash|verifier|browserHash|deviceHash|userCodeHash/,
  );
  expect(Object.keys(data)).toEqual(
    expect.arrayContaining([
      "lookups",
      "reviewCards",
      "reviewEvents",
      "practiceAttempts",
      "gameAttempts",
      "gameLevels",
      "speechAssets",
    ]),
  );
});
it("requires a website session and CSRF plus exact typed confirmation for deletion", async () => {
  await agent().get("/v1/account/export").expect(401);
  const a = await login("delete-guards");
  const { SessionService } = await import("../src/auth/session.service");
  const token = await app
    .get(SessionService)
    .issue(a.user.id, "extension", 3600);
  for (const path of ["export", "delete"]) {
    const call =
      path === "export"
        ? agent().get(`/v1/account/${path}`)
        : agent().post(`/v1/account/${path}`).send({ confirmation: "DELETE" });
    await call.set("Authorization", `Bearer ${token}`).expect(401);
  }
  await a.client
    .post("/v1/account/delete")
    .send({ confirmation: "DELETE" })
    .expect(403);
  await a.client
    .post("/v1/account/delete")
    .set("Origin", "https://evil.invalid")
    .set("X-CSRF-Token", a.csrf)
    .send({ confirmation: "DELETE" })
    .expect(403);
  for (const body of [
    {},
    { confirmation: "delete" },
    { confirmation: " DELETE " },
  ])
    await a.client
      .post("/v1/account/delete")
      .set("Origin", web)
      .set("X-CSRF-Token", a.csrf)
      .send(body)
      .expect(400);
  expect(
    await db.prisma.user.findUnique({ where: { id: a.user.id } }),
  ).not.toBeNull();
  const other = await login("switched-account", a.client);
  await other.client
    .get("/v1/account/export")
    .set("X-CSRF-Token", a.csrf)
    .expect(401);
  await other.client
    .post("/v1/account/delete")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .send({ confirmation: "DELETE" })
    .expect(401);
});
it("deletes owned learning data and credentials, keeps another account, and durably retries failed file cleanup", async () => {
  const a = await login("delete-a");
  const b = await login("delete-b");
  const { AccountService } = await import("../src/account/account.service");
  const { SpeechStore } = await import("../src/speech/speech.store");
  const { FixtureSpeechProvider } =
    await import("../src/speech/gemini-speech.provider");
  const { createHash } = await import("node:crypto");
  const key = createHash("sha256").update(a.user.id).digest("hex");
  const store = new SpeechStore(db.prisma, directory);
  await store.put(a.user.id, key, await new FixtureSpeechProvider().generate());
  const item = await db.prisma.learningItem.create({
    data: {
      userId: a.user.id,
      text: "delete-private",
      normalized: "delete-private",
      sourceLanguage: "en",
      targetLanguage: "zh",
    },
  });
  const lookup = await db.prisma.lookupEvent.create({
    data: {
      userId: a.user.id,
      eventId: "lookup",
      payloadHash: "hash",
      candidateId: item.id,
      text: item.text,
      sourceLanguage: "en",
      targetLanguage: "zh",
    },
  });
  await db.prisma.practiceAttempt.create({
    data: {
      userId: a.user.id,
      lookupId: lookup.id,
      eventId: "practice",
      payloadHash: "hash",
      payload: { audio: "RAW-AUDIO-EXCLUDED", response: "answer" },
      task: { prompt: "Hello" },
    },
  });
  const exported = await a.client
    .get("/v1/account/export")
    .set("X-CSRF-Token", a.csrf)
    .expect(200);
  expect(JSON.stringify(exported.body)).not.toContain("RAW-AUDIO-EXCLUDED");
  expect(exported.body.practiceAttempts[0].payload.response).toBe("answer");
  const { SessionService } = await import("../src/auth/session.service");
  const extension = await app
    .get(SessionService)
    .issue(a.user.id, "extension", 3600);
  // Fail after the durable DB deletion: response still confirms deletion and marks physical cleanup pending.
  const cleanup = vi
    .spyOn(SpeechStore.prototype, "cleanup")
    .mockRejectedValueOnce(new Error("disk unavailable"));
  const deleted = await a.client
    .post("/v1/account/delete")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .send({ confirmation: "DELETE" })
    .expect(200);
  expect(deleted.body).toEqual({ deleted: true, cleanupPending: true });
  cleanup.mockRestore();
  expect(
    await db.prisma.user.findUnique({ where: { id: a.user.id } }),
  ).toBeNull();
  expect(
    await db.prisma.learningItem.count({ where: { userId: a.user.id } }),
  ).toBe(0);
  expect(
    await db.prisma.lookupEvent.count({ where: { userId: a.user.id } }),
  ).toBe(0);
  expect(
    await db.prisma.practiceAttempt.count({ where: { userId: a.user.id } }),
  ).toBe(0);
  expect(
    await db.prisma.authSession.count({ where: { userId: a.user.id } }),
  ).toBe(0);
  expect(
    await db.prisma.user.findUnique({ where: { id: b.user.id } }),
  ).not.toBeNull();
  await a.client.get("/auth/session").expect(401);
  await agent()
    .get("/v1/identity")
    .set("Authorization", `Bearer ${extension}`)
    .expect(401);
  expect(await readdir(directory)).toContain(key + ".wav");
  expect(
    (
      await db.prisma.accountDeletion.findUniqueOrThrow({
        where: { userId: a.user.id },
      })
    ).cleanupCompletedAt,
  ).toBeNull();
  await app.get(AccountService).retryCleanup();
  expect(await readdir(directory)).not.toContain(key + ".wav");
  expect(
    (
      await db.prisma.accountDeletion.findUniqueOrThrow({
        where: { userId: a.user.id },
      })
    ).cleanupCompletedAt,
  ).not.toBeNull();
  const fresh = await login("delete-a");
  expect(fresh.user.id).not.toBe(a.user.id);
  await app.get(AccountService).retryCleanup();
  await fresh.client.get("/auth/session").expect(200);
});
it("unlinks staging before writing private data and closes the descriptor on consumer failure", async () => {
  const a = await login("export-failure");
  const { AccountService } = await import("../src/account/account.service");
  const existing = (await readdir(tmpdir())).filter((name) =>
    name.startsWith("contextspeak-export-"),
  );
  let descriptor: import("node:fs/promises").FileHandle | undefined;
  await expect(
    app.get(AccountService).withExport(a.user.id, async (file) => {
      expect(typeof file.fd).toBe("number");
      descriptor = file;
      const chunks: Buffer[] = [];
      for await (const chunk of file.createReadStream({
        start: 0,
        autoClose: false,
      }))
        chunks.push(chunk);
      expect(JSON.parse(Buffer.concat(chunks).toString()).account.subject).toBe(
        "export-failure",
      );
      expect(
        (await readdir(tmpdir())).filter((name) =>
          name.startsWith("contextspeak-export-"),
        ),
      ).toEqual(existing);
      throw new Error("client disconnected");
    }),
  ).rejects.toThrow("client disconnected");
  await expect(descriptor!.stat()).rejects.toThrow();
});
it("keeps missing-store cleanup pending across retries until storage is configured", async () => {
  const a = await login("delete-no-store");
  const { AccountService } = await import("../src/account/account.service");
  const { SpeechStore } = await import("../src/speech/speech.store");
  const { FixtureSpeechProvider } =
    await import("../src/speech/gemini-speech.provider");
  const key = "d".repeat(64);
  const store = new SpeechStore(db.prisma, directory);
  await store.put(a.user.id, key, await new FixtureSpeechProvider().generate());
  const withoutStore = new AccountService(db.prisma);
  expect(await withoutStore.remove(a.user.id)).toEqual({
    deleted: true,
    cleanupPending: true,
  });
  await withoutStore.retryCleanup();
  expect(
    (
      await db.prisma.accountDeletion.findUniqueOrThrow({
        where: { userId: a.user.id },
      })
    ).cleanupCompletedAt,
  ).toBeNull();
  await new AccountService(db.prisma, store).retryCleanup();
  expect(
    (
      await db.prisma.accountDeletion.findUniqueOrThrow({
        where: { userId: a.user.id },
      })
    ).cleanupCompletedAt,
  ).not.toBeNull();
  expect(await readdir(directory)).not.toContain(key + ".wav");
});
it("serializes deletion behind an in-flight owner write then cascades its result", async () => {
  const a = await login("delete-inflight");
  const { AccountService } = await import("../src/account/account.service");
  let release!: () => void;
  let entered!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const locked = new Promise<void>((resolve) => {
    entered = resolve;
  });
  const work = db.prisma.$transaction(async (tx) => {
    await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${a.user.id} FOR UPDATE`;
    entered();
    await gate;
    await tx.learningItem.create({
      data: {
        userId: a.user.id,
        text: "in flight",
        normalized: "in flight",
        sourceLanguage: "en",
        targetLanguage: "zh",
      },
    });
  });
  await locked;
  const deletion = app.get(AccountService).remove(a.user.id);
  release();
  await work;
  expect((await deletion).deleted).toBe(true);
  expect(
    await db.prisma.learningItem.count({ where: { userId: a.user.id } }),
  ).toBe(0);
  // A late result can never restore a deleted owner via the foreign key.
  await expect(
    db.prisma.learningItem.create({
      data: {
        userId: a.user.id,
        text: "late",
        normalized: "late",
        sourceLanguage: "en",
        targetLanguage: "zh",
      },
    }),
  ).rejects.toThrow();
});
it("cannot recreate an account from an authenticated request paused before owner lookup", async () => {
  const a = await login("delete-authenticated-inflight");
  const { LibraryService } = await import("../src/library/library.service");
  const { AccountService } = await import("../src/account/account.service");
  const library = app.get(LibraryService);
  const original = library.actor.bind(library);
  let release!: () => void;
  let entered!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const started = new Promise<void>((resolve) => {
    entered = resolve;
  });
  const actor = vi
    .spyOn(library, "actor")
    .mockImplementationOnce(async (identity) => {
      entered();
      await gate;
      return original(identity);
    });
  try {
    const pending = a.client.get("/v1/settings").then((response) => response);
    await started;
    await app.get(AccountService).remove(a.user.id);
    release();
    expect([401, 404]).toContain((await pending).status);
    expect(
      await db.prisma.user.count({
        where: { issuer: a.user.issuer, subject: a.user.subject },
      }),
    ).toBe(0);
  } finally {
    release();
    actor.mockRestore();
  }
});
