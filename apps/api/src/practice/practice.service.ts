import { ConflictException, NotFoundException } from "@nestjs/common";
import { createHash } from "node:crypto";
import type {
  PrismaClient,
  PracticeAttempt,
  LookupEvent,
} from "@prisma/client";
import {
  learningDetailsSchema,
  type Identity,
  type PracticeSubmission,
  type ScenarioId,
  type DictationFeedback,
  type SpeakingAssessment,
  type DialogueTurn,
} from "@contextspeak/contracts";
import { gradeDictation } from "./dictation";
import { conversationTask } from "./scenarios";
import type { LibraryFilters } from "../library/library-filters";
import { LibraryService } from "../library/library.service";
function taskFor(
  lookup: Pick<
    LookupEvent,
    | "id"
    | "text"
    | "meaning"
    | "translationStatus"
    | "sourceLanguage"
    | "detectedSourceLanguage"
    | "learningDetails"
  >,
  scenario?: ScenarioId,
) {
  if (lookup.translationStatus !== "succeeded" || !lookup.meaning?.trim())
    throw new ConflictException("A successful saved translation is required.");
  const details = learningDetailsSchema.safeParse(lookup.learningDetails);
  const example = details.success ? details.data.examples[0] : undefined;
  const sourceText = example?.source ?? lookup.text;
  if (!scenario && [...sourceText].length > 600)
    throw new ConflictException(
      "Choose an expression shorter than 600 characters for practice.",
    );
  const detected = lookup.detectedSourceLanguage ?? lookup.sourceLanguage;
  const language = detected === "auto" ? null : detected;
  return {
    version: 1 as const,
    lookupId: lookup.id,
    expression: lookup.text,
    speakingQuestion: `How could you use “${lookup.text.slice(0, 120)}” to describe something from your own life? Give a specific example.`,
    sourceText,
    translation: example?.translation ?? lookup.meaning,
    language,
    ...(scenario ? conversationTask(scenario, language) : {}),
  };
}
export function practiceReceipt(row: PracticeAttempt) {
  const payload = row.payload as unknown as PracticeSubmission;
  const { feedback, speakingAssessment, dialogue } = row.task as unknown as {
    feedback?: DictationFeedback;
    speakingAssessment?: SpeakingAssessment;
    dialogue?: DialogueTurn;
  };
  return {
    id: row.id,
    ...(dialogue ? { dialogue } : {}),
    ...(feedback ? { feedback } : {}),
    ...(speakingAssessment ? { speakingAssessment } : {}),
    mode: payload.mode,
    ...(payload.scenario ? { scenario: payload.scenario } : {}),
    selfCheck: payload.selfCheck,
    plays: payload.plays,
    revealed: payload.revealed,
    durationMs: payload.durationMs,
    createdAt: row.createdAt.toISOString(),
  };
}
export class PracticeService {
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
  ) {}
  async list(identity: Identity, filters: LibraryFilters) {
    const user = await this.library.actor(identity);
    const successful = { translationStatus: "succeeded", meaning: { not: "" } };
    const rows = await this.prisma.learningItem.findMany({
      where: {
        userId: user.id,
        ...(filters.q
          ? { text: { contains: filters.q, mode: "insensitive" as const } }
          : {}),
        ...(filters.sourceLanguage
          ? { sourceLanguage: filters.sourceLanguage }
          : {}),
        ...(filters.targetLanguage
          ? { targetLanguage: filters.targetLanguage }
          : {}),
        lookups: { some: successful },
      },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: 50,
      select: {
        id: true,
        text: true,
        sourceLanguage: true,
        targetLanguage: true,
        // A failed retry must not hide a previously usable encounter.
        lookups: {
          where: successful,
          orderBy: [{ createdAt: "desc" }, { id: "desc" }],
          take: 1,
          select: { id: true },
        },
      },
    });
    return {
      items: rows.map(({ lookups, ...item }) => ({
        ...item,
        lookupId: lookups[0].id,
      })),
    };
  }
  async task(
    identity: Identity,
    itemId: string,
    lookupId?: string,
    scenario?: ScenarioId,
  ) {
    const detail = await this.library.itemDetail(identity, itemId, lookupId);
    if (!detail.lookup)
      throw new ConflictException("Translate and save this vocabulary first.");
    const task = taskFor(detail.lookup, scenario);
    const user = await this.library.actor(identity);
    const recent = await this.prisma.practiceAttempt.findMany({
      where: { userId: user.id, lookupId: task.lookupId },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: 10,
    });
    return { itemId, ...task, recent: recent.map(practiceReceipt) };
  }
  async save(identity: Identity, itemId: string, input: PracticeSubmission) {
    const user = await this.library.actor(identity);
    const payloadHash = createHash("sha256")
      .update(JSON.stringify({ itemId, ...input }))
      .digest("hex");
    return this.prisma.$transaction(async (tx) => {
      // Serialize a learner's event IDs so concurrent retries count once.
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${user.id} FOR UPDATE`;
      const lookup = await tx.lookupEvent.findFirst({
        where: { id: input.lookupId, candidateId: itemId, userId: user.id },
      });
      if (!lookup) throw new NotFoundException();
      const previous = await tx.practiceAttempt.findUnique({
        where: { userId_eventId: { userId: user.id, eventId: input.eventId } },
      });
      if (previous) {
        if (previous.payloadHash !== payloadHash)
          throw new ConflictException(
            "This practice event was already used for another answer.",
          );
        return practiceReceipt(previous);
      }
      const base = taskFor(lookup, input.scenario);
      const task =
        input.activity === "dictation"
          ? {
              ...base,
              feedback: gradeDictation(
                base.sourceText,
                input.response,
                base.language,
              ),
            }
          : base;
      return practiceReceipt(
        await tx.practiceAttempt.create({
          data: {
            userId: user.id,
            eventId: input.eventId,
            lookupId: lookup.id,
            payloadHash,
            payload: input,
            task,
          },
        }),
      );
    });
  }
}
