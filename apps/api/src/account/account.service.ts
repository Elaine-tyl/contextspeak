import { NotFoundException } from "@nestjs/common";
import { Prisma, type PrismaClient } from "@prisma/client";
import { mkdtemp, open, rm, type FileHandle } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import type { SpeechStore } from "../speech/speech.store";

// JSON payloads currently contain only recording metadata; also protect legacy rows.
const rawMedia = new Set([
  "audio",
  "audioBase64",
  "rawAudio",
  "recording",
  "blobUrl",
]);
const json = (value: unknown) =>
  JSON.stringify(value, (key, value) =>
    rawMedia.has(key) ? undefined : value,
  );
export class AccountService {
  private timer?: ReturnType<typeof setInterval>;
  private cleanupRunning?: Promise<void>;
  constructor(
    private readonly db: PrismaClient,
    private readonly speech?: Pick<SpeechStore, "cleanup">,
    private readonly forget?: (userId: string) => void,
  ) {}
  async onModuleInit() {
    await this.retryCleanup().catch(() => {});
    this.timer = setInterval(() => {
      void this.retryCleanup().catch(() => {});
    }, 60000);
    this.timer.unref();
  }
  onModuleDestroy() {
    clearInterval(this.timer);
  }

  async withExport<T>(
    userId: string,
    consume: (file: FileHandle) => Promise<T>,
  ): Promise<T> {
    const directory = await mkdtemp(join(tmpdir(), "contextspeak-export-"));
    const path = join(directory, "contextspeak-data.json");
    try {
      const file = await open(path, "wx+", 0o600);
      try {
        // Keep only an open descriptor: process crashes cannot leave private exports behind.
        await rm(directory, { recursive: true, force: true });
        await this.db.$transaction(
          async (tx) => {
            const account = await tx.user.findUnique({
              where: { id: userId },
              select: {
                id: true,
                issuer: true,
                subject: true,
                collectionEnabled: true,
                excludedOrigins: true,
                consentVersion: true,
                translationPreferences: true,
                learningProfile: true,
              },
            });
            if (!account)
              throw new NotFoundException("Account no longer exists.");
            await file.writeFile(
              '{"schemaVersion":1,"exportedAt":' +
                json(new Date()) +
                ',"account":' +
                json(account),
            );
            const where = (key: string, after?: string) => ({
              userId,
              ...(after ? { [key]: { gt: after } } : {}),
            });
            const rows = async (
              name: string,
              key: string,
              load: (after?: string) => Promise<unknown[]>,
            ) => {
              await file.writeFile(',"' + name + '":[');
              let after: string | undefined;
              let comma = "";
              for (;;) {
                const page = await load(after);
                for (const row of page) {
                  await file.writeFile(comma + json(row));
                  comma = ",";
                }
                if (page.length < 100) break;
                after = (page[page.length - 1] as Record<string, string>)[key];
              }
              await file.writeFile("]");
            };
            await rows("learningItems", "id", (after) =>
              tx.learningItem.findMany({
                where: where("id", after),
                orderBy: { id: "asc" },
                take: 100,
              }),
            );
            await rows("lookups", "id", (after) =>
              tx.lookupEvent.findMany({
                where: where("id", after),
                orderBy: { id: "asc" },
                take: 100,
                select: {
                  id: true,
                  eventId: true,
                  candidateId: true,
                  text: true,
                  context: true,
                  sourceUrl: true,
                  sourceLanguage: true,
                  targetLanguage: true,
                  translationStatus: true,
                  detectedSourceLanguage: true,
                  meaning: true,
                  learningDetails: true,
                  provider: true,
                  errorCode: true,
                  createdAt: true,
                },
              }),
            );
            await rows("reviewCards", "itemId", (after) =>
              tx.reviewCard.findMany({
                where: where("itemId", after),
                orderBy: { itemId: "asc" },
                take: 100,
              }),
            );
            await rows("reviewEvents", "id", (after) =>
              tx.reviewEvent.findMany({
                where: where("id", after),
                orderBy: { id: "asc" },
                take: 100,
              }),
            );
            await rows("practiceAttempts", "id", (after) =>
              tx.practiceAttempt.findMany({
                where: where("id", after),
                orderBy: { id: "asc" },
                take: 100,
                select: {
                  id: true,
                  eventId: true,
                  lookupId: true,
                  payload: true,
                  task: true,
                  createdAt: true,
                },
              }),
            );
            await rows("gameAttempts", "id", (after) =>
              tx.gameAttempt.findMany({
                where: where("id", after),
                orderBy: { id: "asc" },
                take: 100,
                select: {
                  id: true,
                  eventId: true,
                  levelSessionId: true,
                  roundId: true,
                  score: true,
                  payload: true,
                  feedback: true,
                  createdAt: true,
                },
              }),
            );
            await rows("gameLevels", "id", (after) =>
              tx.gameLevelSession.findMany({
                where: where("id", after),
                orderBy: { id: "asc" },
                take: 100,
              }),
            );
            await rows("speechAssets", "key", (after) =>
              tx.speechAsset.findMany({
                where: where("key", after),
                orderBy: { key: "asc" },
                take: 100,
                select: { key: true, bytes: true, lastUsedAt: true },
              }),
            );
            await file.writeFile("}");
          },
          {
            isolationLevel: Prisma.TransactionIsolationLevel.RepeatableRead,
            timeout: 120000,
          },
        );
        return await consume(file);
      } finally {
        await file.close();
      }
    } finally {
      await rm(directory, { recursive: true, force: true });
    }
  }

  async remove(userId: string) {
    await this.db.$transaction(async (tx) => {
      // All learning writes take this same lock before committing provider results.
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${userId} FOR UPDATE`;
      if (!(await tx.user.findUnique({ where: { id: userId } })))
        throw new NotFoundException();
      const hadSpeechAssets = !!(await tx.speechAsset.findFirst({
        where: { userId },
        select: { key: true },
      }));
      await tx.accountDeletion.create({ data: { userId, hadSpeechAssets } });
      await tx.user.delete({ where: { id: userId } });
    });
    // Deletion is committed. Storage/queue outages must never imply that the account remains.
    try {
      this.forget?.(userId);
      await this.retryCleanup();
      const record = await this.db.accountDeletion.findUniqueOrThrow({
        where: { userId },
      });
      return { deleted: true, cleanupPending: !record.cleanupCompletedAt };
    } catch {
      return { deleted: true, cleanupPending: true };
    }
  }

  retryCleanup(): Promise<void> {
    if (this.cleanupRunning) return this.cleanupRunning;
    this.cleanupRunning = this.cleanupBatch().finally(() => {
      this.cleanupRunning = undefined;
    });
    return this.cleanupRunning;
  }
  private async cleanupBatch() {
    const records = await this.db.accountDeletion.findMany({
      where: { cleanupCompletedAt: null },
      orderBy: [
        { lastCleanupAt: { sort: "asc", nulls: "first" } },
        { deletedAt: "asc" },
      ],
      take: 100,
    });
    if (!records.length) return;
    const ids = records.map((record) => record.id);
    await this.db.accountDeletion.updateMany({
      where: { id: { in: ids } },
      data: { cleanupAttempts: { increment: 1 }, lastCleanupAt: new Date() },
    });
    if (this.speech) {
      try {
        await this.speech.cleanup();
      } catch {
        return;
      }
    }
    const completed = records
      .filter((record) => this.speech || !record.hadSpeechAssets)
      .map((record) => record.id);
    await this.db.accountDeletion.updateMany({
      where: { id: { in: completed } },
      data: { cleanupCompletedAt: new Date() },
    });
  }
}
