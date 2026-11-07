import { translationErrorMessage } from "./translation-errors";
import type {
  LookupInput,
  LookupTranslationResult,
  TranslationInput,
} from "@contextspeak/contracts";
import type { Outbox } from "./outbox";
export type LookupReply =
  | {
      ok: true;
      status: "queued" | "pending" | "succeeded";
      eventId?: string;
      meaning?: string;
      error?: string;
    }
  | { ok: false; status: "failed" | "paused" | "cancelled"; error: string };
interface Dependencies {
  outbox: Pick<Outbox, "enqueueLookup" | "flushOutbox" | "getRecord">;
  session(): Promise<{ identity: string; token: string }>;
  translate(
    input: TranslationInput,
    token: string,
  ): Promise<{ meaning: string; provider: string }>;
  result(lookupId: string, token: string): Promise<LookupTranslationResult>;
}
export class LookupFlow {
  constructor(private deps: Dependencies) {}
  async start(input: LookupInput): Promise<LookupReply> {
    const session = await this.deps.session();
    const capture = await this.deps.outbox.enqueueLookup(
      input,
      session.identity,
    );
    if (capture) {
      void this.deps.outbox.flushOutbox().catch(() => {});
      return { ok: true, status: "queued", eventId: input.eventId };
    }
    // Collection-off/excluded queries are transient; never create a second translation for a captured query.
    if ((await this.deps.session()).identity !== session.identity)
      return this.cancelled();
    const { eventId, ...value } = input;
    try {
      const translated = await this.deps.translate(value, session.token);
      if ((await this.deps.session()).identity !== session.identity)
        return this.cancelled();
      return { ok: true, status: "succeeded", meaning: translated.meaning };
    } catch (error) {
      return {
        ok: false,
        status: "failed",
        error:
          translationErrorMessage((error as { code?: string }).code) +
          " Collection is off for this query; nothing was saved.",
      };
    }
  }
  async result(eventId: string): Promise<LookupReply> {
    const session = await this.deps.session();
    const record = await this.deps.outbox.getRecord(eventId, session.identity);
    if (!record) return this.cancelled();
    if (record.status === "paused")
      return {
        ok: false,
        status: "paused",
        error: "Saved locally. Pair the extension again to sync.",
      };
    if (record.status === "failed")
      return {
        ok: false,
        status: "failed",
        error:
          "Saved locally. Synchronization failed; inspect the extension queue.",
      };
    if (!record.receipt)
      return {
        ok: true,
        status: "queued",
        eventId,
        error: record.error
          ? "Saved locally. Offline or unable to sync."
          : "Saved locally. Waiting for sync…",
      };
    try {
      const result = await this.deps.result(
        record.receipt.lookupId,
        session.token,
      );
      if (
        (await this.deps.session()).identity !== session.identity ||
        !(await this.deps.outbox.getRecord(eventId, session.identity))
      )
        return this.cancelled();
      if (result.translationStatus === "succeeded")
        return { ok: true, status: "succeeded", meaning: result.meaning ?? "" };
      if (result.translationStatus === "failed")
        return {
          ok: false,
          status: "failed",
          error:
            translationErrorMessage(result.errorCode) +
            " Your lookup is saved.",
        };
      return {
        ok: true,
        status: "pending",
        eventId,
        error: "Saved. Translation is processing…",
      };
    } catch {
      return {
        ok: true,
        status: "pending",
        eventId,
        error:
          "Saved. Result unavailable while offline; check the library when reconnected.",
      };
    }
  }
  private cancelled(): LookupReply {
    return {
      ok: false,
      status: "cancelled",
      error: "Collection was cancelled or the connected account changed.",
    };
  }
}
