import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  NotFoundException,
  ServiceUnavailableException,
  UnauthorizedException,
} from "@nestjs/common";
import { Prisma, type PrismaClient, type LookupEvent } from "@prisma/client";
import { createHash } from "node:crypto";
import {
  defaultTranslationPreferences,
  translationPreferencesSchema,
} from "@contextspeak/contracts";
import type {
  TranslationPreferences,
  Identity,
  LookupInput,
  CollectionSettings,
  TranslationInput,
} from "@contextspeak/contracts";
import {
  FixtureTranslationError,
  type TranslationProvider,
} from "./translation.provider";
import type { LibraryFilters } from "./library-filters";
import { sessionOwner } from "../auth/identity";
import { TranslationCache } from "./translation-cache";
// Browser responses omit ownership and idempotency metadata.
function publicLookup({ userId, payloadHash, ...record }: LookupEvent) {
  return record;
}
export class LibraryService {
  private translationCache = new TranslationCache({
    retain: (userId, keep) => this.retainForUser(userId, keep),
  });
  private timer?: NodeJS.Timeout;
  private running?: Promise<void>;
  constructor(
    readonly prisma: PrismaClient,
    private provider: TranslationProvider,
  ) {}
  async actor(identity: Identity) {
    const userId = sessionOwner(identity);
    if (userId) {
      const user = await this.prisma.user.findFirst({
        where: {
          id: userId,
          issuer: identity.issuer,
          subject: identity.subject,
        },
      });
      if (!user) throw new UnauthorizedException();
      return user;
    }
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
  forgetUser(userId: string) {
    this.translationCache.forget(userId);
  }
  async retainForUser(userId: string, keep: () => void) {
    await this.prisma.$transaction(async (tx) => {
      const owners = await tx.$queryRaw<
        { id: string }[]
      >`SELECT id FROM "User" WHERE id = ${userId} FOR KEY SHARE`;
      if (owners.length) keep();
    });
  }
  async translationPreferences(
    identity: Identity,
  ): Promise<TranslationPreferences> {
    const user = await this.actor(identity);
    const parsed = translationPreferencesSchema.safeParse(
      user.translationPreferences,
    );
    return parsed.success
      ? parsed.data
      : this.provider.name === "fixture"
        ? { sourceLanguage: "en", targetLanguage: "en", autoTranslate: false }
        : { ...defaultTranslationPreferences };
  }
  async translationOptions() {
    try {
      return {
        provider: this.provider.name ?? "custom",
        maxSelectionCharacters: this.provider.maxSelectionCharacters ?? 2000,
        languages: (await this.provider.languages?.()) ?? [
          { code: "en", name: "English", targets: ["en"] },
        ],
      };
    } catch (error) {
      throw new ServiceUnavailableException(
        error instanceof FixtureTranslationError
          ? error.code
          : "PROVIDER_UNAVAILABLE",
      );
    }
  }
  async setTranslationPreferences(
    identity: Identity,
    value: TranslationPreferences,
  ) {
    const { languages } = await this.translationOptions();
    const source = languages.find(
      (language) => language.code === value.sourceLanguage,
    );
    if (
      !languages.some((language) => language.code === value.targetLanguage) ||
      (value.sourceLanguage !== "auto" &&
        (!source ||
          (value.sourceLanguage !== value.targetLanguage &&
            !source.targets.includes(value.targetLanguage))))
    )
      throw new BadRequestException("UNSUPPORTED_LANGUAGE");
    const user = await this.actor(identity);
    await this.prisma.user.update({
      where: { id: user.id },
      data: { translationPreferences: value },
    });
    return value;
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
  async list(
    identity: Identity,
    limit = 20,
    cursor?: string,
    filters: LibraryFilters = {},
  ) {
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
      where: {
        userId: u.id,
        ...(filters.targetLanguage
          ? { targetLanguage: filters.targetLanguage }
          : {}),
        AND: [
          ...(filters.q
            ? [
                {
                  OR: ["text", "context", "meaning"].map((field) => ({
                    [field]: {
                      contains: filters.q,
                      mode: "insensitive" as const,
                    },
                  })),
                },
              ]
            : []),
          ...(filters.sourceLanguage
            ? [
                {
                  OR: [
                    { sourceLanguage: filters.sourceLanguage },
                    { detectedSourceLanguage: filters.sourceLanguage },
                  ],
                },
              ]
            : []),
        ],
      },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: limit + 1,
      ...(cursor ? { cursor: { id: cursor }, skip: 1 } : {}),
    });
    const items = rows.slice(0, limit).map(publicLookup);
    return { items, nextCursor: rows.length > limit ? items.at(-1)!.id : null };
  }
  async items(identity: Identity, filters: LibraryFilters = {}) {
    const u = await this.actor(identity);
    const items = await this.prisma.learningItem.findMany({
      where: {
        userId: u.id,
        ...(filters.q
          ? { text: { contains: filters.q, mode: "insensitive" as const } }
          : {}),
        ...(filters.targetLanguage
          ? { targetLanguage: filters.targetLanguage }
          : {}),
        ...(filters.sourceLanguage
          ? {
              OR: [
                { sourceLanguage: filters.sourceLanguage },
                {
                  lookups: {
                    some: { detectedSourceLanguage: filters.sourceLanguage },
                  },
                },
              ],
            }
          : {}),
      },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: 100,
      include: {
        _count: { select: { lookups: true } },
        reviewCard: { select: { active: true, dueAt: true } },
      },
    });
    return {
      items: items.map(({ userId, _count, reviewCard, ...item }) => ({
        ...item,
        encounterCount: _count.lookups,
        reviewDueAt: reviewCard?.active ? reviewCard.dueAt : null,
      })),
      nextCursor: null,
    };
  }
  async itemDetail(identity: Identity, id: string, lookupId?: string) {
    const user = await this.actor(identity);
    const item = await this.prisma.learningItem.findFirst({
      where: { id, userId: user.id },
      include: {
        _count: { select: { lookups: true } },
        reviewCard: { select: { active: true, dueAt: true } },
      },
    });
    if (!item) throw new NotFoundException();
    const lookup = await this.prisma.lookupEvent.findFirst({
      where: {
        candidateId: id,
        userId: user.id,
        ...(lookupId ? { id: lookupId } : {}),
      },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
    });
    if (lookupId && !lookup) throw new NotFoundException();
    const { userId, _count, reviewCard, ...candidate } = item;
    return {
      item: {
        ...candidate,
        encounterCount: _count.lookups,
        reviewDueAt: reviewCard?.active ? reviewCard.dueAt : null,
      },
      lookup: lookup ? publicLookup(lookup) : null,
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
        learningDetails: true,
        provider: true,
        detectedSourceLanguage: true,
        errorCode: true,
      },
    });
    if (!record) throw new NotFoundException();
    return {
      lookupId: record.id,
      translationStatus: record.translationStatus,
      meaning: record.meaning,
      learningDetails: record.learningDetails,
      provider: record.provider,
      detectedSourceLanguage: record.detectedSourceLanguage,
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
  private translateForUser(userId: string, input: TranslationInput) {
    const load = () => this.provider.translate(input);
    return this.provider.name === "gemini"
      ? this.translationCache.translate(userId, input, load)
      : load();
  }
  async translate(identity: Identity, input: TranslationInput) {
    const user = await this.actor(identity);
    try {
      return await this.translateForUser(user.id, input);
    } catch (e) {
      if (e instanceof FixtureTranslationError) {
        if (
          [
            "FIXTURE_NOT_FOUND",
            "UNSUPPORTED_PAIR",
            "UNSUPPORTED_LANGUAGE",
            "AI_SELECTION_TOO_LONG",
          ].includes(e.code)
        )
          throw new BadRequestException(e.code);
        throw new ServiceUnavailableException(e.code);
      }
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
          const result = await this.translateForUser(row.userId, {
            text: row.text,
            context: row.context ?? undefined,
            sourceUrl: row.sourceUrl ?? undefined,
            sourceLanguage: row.sourceLanguage,
            targetLanguage: row.targetLanguage,
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
