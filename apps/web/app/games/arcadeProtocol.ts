import { z } from "zod";
import type { GameRound } from "@contextspeak/contracts";

const textAnswer = z
  .object({
    response: z
      .string()
      .trim()
      .max(2000)
      .regex(/[\p{L}\p{N}]/u),
  })
  .strict();
const choiceAnswer = z
  .object({
    choice: z.string().min(1).max(80),
    elapsedMs: z.number().int().min(0).max(600000).optional(),
  })
  .strict();
const gapAnswer = z
  .object({
    gapAnswers: z.array(z.string().trim().min(1).max(80)).min(1).max(6),
  })
  .strict();
const answerSchema = z.union([textAnswer, choiceAnswer, gapAnswer]);
export type RoundAnswer = z.infer<typeof answerSchema>;

export function readRoundAnswer(
  answer: unknown,
  round: GameRound,
): RoundAnswer | null {
  if (round.track === "speaking") return null;
  const schema =
    round.kind === "dictation" ||
    round.kind === "spelling" ||
    round.kind === "word-order" ||
    round.kind === "reading-order" ||
    round.kind === "word-scramble" ||
    round.kind === "sentence-puzzle"
      ? textAnswer
      : round.kind === "gap-fill" ||
          round.kind === "scene-words" ||
          round.kind === "mini-crossword" ||
          round.recall
        ? gapAnswer
        : choiceAnswer;
  const parsed = schema.safeParse(answer);
  if (!parsed.success) return null;
  const value = parsed.data;
  if (
    "gapAnswers" in value &&
    value.gapAnswers.length !==
      (round.crossword?.length ?? (round.gapText?.length ?? 0) - 1)
  )
    return null;
  if (
    "choice" in value &&
    !round.choices?.some((item) => item.id === value.choice)
  )
    return null;
  return value;
}
const envelope = {
  channel: z.literal("contextspeak-arcade"),
  roundId: z.string().min(1).max(80),
};
const actionSchema = z.union([
  z.object({ channel: envelope.channel, action: z.literal("ready") }).strict(),
  z
    .object({
      ...envelope,
      action: z.enum([
        "play",
        "record",
        "stop",
        "cancel",
        "retry",
        "next",
        "leave",
      ]),
    })
    .strict(),
  z
    .object({ ...envelope, action: z.literal("submit"), answer: answerSchema })
    .strict(),
]);
export type ArcadeAction = z.infer<typeof actionSchema>;

/** Same-origin alone is insufficient: other tabs/frames must not control this round. */
export function readArcadeAction(
  event: { data: unknown; source: unknown; origin: string },
  origin: string,
  frame: unknown,
  round: GameRound,
): ArcadeAction | null {
  if (!frame || event.source !== frame || event.origin !== origin) return null;
  const parsed = actionSchema.safeParse(event.data);
  if (!parsed.success) return null;
  const action = parsed.data;
  if (action.action === "ready") return action;
  if (action.roundId !== round.id) return null;
  if (action.action === "submit" && !readRoundAnswer(action.answer, round))
    return null;
  return action;
}
