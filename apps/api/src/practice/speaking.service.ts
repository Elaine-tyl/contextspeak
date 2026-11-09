import {
  BadRequestException,
  ConflictException,
  HttpException,
  NotFoundException,
} from "@nestjs/common";
import { createHash } from "node:crypto";
import type { PrismaClient } from "@prisma/client";
import type {
  Identity,
  DialogueTurn,
  PracticeReceipt,
  SpeakingUpload,
} from "@contextspeak/contracts";
import { dialogueContext } from "./dialogue";
import { LibraryService } from "../library/library.service";
import { PracticeService, practiceReceipt } from "./practice.service";
import type { SpeakingProvider } from "./speaking.provider";
function recording(input: SpeakingUpload) {
  const bytes = Buffer.from(input.audio, "base64");
  const signatures: Record<SpeakingUpload["mimeType"], boolean> = {
    "audio/webm": bytes
      .subarray(0, 4)
      .equals(Buffer.from([0x1a, 0x45, 0xdf, 0xa3])),
    "audio/ogg": bytes.toString("ascii", 0, 4) === "OggS",
    "audio/wav":
      bytes.toString("ascii", 0, 4) === "RIFF" &&
      bytes.toString("ascii", 8, 12) === "WAVE",
    "audio/mp4": bytes.toString("ascii", 4, 8) === "ftyp",
  };
  if (
    bytes.length < 16 ||
    bytes.length > 2 * 1024 * 1024 ||
    bytes.toString("base64") !== input.audio ||
    !signatures[input.mimeType]
  )
    throw new BadRequestException(
      "Choose a supported recording smaller than 2 MB.",
    );
  return createHash("sha256").update(bytes).digest("hex");
}
export class SpeakingService {
  private pending = new Map<
    string,
    { hash: string; result: Promise<PracticeReceipt> }
  >();
  private requests = new Map<string, number[]>();
  private globalRequests: number[] = [];
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
    private practice: PracticeService,
    private provider?: SpeakingProvider,
  ) {}
  async submit(identity: Identity, itemId: string, input: SpeakingUpload) {
    const audioHash = recording(input);
    const { audio, ...metadata } = input;
    const payloadHash = createHash("sha256")
      .update(JSON.stringify({ itemId, ...metadata, audioHash }))
      .digest("hex");
    const user = await this.library.actor(identity);
    const key = `${user.id}:${input.eventId}`;
    const pending = this.pending.get(key);
    if (pending) {
      if (pending.hash !== payloadHash)
        throw new ConflictException(
          "This assessment already has another recording.",
        );
      return pending.result;
    }
    // Reserve before any asynchronous lookup, including already-completed retries.
    const result = this.prepare(
      identity,
      user.id,
      itemId,
      input,
      payloadHash,
      audioHash,
    );
    this.pending.set(key, { hash: payloadHash, result });
    try {
      return await result;
    } finally {
      this.pending.delete(key);
    }
  }
  private async prepare(
    identity: Identity,
    userId: string,
    itemId: string,
    input: SpeakingUpload,
    payloadHash: string,
    audioHash: string,
  ) {
    const { audio, ...metadata } = input;
    const previous = await this.prisma.practiceAttempt.findUnique({
      where: { userId_eventId: { userId: userId, eventId: input.eventId } },
    });
    if (previous) {
      if (previous.payloadHash !== payloadHash)
        throw new ConflictException("This event already has another answer.");
      return practiceReceipt(previous);
    }
    const { recent, ...base } = await this.practice.task(
      identity,
      itemId,
      input.lookupId,
      input.scenario,
    );
    const task = {
      ...base,
      ...(!input.scenario && base.speakingQuestion
        ? { sourceText: base.speakingQuestion }
        : {}),
      ...(await dialogueContext(this.prisma, userId, base, input)),
    };
    if (task.language !== "en")
      throw new ConflictException(
        "AI speaking assessment currently supports English practice.",
      );
    if (!this.provider)
      throw new HttpException("AI speaking assessment is unavailable.", 503);
    const now = Date.now();
    for (const [id, times] of this.requests)
      if (times.every((time) => time <= now - 60000)) this.requests.delete(id);
    const times =
      this.requests.get(userId)?.filter((time) => time > now - 60000) ?? [];
    this.globalRequests = this.globalRequests.filter(
      (time) => time > now - 60000,
    );
    if (
      this.pending.size > 2 ||
      times.length >= (input.conversation ? 6 : 3) ||
      this.globalRequests.length >= 6
    )
      throw new HttpException(
        "Speaking quota reached or busy. Try again shortly.",
        429,
      );
    this.requests.set(userId, [...times, now]);
    this.globalRequests.push(now);
    return this.assessAndSave(
      userId,
      itemId,
      input,
      metadata,
      payloadHash,
      audioHash,
      task,
    );
  }
  private async assessAndSave(
    userId: string,
    itemId: string,
    input: SpeakingUpload,
    metadata: Omit<SpeakingUpload, "audio">,
    payloadHash: string,
    audioHash: string,
    task: Omit<Awaited<ReturnType<PracticeService["task"]>>, "recent"> & {
      dialogue?: DialogueTurn;
    },
  ) {
    let assessment;
    try {
      assessment = await this.provider!.assess({
        audio: input.audio,
        mimeType: input.mimeType,
        task,
        dialogue: task.dialogue,
      });
    } catch (error) {
      throw new HttpException(
        "AI assessment unavailable. Your recording stays in this tab; retry later.",
        (error as { status?: number }).status === 429 ? 429 : 503,
      );
    }
    return this.prisma.$transaction(async (tx) => {
      await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${userId} FOR UPDATE`;
      // Deletion or ownership changes while Gemini was running must not recreate learning data.
      if (
        !(await tx.lookupEvent.findFirst({
          where: { id: input.lookupId, candidateId: itemId, userId },
        }))
      )
        throw new NotFoundException();
      const previous = await tx.practiceAttempt.findUnique({
        where: { userId_eventId: { userId, eventId: input.eventId } },
      });
      if (previous) {
        if (previous.payloadHash !== payloadHash)
          throw new ConflictException("This event already has another answer.");
        return practiceReceipt(previous);
      }
      return practiceReceipt(
        await tx.practiceAttempt.create({
          data: {
            userId,
            eventId: input.eventId,
            lookupId: input.lookupId,
            payloadHash,
            // Store submission metadata and a digest, never the base64 recording or Blob URL.
            payload: {
              ...metadata,
              audioHash,
              mode: "speaking",
              activity: "ai-speaking",
              plays: 0,
              completedPlays: 0,
              response: "",
            },
            task: { ...task, speakingAssessment: assessment },
          },
        }),
      );
    });
  }
}
