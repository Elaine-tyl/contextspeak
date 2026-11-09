import { createHash, randomUUID } from "node:crypto";
import {
  mkdir,
  readFile,
  readdir,
  rename,
  rm,
  writeFile,
} from "node:fs/promises";
import { join } from "node:path";
import type { PrismaClient } from "@prisma/client";
import type { SpeechAudio } from "@contextspeak/contracts";
import { validateWav } from "./gemini-speech.provider";
const maxAge = 30 * 86400000;
const digest = (bytes: Buffer) =>
  createHash("sha256").update(bytes).digest("hex");
export class SpeechStore {
  private tail: Promise<unknown> = Promise.resolve();
  constructor(
    private prisma: PrismaClient,
    private directory: string,
    private limits = {
      totalBytes: 512 * 1024 * 1024,
      userBytes: 128 * 1024 * 1024,
    },
  ) {}
  private serial<T>(work: () => Promise<T>): Promise<T> {
    const next = this.tail.then(work, work);
    this.tail = next.catch(() => {});
    return next;
  }
  private file(key: string) {
    if (!/^[a-f0-9]{64}$/.test(key)) throw new Error("Invalid speech key");
    return join(this.directory, key + ".wav");
  }
  async get(userId: string, key: string): Promise<SpeechAudio | null> {
    return this.serial(async () => {
      const row = await this.prisma.speechAsset.findFirst({
        where: { key, userId },
      });
      if (!row) return null;
      try {
        if (row.lastUsedAt.getTime() < Date.now() - maxAge)
          throw new Error("Expired");
        const bytes = await readFile(this.file(key));
        validateWav(bytes);
        if (bytes.length !== row.bytes || digest(bytes) !== row.digest)
          throw new Error("Corrupt speech");
        await this.prisma.speechAsset.updateMany({
          where: { key, userId },
          data: { lastUsedAt: new Date() },
        });
        return { audio: bytes.toString("base64"), mimeType: "audio/wav" };
      } catch {
        await this.remove(key);
        return null;
      }
    });
  }
  async getLatest(userId: string, requestHash: string) {
    const row = await this.prisma.speechAsset.findFirst({
      where: { userId, requestHash },
      orderBy: { lastUsedAt: "desc" },
      select: { key: true },
    });
    return row ? this.get(userId, row.key) : null;
  }
  async put(
    userId: string,
    key: string,
    value: SpeechAudio,
    requestHash = key,
  ) {
    return this.serial(async () => {
      const bytes = Buffer.from(value.audio, "base64");
      validateWav(bytes);
      await mkdir(this.directory, { recursive: true, mode: 0o700 });
      const temporary = join(this.directory, randomUUID() + ".tmp");
      try {
        await writeFile(temporary, bytes, { mode: 0o600, flag: "wx" });
        await rename(temporary, this.file(key));
        await this.prisma.$transaction(async (tx) => {
          await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${userId} FOR UPDATE`;
          if (!(await tx.user.findUnique({ where: { id: userId } })))
            throw new Error("Speech owner deleted");
          await tx.speechAsset.upsert({
            where: { key },
            create: {
              key,
              userId,
              requestHash,
              bytes: bytes.length,
              digest: digest(bytes),
            },
            update: {
              bytes: bytes.length,
              digest: digest(bytes),
              lastUsedAt: new Date(),
            },
          });
        });
      } catch (error) {
        await rm(this.file(key), { force: true });
        throw error;
      } finally {
        await rm(temporary, { force: true });
      }
      await this.prune();
    });
  }
  cleanup() {
    return this.serial(() => this.prune());
  }
  private async remove(key: string) {
    await this.prisma.speechAsset.deleteMany({ where: { key } });
    await rm(this.file(key), { force: true });
  }
  private async prune() {
    await mkdir(this.directory, { recursive: true, mode: 0o700 });
    const rows = await this.prisma.speechAsset.findMany({
      orderBy: { lastUsedAt: "desc" },
    });
    const sizes = new Map<string, number>();
    const keep = new Set<string>();
    let total = 0;
    for (const row of rows) {
      const owned = sizes.get(row.userId) ?? 0;
      if (
        row.lastUsedAt.getTime() < Date.now() - maxAge ||
        total + row.bytes > this.limits.totalBytes ||
        owned + row.bytes > this.limits.userBytes
      ) {
        await this.remove(row.key);
        continue;
      }
      total += row.bytes;
      sizes.set(row.userId, owned + row.bytes);
      keep.add(row.key + ".wav");
    }
    // User deletion cascades the index; remove its orphaned private files as well.
    for (const name of await readdir(this.directory))
      if (
        (/^[a-f0-9]{64}\.wav$/.test(name) && !keep.has(name)) ||
        /^[a-f0-9-]{36}\.tmp$/.test(name)
      )
        await rm(join(this.directory, name), { force: true });
  }
}
