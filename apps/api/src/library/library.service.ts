import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  NotFoundException,
  ServiceUnavailableException,
} from "@nestjs/common";
import { Prisma, type PrismaClient } from "@prisma/client";
import { createHash } from "node:crypto";
import type {
  Identity,
  LookupInput,
  CollectionSettings,
  TranslationInput,
} from "@contextspeak/contracts";
import {
  FixtureTranslationError,
  type TranslationProvider,
} from "./translation.provider";
export class LibraryService {
  private timer?: NodeJS.Timeout;
  private running?: Promise<void>;
  constructor(
    readonly prisma: PrismaClient,
    private provider: TranslationProvider,
  ) {}
  async actor(identity: Identity) {
    const where = { issuer_subject: identity };
    try {
      return await this.prisma.user.upsert({
        where,
        update: {},
        create: identity,
      });
    } catch (e) {
      if (
        e instanceof Prisma.PrismaClientKnownRequestError &&
        e.code === "P2002"
      )
        return this.prisma.user.findUniqueOrThrow({ where });
      throw e;
    }
  }
  async settings(identity: Identity) {
    const u = await this.actor(identity);
    return { enabled: u.collectionEnabled, excludedOrigins: u.excludedOrigins };
  }
  async setSettings(identity: Identity, value: CollectionSettings) {
    const u = await this.actor(identity);
    await this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${u.id} FOR UPDATE`;
      await tx.user.update({
        where: { id: u.id },
        data: {
          collectionEnabled: value.enabled,
          excludedOrigins: value.excludedOrigins,
          consentVersion: { increment: 1 },
        },
      });
    });
    return value;
  }
  async capture(identity: Identity, input: LookupInput) {
    const u = await this.actor(identity);
    const payloadHash = createHash("sha256")
      .update(JSON.stringify(input))
      .digest("hex");
    const row = await this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${u.id} FOR UPDATE`;
      const current = await tx.user.findUniqueOrThrow({ where: { id: u.id } });
      if (
        !current.collectionEnabled ||
        (input.sourceUrl &&
          current.excludedOrigins.includes(new URL(input.sourceUrl).origin))
      )
        throw new ForbiddenException("Collection is disabled for this source");
      const old = await tx.lookupEvent.findUnique({
        where: { userId_eventId: { userId: u.id, eventId: input.eventId } },
      });
      if (old) {
        if (old.payloadHash !== payloadHash)
          throw new ConflictException(
            "Event ID already used with different content",
          );
        return old;
      }
      const normalized = input.text.toLowerCase().replace(/\s+/g, " ");
      const item = await tx.learningItem.upsert({
        where: {
          userId_normalized_sourceLanguage_targetLanguage: {
            userId: u.id,
            normalized,
            sourceLanguage: input.sourceLanguage,
            targetLanguage: input.targetLanguage,
          },
        },
        update: {},
        create: {
          userId: u.id,
          text: input.text,
          normalized,
          sourceLanguage: input.sourceLanguage,
          targetLanguage: input.targetLanguage,
        },
      });
      return tx.lookupEvent.create({
        data: { ...input, userId: u.id, payloadHash, candidateId: item.id },
      });
    });
    return {
      lookupId: row.id,
      candidateId: row.candidateId,
      translationStatus: row.translationStatus,
    };
  }
  async list(identity: Identity, limit = 20, cursor?: string) {
    const u = await this.actor(identity);
    if (!Number.isInteger(limit) || limit < 1 || limit > 100)
      throw new BadRequestException("Limit must be 1–100");
    if (
      cursor &&
      !(await this.prisma.lookupEvent.findFirst({
        where: { id: cursor, userId: u.id },
      }))
    )
      throw new BadRequestException("Invalid cursor");
    const rows = await this.prisma.lookupEvent.findMany({
      where: { userId: u.id },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: limit + 1,
      ...(cursor ? { cursor: { id: cursor }, skip: 1 } : {}),
    });
    const items = rows
      .slice(0, limit)
      .map(({ payloadHash, userId, ...record }) => record);
    return { items, nextCursor: rows.length > limit ? items.at(-1)!.id : null };
  }
  async items(identity: Identity) {
    const u = await this.actor(identity);
    const items = await this.prisma.learningItem.findMany({
      where: { userId: u.id },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: 100,
      include: { _count: { select: { lookups: true } } },
    });
    return {
      items: items.map(({ userId, _count, ...item }) => ({
        ...item,
        encounterCount: _count.lookups,
      })),
      nextCursor: null,
    };
  }
  async translationResult(identity: Identity, id: string) {
    const user = await this.actor(identity);
    const record = await this.prisma.lookupEvent.findFirst({
      where: { id, userId: user.id },
      select: {
        id: true,
        translationStatus: true,
        meaning: true,
        provider: true,
        errorCode: true,
      },
    });
    if (!record) throw new NotFoundException();
    return {
      lookupId: record.id,
      translationStatus: record.translationStatus,
      meaning: record.meaning,
      provider: record.provider,
      errorCode: record.errorCode,
    };
  }
  async retry(identity: Identity, id: string) {
    const u = await this.actor(identity);
    const changed = await this.prisma.lookupEvent.updateMany({
      where: { id, userId: u.id, translationStatus: "failed" },
      data: { translationStatus: "pending", errorCode: null },
    });
    if (
      !changed.count &&
      !(await this.prisma.lookupEvent.findFirst({
        where: { id, userId: u.id },
      }))
    )
      throw new NotFoundException();
    return { status: "pending" };
  }
  async translate(input: TranslationInput) {
    try {
      return await this.provider.translate(input);
    } catch (e) {
      if (e instanceof FixtureTranslationError)
        throw new BadRequestException(e.code);
      throw new ServiceUnavailableException("Translation unavailable");
    }
  }
  start() {
    this.timer = setInterval(() => {
      void this.drain().catch(() => {
        /* Pending rows survive outages and are retried by the next poll. */
      });
    }, 100);
    this.timer.unref();
  }
  async drain() {
    if (this.running) return this.running;
    this.running = this.processPending().finally(() => {
      this.running = undefined;
    });
    return this.running;
  }
  private async processPending() {
    const rows = await this.prisma.lookupEvent.findMany({
      where: { translationStatus: "pending" },
      take: 4,
      orderBy: { createdAt: "asc" },
    });
    await Promise.all(
      rows.map(async (row) => {
        try {
          const result = await this.provider.translate({
            text: row.text,
            context: row.context ?? undefined,
            sourceUrl: row.sourceUrl ?? undefined,
            sourceLanguage: "en",
            targetLanguage: "en",
          });
          await this.prisma.lookupEvent.updateMany({
            where: { id: row.id, translationStatus: "pending" },
            data: {
              ...result,
              translationStatus: "succeeded",
              errorCode: null,
            },
          });
        } catch (error) {
          await this.prisma.lookupEvent.updateMany({
            where: { id: row.id, translationStatus: "pending" },
            data: {
              translationStatus: "failed",
              errorCode:
                error instanceof FixtureTranslationError
                  ? error.code
                  : "PROVIDER_UNAVAILABLE",
            },
          });
        }
      }),
    );
  }
  async onModuleDestroy() {
    clearInterval(this.timer);
    await this.running;
  }
}
