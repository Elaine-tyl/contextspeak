import type { Prisma, PrismaClient } from "@prisma/client";
import type {
  GameTrack,
  SpeakingAssessment,
  GameReceipt,
  PracticeTask,
  PracticeSubmission,
  DictationFeedback,
} from "@contextspeak/contracts";
import { challenges } from "../games/catalog";
import {
  recommendPractice,
  practiceExerciseKey,
  type PerformanceSample,
} from "./adaptation";
export class AdaptiveService {
  constructor(private prisma: PrismaClient) {}
  async recommend(userId: string, track: GameTrack) {
    const recent = { gte: new Date(Date.now() - 30 * 86400000) };
    const orderBy = [{ createdAt: "desc" as const }, { id: "desc" as const }];
    const gameChecks: Prisma.GameAttemptWhereInput[] =
      track === "speaking"
        ? [
            { feedback: { path: ["speaking", "rubricVersion"], equals: 2 } },
            {
              feedback: { path: ["speaking", "audioQuality"], equals: "clear" },
            },
          ]
        : [];
    const practiceChecks: Prisma.PracticeAttemptWhereInput[] =
      track === "speaking"
        ? [
            {
              task: {
                path: ["speakingAssessment", "rubricVersion"],
                equals: 2,
              },
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
    const readGames = (independent: boolean) =>
      this.prisma.gameAttempt.findMany({
        where: {
          userId,
          createdAt: recent,
          score: { not: null },
          roundId: {
            in: challenges
              .filter((round) => round.track === track)
              .map((round) => round.id),
          },
          AND: [
            ...gameChecks,
            ...(independent
              ? [{ feedback: { path: ["independent"], equals: true } }]
              : []),
          ],
        },
        orderBy,
        take: 30,
      });
    const readPractice = (independent: boolean) =>
      this.prisma.practiceAttempt.findMany({
        where: {
          userId,
          createdAt: recent,
          payload: { path: ["mode"], equals: track },
          AND: [
            ...practiceChecks,
            ...(independent
              ? [{ task: { path: ["independent"], equals: true } }]
              : []),
          ],
        },
        orderBy,
        take: 30,
      });
    // Select valid recent outcomes and independent evidence separately: repeats/noise cannot evict qualifications.
    const [recentGames, independentGames, recentPractice, independentPractice] =
      await Promise.all([
        readGames(false),
        readGames(true),
        readPractice(false),
        readPractice(true),
      ]);
    const games = [
      ...new Map(
        [...recentGames, ...independentGames].map((row) => [row.id, row]),
      ).values(),
    ];
    const practice = [
      ...new Map(
        [...recentPractice, ...independentPractice].map((row) => [row.id, row]),
      ).values(),
    ];
    const samples: (PerformanceSample & { at: number })[] = [];
    const usable = (assessment?: SpeakingAssessment) =>
      assessment?.rubricVersion === 2 &&
      assessment.audioQuality === "clear" &&
      assessment.overall !== null;
    for (const row of games) {
      const round = challenges.find((value) => value.id === row.roundId)!;
      const feedback = row.feedback as GameReceipt["feedback"];
      if (track === "speaking" && !usable(feedback.speaking)) continue;
      samples.push({
        exercise: row.roundId,
        level: round.level,
        score: row.score,
        independent: feedback.independent === true,
        scores: feedback.speaking?.scores,
        at: row.createdAt.getTime(),
      });
    }
    for (const row of practice) {
      const task = row.task as unknown as Partial<PracticeTask> & {
        independent?: boolean;
        exerciseKey?: string;
        feedback?: DictationFeedback;
        speakingAssessment?: SpeakingAssessment;
      };
      const payload = row.payload as unknown as Partial<PracticeSubmission>;
      if (!task.expression) continue;
      if (
        track === "speaking" ? !usable(task.speakingAssessment) : !task.feedback
      )
        continue;
      samples.push({
        exercise:
          task.exerciseKey ??
          practiceExerciseKey(
            track,
            task.expression,
            payload.scenario,
            task.difficulty ?? 1,
          ),
        level: task.difficulty ?? 1,
        score:
          track === "speaking"
            ? task.speakingAssessment!.overall
            : task.feedback!.score,
        independent: task.independent === true,
        scores: task.speakingAssessment?.scores,
        at: row.createdAt.getTime(),
      });
    }
    return recommendPractice(samples.sort((a, b) => b.at - a.at));
  }
}
