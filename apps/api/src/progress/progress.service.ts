import { Prisma, type PrismaClient } from "@prisma/client";
import type {
  Identity,
  GameTrack,
  SpeakingAssessment,
  LearningProgress,
  GameReceipt,
  PracticeTask,
  DictationFeedback,
} from "@contextspeak/contracts";
import { gameModes } from "@contextspeak/contracts";
import { LibraryService } from "../library/library.service";
import { GameLevelsService } from "../games/levels.service";
import { challenges } from "../games/catalog";
import { practiceExerciseKey } from "../practice/adaptation";
import { summarize, type ScoredSample } from "./summary";
export class ProgressService {
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
  ) {}
  private async skill(userId: string, track: GameTrack, since: Date) {
    const ids = challenges
      .filter((round) => round.track === track)
      .map((round) => round.id);
    const speaking = track === "speaking";
    const quality: Prisma.GameAttemptWhereInput[] = speaking
      ? [
          { feedback: { path: ["speaking", "rubricVersion"], equals: 2 } },
          { feedback: { path: ["speaking", "audioQuality"], equals: "clear" } },
        ]
      : [];
    const practiceQuality: Prisma.PracticeAttemptWhereInput[] = speaking
      ? [
          {
            task: { path: ["speakingAssessment", "rubricVersion"], equals: 2 },
          },
          {
            task: {
              path: ["speakingAssessment", "audioQuality"],
              equals: "clear",
            },
          },
          { task: { path: ["speakingAssessment", "overall"], gte: 0 } },
        ]
      : [{ task: { path: ["feedback", "score"], gte: 0 } }];
    const [games, practice, uncertainGames, uncertainPractice] =
      await Promise.all([
        this.prisma.gameAttempt.findMany({
          where: {
            userId,
            createdAt: { gte: since },
            roundId: { in: ids },
            score: { not: null },
            AND: quality,
          },
          orderBy: [{ createdAt: "desc" }, { id: "desc" }],
          take: 100,
        }),
        this.prisma.practiceAttempt.findMany({
          where: {
            userId,
            createdAt: { gte: since },
            payload: { path: ["mode"], equals: track },
            AND: practiceQuality,
          },
          orderBy: [{ createdAt: "desc" }, { id: "desc" }],
          take: 100,
        }),
        speaking
          ? this.prisma.gameAttempt.count({
              where: {
                userId,
                createdAt: { gte: since },
                roundId: { in: ids },
                score: null,
                feedback: { path: ["speaking", "rubricVersion"], equals: 2 },
              },
            })
          : Promise.resolve(0),
        speaking
          ? this.prisma.practiceAttempt.count({
              where: {
                userId,
                createdAt: { gte: since },
                AND: [
                  {
                    task: {
                      path: ["speakingAssessment", "rubricVersion"],
                      equals: 2,
                    },
                  },
                  {
                    task: {
                      path: ["speakingAssessment", "overall"],
                      equals: Prisma.JsonNull,
                    },
                  },
                ],
              },
            })
          : Promise.resolve(0),
      ]);
    const samples: ScoredSample[] = games.map((row) => {
      const feedback = row.feedback as GameReceipt["feedback"];
      return {
        exercise: row.roundId,
        score: row.score!,
        independent: feedback.independent === true,
        scores: feedback.speaking?.scores,
        at: row.createdAt.getTime(),
      };
    });
    for (const row of practice) {
      const task = row.task as unknown as Partial<PracticeTask> & {
        exerciseKey?: string;
        independent?: boolean;
        speakingAssessment?: SpeakingAssessment;
        feedback?: DictationFeedback;
      };
      const payload = row.payload as { scenario?: string };
      samples.push({
        exercise:
          task.exerciseKey ??
          practiceExerciseKey(
            track,
            task.expression ?? row.lookupId,
            payload.scenario,
            task.difficulty ?? 1,
          ),
        score: speaking
          ? task.speakingAssessment!.overall!
          : task.feedback!.score,
        independent: task.independent === true,
        scores: task.speakingAssessment?.scores,
        at: row.createdAt.getTime(),
      });
    }
    return summarize(samples, uncertainGames + uncertainPractice);
  }
  async get(identity: Identity): Promise<LearningProgress> {
    const user = await this.library.actor(identity);
    const now = new Date();
    const since = new Date(now.getTime() - 30 * 86400000);
    const [
      saved,
      active,
      paused,
      due,
      reviews,
      ratings,
      difficult,
      listening,
      speaking,
      modes,
    ] = await Promise.all([
      this.prisma.learningItem.count({ where: { userId: user.id } }),
      this.prisma.reviewCard.count({
        where: { userId: user.id, active: true },
      }),
      this.prisma.reviewCard.count({
        where: { userId: user.id, active: false },
      }),
      this.prisma.reviewCard.count({
        where: { userId: user.id, active: true, dueAt: { lte: now } },
      }),
      this.prisma.reviewEvent.count({ where: { userId: user.id } }),
      this.prisma.reviewEvent.groupBy({
        by: ["rating"],
        where: { userId: user.id, createdAt: { gte: since } },
        _count: { _all: true },
      }),
      this.prisma.reviewEvent.groupBy({
        by: ["itemId"],
        where: {
          userId: user.id,
          createdAt: { gte: since },
          rating: { in: ["again", "hard"] },
        },
        _count: { _all: true },
        orderBy: { _count: { itemId: "desc" } },
        take: 5,
      }),
      this.skill(user.id, "listening", since),
      this.skill(user.id, "speaking", since),
      new GameLevelsService(this.prisma, this.library).progress(user.id),
    ]);
    const words = await this.prisma.reviewCard.findMany({
      where: {
        userId: user.id,
        itemId: { in: difficult.map((value) => value.itemId) },
      },
      select: { itemId: true, prompt: true },
    });
    const recentRatings = { again: 0, hard: 0, good: 0, easy: 0 };
    for (const value of ratings)
      if (value.rating in recentRatings)
        recentRatings[value.rating as keyof typeof recentRatings] =
          value._count._all;
    return {
      updatedAt: now.toISOString(),
      since: since.toISOString(),
      sampleLimit: 100,
      vocabulary: {
        saved,
        active,
        paused,
        due,
        reviews,
        recentRatings,
        difficult: difficult.flatMap((value) => {
          const word = words.find((word) => word.itemId === value.itemId);
          return word
            ? [{ id: word.itemId, word: word.prompt, count: value._count._all }]
            : [];
        }),
      },
      listening,
      speaking,
      games: gameModes.map((mode) => ({
        kind: mode.kind,
        label: mode.label,
        track: mode.track,
        level: modes[mode.kind].level,
      })),
    };
  }
}
