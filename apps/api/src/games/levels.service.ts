import {
  BadRequestException,
  ConflictException,
  NotFoundException,
} from "@nestjs/common";
import type { Prisma, PrismaClient } from "@prisma/client";
import {
  gameModes,
  type GameKind,
  type GameLevelStart,
  type GameLevelRun,
  type GameProgress,
  type Identity,
} from "@contextspeak/contracts";
import { LibraryService } from "../library/library.service";
import { challenges, gameProgress, publicRound, roundTopic } from "./catalog";
export class GameLevelsService {
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
  ) {}
  async progress(
    userId: string,
    db: Prisma.TransactionClient = this.prisma,
    legacy?: GameProgress,
  ) {
    const old =
      legacy ??
      gameProgress(
        (
          await db.gameAttempt.groupBy({
            by: ["roundId"],
            where: { userId, levelSessionId: null, score: { gte: 70 } },
          })
        ).map((row) => row.roundId),
      );
    const passed = await db.gameLevelSession.groupBy({
      by: ["kind", "level"],
      where: { userId, completedAt: { not: null }, score: { gte: 70 } },
    });
    return Object.fromEntries(
      gameModes.map((mode) => {
        let level = old[mode.track].level;
        const completed = new Set(
          passed
            .filter((value) => value.kind === mode.kind)
            .map((value) => value.level),
        );
        while (level < 10 && completed.has(level)) level++;
        return [mode.kind, { level }];
      }),
    ) as Record<GameKind, { level: number }>;
  }
  async start(identity: Identity, input: GameLevelStart) {
    const user = await this.library.actor(identity);
    const id = await this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${user.id} FOR UPDATE`;
      if (!(await tx.user.findUnique({ where: { id: user.id } })))
        throw new NotFoundException();
      const existing = await tx.gameLevelSession.findUnique({
        where: { userId_eventId: { userId: user.id, eventId: input.eventId } },
      });
      if (existing) {
        if (
          existing.kind !== input.kind ||
          existing.level !== input.level ||
          existing.topic !== (input.topic ?? null) ||
          existing.source !== (input.source ?? null)
        )
          throw new ConflictException(
            "This level start already has different settings.",
          );
        return existing.id;
      }
      const progress = await this.progress(user.id, tx);
      if (input.level > progress[input.kind].level)
        throw new ConflictException(
          "Pass the previous level in this game mode first.",
        );
      const rounds = challenges
        .filter(
          (round) =>
            round.kind === input.kind &&
            round.level === input.level &&
            (!input.topic || roundTopic(round) === input.topic) &&
            (!input.source ||
              (round.source?.provider ?? "ContextSpeak") === input.source),
        )
        .slice(0, 3);
      if (!rounds.length)
        throw new BadRequestException(
          "No reviewed questions match this level, theme and source.",
        );
      return (
        await tx.gameLevelSession.create({
          data: {
            userId: user.id,
            eventId: input.eventId,
            kind: input.kind,
            level: input.level,
            topic: input.topic,
            source: input.source,
            roundIds: rounds.map((round) => round.id),
          },
        })
      ).id;
    });
    return this.getForUser(user.id, id);
  }
  async get(identity: Identity, id: string) {
    const user = await this.library.actor(identity);
    return this.getForUser(user.id, id);
  }
  private async getForUser(userId: string, id: string): Promise<GameLevelRun> {
    const run = await this.prisma.gameLevelSession.findFirst({
      where: { id, userId },
      include: { attempts: true },
    });
    if (!run) throw new NotFoundException();
    const passed =
      run.completedAt !== null && run.score !== null && run.score >= 70;
    return {
      id: run.id,
      kind: run.kind as GameKind,
      level: run.level,
      topic: run.topic,
      source: run.source,
      rounds: run.roundIds.map((id) =>
        publicRound(challenges.find((round) => round.id === id)!),
      ),
      receipts: run.roundIds.flatMap((id) =>
        run.attempts
          .filter((attempt) => attempt.roundId === id)
          .map((attempt) => ({
            id: attempt.id,
            roundId: attempt.roundId,
            score: attempt.score,
            feedback:
              attempt.feedback as GameLevelRun["receipts"][number]["feedback"],
            createdAt: attempt.createdAt.toISOString(),
          })),
      ),
      completed: run.completedAt !== null,
      score: run.score,
      passed,
      nextLevel: passed && run.level < 10 ? run.level + 1 : null,
    };
  }
  async assertRound(
    db: Prisma.TransactionClient,
    userId: string,
    id: string,
    roundId: string,
  ) {
    const run = await db.gameLevelSession.findFirst({
      where: { id, userId },
      include: { attempts: { select: { roundId: true } } },
    });
    if (!run) throw new NotFoundException();
    if (run.completedAt || run.roundIds[run.attempts.length] !== roundId)
      throw new ConflictException(
        "Answer the current question once, in order. Retry the level for a new attempt.",
      );
  }
  async finish(db: Prisma.TransactionClient, userId: string, id: string) {
    const run = await db.gameLevelSession.findFirst({
      where: { id, userId },
      include: { attempts: true },
    });
    if (!run || run.completedAt || run.attempts.length !== run.roundIds.length)
      return;
    const scores = run.attempts.map((attempt) => attempt.score);
    // Uncertain audio is not a zero, but cannot produce a reliable level mark.
    const score = scores.every((value): value is number => value !== null)
      ? Math.round(
          scores.reduce((sum, value) => sum + value, 0) / scores.length,
        )
      : null;
    await db.gameLevelSession.update({
      where: { id },
      data: { score, completedAt: new Date() },
    });
  }
}
