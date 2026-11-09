import { ConflictException, NotFoundException } from "@nestjs/common";
import type { PrismaClient } from "@prisma/client";
import type {
  DialogueTurn,
  PracticeTask,
  SpeakingAssessment,
  SpeakingUpload,
} from "@contextspeak/contracts";
export async function dialogueContext(
  prisma: PrismaClient,
  userId: string,
  task: Pick<PracticeTask, "itemId" | "sourceText" | "scenario" | "difficulty">,
  input: SpeakingUpload,
) {
  if (!input.conversation) return {};
  if (!input.previousTurnId)
    return { dialogue: { turn: 1, history: [] } satisfies DialogueTurn };
  const row = await prisma.practiceAttempt.findFirst({
    where: { id: input.previousTurnId, userId, lookupId: input.lookupId },
  });
  if (!row) throw new NotFoundException();
  const previous = row.task as unknown as PracticeTask & {
    dialogue?: DialogueTurn;
    speakingAssessment?: SpeakingAssessment;
  };
  const payload = row.payload as unknown as SpeakingUpload;
  const assessment = previous.speakingAssessment;
  if (
    !payload.conversation ||
    payload.scenario !== input.scenario ||
    previous.itemId !== task.itemId ||
    (previous.difficulty ?? 1) !== (task.difficulty ?? 1) ||
    !previous.dialogue ||
    previous.dialogue.turn >= 4 ||
    !assessment?.speechDetected ||
    !assessment.nextTurn
  ) {
    throw new ConflictException(
      "Choose a completed speaking turn from this conversation, or start a new conversation.",
    );
  }
  return {
    sourceText: assessment.nextTurn,
    dialogue: {
      turn: previous.dialogue.turn + 1,
      history: [
        ...previous.dialogue.history,
        { partner: previous.sourceText, response: assessment.transcript },
      ],
    } satisfies DialogueTurn,
  };
}
