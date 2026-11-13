import {
  BadRequestException,
  ConflictException,
  HttpException,
  NotFoundException,
} from "@nestjs/common";
import { createHash } from "node:crypto";
import type { PrismaClient, GameAttempt } from "@prisma/client";
import { gameRequiresAudio } from "@contextspeak/contracts";
import type {
  Identity,
  GameSubmission,
  GameReceipt,
} from "@contextspeak/contracts";
import { GameLevelsService } from "./levels.service";
import { AdaptiveService } from "../practice/adaptive.service";
import { LibraryService } from "../library/library.service";
import { recording } from "../practice/recording";
import { gradeDictation } from "../practice/dictation";
import { gradeQuickFire, gradeSpelling } from "./new-modes";
import { gradeGapFill } from "./gap-fill";
import type { SpeakingProvider } from "../practice/speaking.provider";
import {
  challenges,
  activeChallenges,
  gameProgress,
  publicRound,
  type Challenge,
} from "./catalog";
function receipt(row: GameAttempt): GameReceipt {
  return {
    id: row.id,
    roundId: row.roundId,
    score: row.score,
    feedback: row.feedback as GameReceipt["feedback"],
    createdAt: row.createdAt.toISOString(),
  };
}
export class GamesService {
  private pending = new Map<
    string,
    { hash: string; result: Promise<GameReceipt> }
  >();
  private requests = new Map<string, number[]>();
  private global: number[] = [];
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
    private provider?: SpeakingProvider,
    private adaptive = new AdaptiveService(prisma),
    readonly levels = new GameLevelsService(prisma, library),
  ) {}
  async progress(userId: string) {
    const passed = await this.prisma.gameAttempt.groupBy({
      by: ["roundId"],
      where: { userId, levelSessionId: null, score: { gte: 70 } },
    });
    return gameProgress(passed.map((row) => row.roundId));
  }
  async catalog(identity: Identity) {
    const user = await this.library.actor(identity);
    const progress = await this.progress(user.id);
    const [listening, speaking, vocabulary, reading] = await Promise.all([
      this.adaptive.recommend(user.id, "listening", 10),
      this.adaptive.recommend(user.id, "speaking", 10),
      this.adaptive.recommend(user.id, "vocabulary", 10),
      this.adaptive.recommend(user.id, "reading", 10),
    ]);
    return {
      rounds: activeChallenges.map(publicRound),
      progress,
      modeProgress: await this.levels.progress(user.id, this.prisma, progress),
      recommendations: {
        vocabulary,
        reading,
        listening: {
          ...listening,
          level: Math.min(listening.level, progress.listening.level),
        },
        speaking: {
          ...speaking,
          level: Math.min(speaking.level, progress.speaking.level),
        },
      },
    };
  }
  async submit(identity: Identity, input: GameSubmission) {
    const user = await this.library.actor(identity);
    const round = challenges.find((value) => value.id === input.roundId);
    if (!round) throw new NotFoundException();
    const { audio, ...metadata } = input;
    const audioHash =
      audio && input.mimeType
        ? recording({ audio, mimeType: input.mimeType })
        : undefined;
    const hash = createHash("sha256")
      .update(JSON.stringify({ ...metadata, audioHash }))
      .digest("hex");
    const key = `${user.id}:${input.eventId}`;
    const pending = this.pending.get(key);
    if (pending) {
      if (pending.hash !== hash)
        throw new ConflictException(
          "This game event already has another answer.",
        );
      return pending.result;
    }
    const result = this.prepare(user.id, round, input, hash, audioHash);
    this.pending.set(key, { hash, result });
    try {
      return await result;
    } finally {
      this.pending.delete(key);
    }
  }
  private async prepare(
    userId: string,
    round: Challenge,
    input: GameSubmission,
    hash: string,
    audioHash?: string,
  ) {
    const previous = await this.prisma.gameAttempt.findUnique({
      where: { userId_eventId: { userId, eventId: input.eventId } },
    });
    if (previous) {
      if (previous.payloadHash !== hash)
        throw new ConflictException(
          "This game event already has another answer.",
        );
      return receipt(previous);
    }
    if (
      (round.kind === "gap-fill" ||
        round.kind === "scene-words" ||
        round.recall ||
        !gameRequiresAudio(round)) &&
      !input.levelSessionId
    )
      throw new BadRequestException("Start a level in this game mode first.");
    if (input.levelSessionId)
      await this.levels.assertRound(
        this.prisma,
        userId,
        input.levelSessionId,
        round.id,
      );
    const progress = input.levelSessionId ? null : await this.progress(userId);
    if (progress && round.level > (progress[round.track]?.level ?? 1))
      throw new ConflictException(
        "Pass three different challenges at 70 or above to unlock this level.",
      );
    if (gameRequiresAudio(round) && !input.completedPlays)
      throw new BadRequestException("Finish the challenge audio first.");
    if (
      input.gapAnswers &&
      !round.recall &&
      round.kind !== "gap-fill" &&
      round.kind !== "scene-words" &&
      round.kind !== "mini-crossword"
    )
      throw new BadRequestException("This challenge does not have word gaps.");
    const feedback: GameReceipt["feedback"] = {
      explanation: round.explanation,
    };
    let score: number | null;
    if (round.track === "speaking") {
      if (!input.audio || !input.mimeType || !input.durationMs || !audioHash)
        throw new BadRequestException(
          "Record an answer of up to 60 seconds and 2 MB.",
        );
      this.reserve(userId);
      if (!this.provider)
        throw new HttpException("AI feedback is unavailable.", 503);
      try {
        const assessment = await this.provider.assess({
          audio: input.audio,
          mimeType: input.mimeType,
          task: {
            expression: "",
            difficulty: round.level,
            criteria: round.criteria,
            language: "en",
            sourceText: round.audioText,
            goal: `English ${round.kind} game. Assess the spoken answer against this task and solution criteria: ${round.answer}. Reward relevance and the explicit criteria; accept equivalent answers. Do not require reasoning or a long response unless the criteria ask for it. Do not reveal a correct answer in the transcript unless it was spoken.`,
          },
        });
        feedback.speaking = assessment;
        if (assessment.speechDetected && !round.source)
          feedback.correctAnswer = round.answer;
        score =
          assessment.overall === null || !assessment.scores
            ? null
            : Math.round(
                (assessment.overall + assessment.scores.taskResponse) / 2,
              );
        if (round.opponentScore !== undefined)
          feedback.opponentScore = round.opponentScore;
      } catch (error) {
        throw new HttpException(
          "AI feedback not confirmed. Retry this recording later.",
          (error as { status?: number }).status === 429 ? 429 : 503,
        );
      }
    } else if (
      round.kind === "gap-fill" ||
      round.kind === "scene-words" ||
      round.kind === "mini-crossword" ||
      round.recall
    ) {
      if (
        !input.gapAnswers ||
        input.gapAnswers.length !== round.gapAnswers!.length ||
        input.audio ||
        input.response !== undefined ||
        input.choice !== undefined
      )
        throw new BadRequestException("Fill each blank with one word.");
      feedback.gapFill = gradeGapFill(round.gapAnswers!, input.gapAnswers);
      feedback.correctAnswer = round.answer;
      score = feedback.gapFill.score;
      if (round.kind === "scene-words")
        feedback.sceneWordNotes = round.sceneWordNotes;
    } else if (round.kind === "spelling" || round.kind === "word-scramble") {
      if (!input.response || !/\p{L}/u.test(input.response) || input.audio)
        throw new BadRequestException("Build the word from the letter tiles.");
      score = gradeSpelling(round.answer, input.response);
      feedback.correct = score === 100;
      feedback.correctAnswer = round.answer;
    } else if (round.kind === "word-order") {
      if (!input.response || !/\p{L}/u.test(input.response) || input.audio)
        throw new BadRequestException("Put the word tiles in order.");
      feedback.dictation = gradeDictation(round.answer, input.response, "en");
      feedback.correctAnswer = round.answer;
      score = feedback.dictation.score;
    } else if (
      round.kind === "reading-order" ||
      round.kind === "sentence-puzzle"
    ) {
      if (!input.response || input.audio || input.choice !== undefined)
        throw new BadRequestException(
          "Arrange the paragraphs before checking.",
        );
      const normalize = (text: string) => text.trim().replace(/\s+/gu, " ");
      const response = normalize(input.response);
      if (
        response.length !== normalize(round.answer).length ||
        !round.tiles!.every((text) => response.includes(normalize(text)))
      )
        throw new BadRequestException("Use every paragraph exactly once.");
      feedback.correct = response === normalize(round.answer);
      feedback.correctAnswer = round.answer;
      score = feedback.correct ? 100 : 0;
    } else if (round.kind === "dictation") {
      if (
        !input.response ||
        !/[\p{L}\p{N}]/u.test(input.response) ||
        input.audio
      )
        throw new BadRequestException("Type what you heard.");
      feedback.dictation = gradeDictation(round.answer, input.response, "en");
      score = feedback.dictation.score;
    } else {
      if (
        !round.choices?.some((choice) => choice.id === input.choice) ||
        input.audio
      )
        throw new BadRequestException("Choose one of the available answers.");
      feedback.correct = input.choice === round.answer;
      feedback.correctAnswer = round.choices.find(
        (choice) => choice.id === round.answer,
      )!.label;
      score = feedback.correct ? 100 : 0;
      if (round.kind === "true-false") {
        score = gradeQuickFire(
          feedback.correct,
          input.elapsedMs,
          round.timeLimit ?? 10,
        );
        if (feedback.correct && score < 100)
          feedback.explanation = `Correct, but after the ${round.timeLimit ?? 10}-second timer. ${round.explanation}`;
      }
    }
    return this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${userId} FOR UPDATE`;
      if (!(await tx.user.findUnique({ where: { id: userId } })))
        throw new NotFoundException();
      const previous = await tx.gameAttempt.findUnique({
        where: { userId_eventId: { userId, eventId: input.eventId } },
      });
      if (previous) {
        if (previous.payloadHash !== hash)
          throw new ConflictException(
            "This game event already has another answer.",
          );
        return receipt(previous);
      }
      if (input.levelSessionId)
        await this.levels.assertRound(
          tx,
          userId,
          input.levelSessionId,
          round.id,
        );
      feedback.independent =
        input.completedPlays <= 2 &&
        !(await tx.gameAttempt.findFirst({
          where: {
            userId,
            roundId: round.id,
            score: { not: null },
            createdAt: { gte: new Date(Date.now() - 86400000) },
          },
        }));
      const { audio, ...metadata } = input;
      const saved = receipt(
        await tx.gameAttempt.create({
          data: {
            userId,
            levelSessionId: input.levelSessionId,
            eventId: input.eventId,
            roundId: round.id,
            score,
            payloadHash: hash,
            payload: { ...metadata, ...(audioHash ? { audioHash } : {}) },
            feedback,
          },
        }),
      );
      if (input.levelSessionId)
        await this.levels.finish(tx, userId, input.levelSessionId);
      return saved;
    });
  }
  private reserve(userId: string) {
    const now = Date.now();
    for (const [id, times] of this.requests)
      if (times.every((time) => time <= now - 60000)) this.requests.delete(id);
    const recent =
      this.requests.get(userId)?.filter((time) => time > now - 60000) ?? [];
    this.global = this.global.filter((time) => time > now - 60000);
    if (this.pending.size > 2 || recent.length >= 6 || this.global.length >= 12)
      throw new HttpException("Game assessment is busy. Retry shortly.", 429);
    this.requests.set(userId, [...recent, now]);
    this.global.push(now);
  }
}
