import { afterAll, beforeAll, expect, it, vi } from "vitest";
import { createHash } from "node:crypto";
import { mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { startDatabase } from "../../../tests/helpers/database";
import { SpeechStore } from "../src/speech/speech.store";
import { SpeechService } from "../src/speech/speech.service";
import { FixtureSpeechProvider } from "../src/speech/gemini-speech.provider";
let db: Awaited<ReturnType<typeof startDatabase>>;
let directory: string;
let userId: string;
let otherId: string;
const identity = { issuer: "test", subject: "a" };
const input = { text: "Hello from saved audio.", language: "en" as const };
const key = (text: string) => createHash("sha256").update(text).digest("hex");
beforeAll(async () => {
  db = await startDatabase();
  directory = await mkdtemp(join(tmpdir(), "cs-speech-"));
  userId = (await db.prisma.user.create({ data: identity })).id;
  otherId = (
    await db.prisma.user.create({ data: { issuer: "test", subject: "b" } })
  ).id;
});
afterAll(async () => {
  await db?.close();
  if (directory) await rm(directory, { recursive: true, force: true });
});
it("reuses private validated WAV files after restart without a provider call", async () => {
  const generate = vi.fn(() => new FixtureSpeechProvider().generate());
  const library = { actor: async () => ({ id: userId }) } as any;
  const first = new SpeechService(
    library,
    { cacheKey: "voice-v1", generate },
    new SpeechStore(db.prisma, directory),
  );
  const results = await Promise.all([
    first.speak(identity, input),
    first.speak(identity, input),
  ]);
  expect(generate).toHaveBeenCalledTimes(1);
  expect(results[0].audio).toBe(results[1].audio);
  const unavailable = vi.fn(async () => {
    throw new Error("Offline");
  });
  const restarted = new SpeechService(
    library,
    { cacheKey: "voice-v1", generate: unavailable },
    new SpeechStore(db.prisma, directory),
  );
  expect(await restarted.speak(identity, input)).toMatchObject({
    audio: results[0].audio,
    cached: true,
  });
  expect(unavailable).not.toHaveBeenCalled();
  const offline = new SpeechService(
    library,
    undefined,
    new SpeechStore(db.prisma, directory),
  );
  expect(await offline.speak(identity, input)).toMatchObject({
    audio: results[0].audio,
    cached: true,
  });
  const other = new SpeechService(
    { actor: async () => ({ id: otherId }) } as any,
    { cacheKey: "voice-v1", generate: unavailable },
    new SpeechStore(db.prisma, directory),
  );
  await expect(other.speak(identity, input)).rejects.toBeDefined();
  const changed = new SpeechService(
    library,
    { cacheKey: "voice-v2", generate: unavailable },
    new SpeechStore(db.prisma, directory),
  );
  await expect(changed.speak(identity, input)).rejects.toBeDefined();
  const row = await db.prisma.speechAsset.findFirstOrThrow();
  expect(JSON.stringify(row)).not.toContain(input.text);
  expect(
    (await readFile(join(directory, row.key + ".wav"))).toString("ascii", 0, 4),
  ).toBe("RIFF");
});
it("discards corrupted audio and expires old entries", async () => {
  const store = new SpeechStore(db.prisma, directory);
  const audio = await new FixtureSpeechProvider().generate();
  await store.put(userId, key("corrupt"), audio);
  await writeFile(join(directory, key("corrupt") + ".wav"), "broken");
  expect(await store.get(userId, key("corrupt"))).toBeNull();
  await store.put(userId, key("expired"), audio);
  await db.prisma.speechAsset.update({
    where: { key: key("expired") },
    data: { lastUsedAt: new Date(0) },
  });
  await store.cleanup();
  expect(await store.get(userId, key("expired"))).toBeNull();
});
it("bounds disk usage and removes deleted-account orphans without serving another owner's audio", async () => {
  const audio = await new FixtureSpeechProvider().generate();
  const limitedDir = join(directory, "limited");
  const store = new SpeechStore(db.prisma, limitedDir, {
    totalBytes: 4000,
    userBytes: 1700,
  });
  // A separate DB-backed index uses one root per deployment; remove earlier test assets first.
  await db.prisma.speechAsset.deleteMany();
  await store.put(otherId, key("older"), audio);
  await db.prisma.speechAsset.update({
    where: { key: key("older") },
    data: { lastUsedAt: new Date(Date.now() - 1000) },
  });
  await store.put(otherId, key("newer"), audio);
  expect(await store.get(otherId, key("older"))).toBeNull();
  expect(await store.get(userId, key("newer"))).toBeNull();
  await db.prisma.user.delete({ where: { id: otherId } });
  await store.cleanup();
  expect(await readdir(limitedDir)).toEqual([]);
  await expect(store.put(otherId, key("deleted"), audio)).rejects.toBeDefined();
  expect(await readdir(limitedDir)).toEqual([]);
});

it("enforces the total disk budget across accounts", async () => {
  await db.prisma.speechAsset.deleteMany();
  const root = join(directory, "global-budget");
  const store = new SpeechStore(db.prisma, root, {
    totalBytes: 4000,
    userBytes: 4000,
  });
  const audio = await new FixtureSpeechProvider().generate();
  const users = [];
  for (let index = 0; index < 3; index++)
    users.push(
      await db.prisma.user.create({
        data: { issuer: "test", subject: `budget-${index}` },
      }),
    );
  await store.put(users[0].id, key("global-old"), audio);
  await db.prisma.speechAsset.update({
    where: { key: key("global-old") },
    data: { lastUsedAt: new Date(Date.now() - 1000) },
  });
  await store.put(users[1].id, key("global-middle"), audio);
  await store.put(users[2].id, key("global-new"), audio);
  expect(await store.get(users[0].id, key("global-old"))).toBeNull();
  expect(await db.prisma.speechAsset.count()).toBe(2);
  expect(
    (await readdir(root)).filter((name) => name.endsWith(".wav")),
  ).toHaveLength(2);
});
