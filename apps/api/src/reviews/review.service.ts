import { ConflictException, NotFoundException } from "@nestjs/common";
import {
  Prisma,
  type PrismaClient,
  type ReviewCard,
  type ReviewEvent,
} from "@prisma/client";
import { DefaultScheduler, Rating, type DefaultSchedulerCard } from "ts-fsrs";
import type { Identity, ReviewSubmission } from "@contextspeak/contracts";
import { LibraryService } from "../library/library.service";
const ratings = {
  again: Rating.Again,
  hard: Rating.Hard,
  good: Rating.Good,
  easy: Rating.Easy,
};
function json(value: unknown): Prisma.InputJsonValue {
  return JSON.parse(JSON.stringify(value));
}
function receipt(card: ReviewCard | ReviewEvent) {
  return { id: card.itemId, version: card.version, dueAt: card.dueAt };
}
function restoreSchedule(value: Prisma.JsonValue): DefaultSchedulerCard {
  const card = value as unknown as DefaultSchedulerCard;
  return {
    ...card,
    dueAt: new Date(card.dueAt),
    lastReviewAt: card.lastReviewAt ? new Date(card.lastReviewAt) : null,
  };
}
export class ReviewService {
  private scheduler = DefaultScheduler({
    version: "FSRS-7",
    desiredRetention: 0.9,
    enableFuzz: false,
  });
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
  ) {}
  async enroll(identity: Identity, itemId: string) {
    const user = await this.library.actor(identity);
    const scheduler = await this.scheduler;
    return this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${user.id} FOR UPDATE`;
      const item = await tx.learningItem.findFirst({
        where: { id: itemId, userId: user.id },
      });
      if (!item) throw new NotFoundException();
      const existing = await tx.reviewCard.findUnique({ where: { itemId } });
      // Resume without resetting an existing schedule or counting another review.
      if (existing)
        return receipt(
          await tx.reviewCard.update({
            where: { itemId },
            data: { active: true },
          }),
        );
      const answer = await tx.lookupEvent.findFirst({
        where: {
          candidateId: itemId,
          userId: user.id,
          translationStatus: "succeeded",
          meaning: { not: null },
        },
        orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      });
      if (!answer?.meaning?.trim())
        throw new ConflictException(
          "Translate this item successfully before starting reviews.",
        );
      const now = new Date();
      const schedule = scheduler.newCard({ cardId: itemId, now });
      const card = await tx.reviewCard.create({
        data: {
          itemId,
          userId: user.id,
          prompt: answer.text,
          answer: answer.meaning,
          context: answer.context,
          dueAt: now,
          schedule: json(schedule),
        },
      });
      return receipt(card);
    });
  }
  async queue(identity: Identity) {
    const user = await this.library.actor(identity);
    const now = new Date();
    const owned = { userId: user.id, active: true };
    const due = { ...owned, dueAt: { lte: now } };
    const [cards, dueCount, totalCount, next] = await this.prisma.$transaction([
      this.prisma.reviewCard.findMany({
        where: due,
        orderBy: [{ dueAt: "asc" }, { itemId: "asc" }],
        take: 20,
        include: {
          item: { select: { sourceLanguage: true, targetLanguage: true } },
        },
      }),
      this.prisma.reviewCard.count({ where: due }),
      this.prisma.reviewCard.count({ where: owned }),
      this.prisma.reviewCard.findFirst({
        where: { ...owned, dueAt: { gt: now } },
        orderBy: { dueAt: "asc" },
        select: { dueAt: true },
      }),
    ]);
    return {
      items: cards.map((card) => ({
        id: card.itemId,
        version: card.version,
        prompt: card.prompt,
        answer: card.answer,
        context: card.context,
        dueAt: card.dueAt,
        ...card.item,
      })),
      dueCount,
      totalCount,
      nextDueAt: next?.dueAt ?? null,
    };
  }
  async rate(identity: Identity, itemId: string, input: ReviewSubmission) {
    const user = await this.library.actor(identity);
    const scheduler = await this.scheduler;
    return this.prisma.$transaction(async (tx) => {
      // Serialize one learner's rating stream, including event IDs across cards.
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${user.id} FOR UPDATE`;
      const previous = await tx.reviewEvent.findUnique({
        where: { userId_eventId: { userId: user.id, eventId: input.eventId } },
      });
      if (previous) {
        if (
          previous.itemId !== itemId ||
          previous.rating !== input.rating ||
          previous.version !== input.version + 1
        )
          throw new ConflictException(
            "Review event already used with different content.",
          );
        return receipt(previous);
      }
      const card = await tx.reviewCard.findFirst({
        where: { itemId, userId: user.id },
      });
      if (!card) throw new NotFoundException();
      const now = new Date();
      if (!card.active || card.version !== input.version || card.dueAt > now)
        throw new ConflictException(
          "This review changed or is not due. Refresh the queue.",
        );
      const scheduled = scheduler.review({
        card: restoreSchedule(card.schedule),
        grade: ratings[input.rating],
        now,
      });
      const updated = await tx.reviewCard.update({
        where: { itemId },
        data: {
          schedule: json(scheduled.card),
          dueAt: scheduled.card.dueAt,
          version: { increment: 1 },
        },
      });
      await tx.reviewEvent.create({
        data: {
          userId: user.id,
          eventId: input.eventId,
          itemId,
          rating: input.rating,
          version: updated.version,
          dueAt: updated.dueAt,
          log: json(scheduled.revlog),
        },
      });
      return receipt(updated);
    });
  }
  async pause(identity: Identity, itemId: string) {
    const user = await this.library.actor(identity);
    return this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${user.id} FOR UPDATE`;
      const updated = await tx.reviewCard.updateMany({
        where: { itemId, userId: user.id },
        data: { active: false },
      });
      if (!updated.count) throw new NotFoundException();
      return { paused: true };
    });
  }
}
