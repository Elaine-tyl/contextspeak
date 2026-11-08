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
} from "@contextspeak/contracts";
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
) {
  if (lookup.translationStatus !== "succeeded" || !lookup.meaning?.trim())
    throw new ConflictException("A successful saved translation is required.");
  const details = learningDetailsSchema.safeParse(lookup.learningDetails);
  const example = details.success ? details.data.examples[0] : undefined;
  const sourceText = example?.source ?? lookup.text;
  if ([...sourceText].length > 600)
    throw new ConflictException(
      "Choose an expression shorter than 600 characters for practice.",
    );
  const detected = lookup.detectedSourceLanguage ?? lookup.sourceLanguage;
  return {
    version: 1 as const,
    lookupId: lookup.id,
    expression: lookup.text,
    sourceText,
    translation: example?.translation ?? lookup.meaning,
    language: detected === "auto" ? null : detected,
  };
}
function receipt(row: PracticeAttempt) {
  const payload = row.payload as unknown as PracticeSubmission;
  return {
    id: row.id,
    mode: payload.mode,
    selfCheck: payload.selfCheck,
    plays: payload.plays,
    revealed: payload.revealed,
    durationMs: payload.durationMs,
    createdAt: row.createdAt,
  };
}
export class PracticeService {
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
  ) {}
  async task(identity: Identity, itemId: string, lookupId?: string) {
    const detail = await this.library.itemDetail(identity, itemId, lookupId);
    if (!detail.lookup)
      throw new ConflictException("Translate and save this vocabulary first.");
    const task = taskFor(detail.lookup);
    const user = await this.library.actor(identity);
    const recent = await this.prisma.practiceAttempt.findMany({
      where: { userId: user.id, lookupId: task.lookupId },
      orderBy: [{ createdAt: "desc" }, { id: "desc" }],
      take: 10,
    });
    return { itemId, ...task, recent: recent.map(receipt) };
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
        return receipt(previous);
      }
      const task = taskFor(lookup);
      return receipt(
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
