import {
  ConflictException,
  NotFoundException,
  ServiceUnavailableException,
} from "@nestjs/common";
import { Prisma, type PrismaClient } from "@prisma/client";
import { createHash } from "node:crypto";
import {
  learningDetailsSchema,
  wordExpansionSchema,
  type Identity,
  type TranslationInput,
  type WordExpansion,
} from "@contextspeak/contracts";
import { LibraryService } from "../library/library.service";
import { filterExpansion, type ExpansionProvider } from "./expansion.provider";
export type ExpansionRequest = {
  input: TranslationInput;
  meaning: string;
  lookupId?: string;
};
export class ExpansionService {
  private cache = new Map<
    string,
    { userId: string; until: number; value: WordExpansion }
  >();
  private pending = new Map<string, Promise<WordExpansion>>();
  private requests = new Map<string, number[]>();
  constructor(
    private prisma: PrismaClient,
    private library: LibraryService,
    private provider?: ExpansionProvider,
  ) {}
  forgetUser(userId: string) {
    for (const [key, entry] of this.cache)
      if (entry.userId === userId) this.cache.delete(key);
    this.requests.delete(userId);
  }
  async get(identity: Identity, request: ExpansionRequest) {
    const user = await this.library.actor(identity);
    let { input, meaning } = request;
    const lookup = request.lookupId
      ? await this.prisma.lookupEvent.findFirst({
          where: { id: request.lookupId, userId: user.id },
        })
      : null;
    if (request.lookupId && !lookup) throw new NotFoundException();
    if (lookup) {
      if (
        lookup.translationStatus !== "succeeded" ||
        lookup.text !== input.text ||
        lookup.targetLanguage !== input.targetLanguage ||
        lookup.context !== (input.context ?? null)
      )
        throw new ConflictException("Translate this saved lookup first.");
      input = {
        text: lookup.text,
        context: lookup.context ?? undefined,
        sourceLanguage: lookup.detectedSourceLanguage ?? lookup.sourceLanguage,
        targetLanguage: lookup.targetLanguage,
      };
      if (!lookup.meaning?.trim())
        throw new ConflictException("Saved meaning unavailable.");
      meaning = lookup.meaning;
      const details = learningDetailsSchema.safeParse(lookup.learningDetails);
      if (details.success && details.data.expansion)
        return { expansion: details.data.expansion };
    }
    if (
      input.sourceLanguage !== "en" ||
      !/^\p{L}[\p{L}'-]{1,39}$/u.test(input.text) ||
      !this.provider
    )
      throw new ServiceUnavailableException(
        "Expansion is available for English words with the configured AI provider.",
      );
    const key = createHash("sha256")
      .update(
        JSON.stringify([
          user.id,
          input.text,
          input.context ?? "",
          input.sourceLanguage,
          input.targetLanguage,
          meaning,
        ]),
      )
      .digest("hex");
    const cached = this.cache.get(key);
    let value = cached && cached.until > Date.now() ? cached.value : undefined;
    let work: Promise<WordExpansion> | undefined;
    if (!value) {
      work = this.pending.get(key);
      if (!work) {
        for (const [id, times] of this.requests)
          if (times.every((time) => time < Date.now() - 60000))
            this.requests.delete(id);
        if (this.pending.size >= 2)
          throw new ServiceUnavailableException(
            "Expansion busy. The base translation is unchanged.",
          );
        const recent = (this.requests.get(user.id) ?? []).filter(
          (time) => time > Date.now() - 60000,
        );
        if (recent.length >= 5)
          throw new ServiceUnavailableException(
            "Expansion busy. The base translation is unchanged.",
          );
        this.requests.set(user.id, [...recent, Date.now()]);
        work = this.provider
          .expand(input, meaning)
          .then((result) =>
            filterExpansion(wordExpansionSchema.parse(result), input.text),
          );
        this.pending.set(key, work);
      }
      try {
        value = await work;
      } catch {
        if (this.pending.get(key) === work) this.pending.delete(key);
        throw new ServiceUnavailableException(
          "Expansion unavailable. The base translation is unchanged.",
        );
      }
    }
    try {
      await this.prisma.$transaction(async (tx) => {
        await tx.$queryRaw`SELECT id FROM "User" WHERE id = ${user.id} FOR UPDATE`;
        const current = await tx.user.findUnique({ where: { id: user.id } });
        if (!current) throw new NotFoundException();
        if (current.consentVersion !== user.consentVersion)
          throw new ConflictException("Collection settings changed.");
        this.cache.set(key, {
          userId: user.id,
          until: Date.now() + 86400000,
          value,
        });
        if (this.cache.size > 500)
          this.cache.delete(this.cache.keys().next().value!);
        if (lookup) {
          const row = await tx.lookupEvent.findFirst({
            where: {
              id: lookup.id,
              userId: user.id,
              translationStatus: "succeeded",
            },
          });
          const details = learningDetailsSchema.safeParse(row?.learningDetails);
          if (!details.success)
            throw new ConflictException(
              "Saved learning notes are unavailable.",
            );
          await tx.lookupEvent.update({
            where: { id: lookup.id },
            data: {
              learningDetails: JSON.parse(
                JSON.stringify({ ...details.data, expansion: value }),
              ) as Prisma.InputJsonValue,
            },
          });
        }
      });
    } finally {
      // Keep the reservation until the checked cache is visible, not just until
      // model generation ends; otherwise a concurrent request can generate again.
      if (work && this.pending.get(key) === work) this.pending.delete(key);
    }
    return { expansion: value };
  }
}
