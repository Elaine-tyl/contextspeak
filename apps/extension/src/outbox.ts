import {
  lookupSchema,
  type LookupInput,
  type LookupReceipt,
  type CollectionSettings,
} from "@contextspeak/contracts";
export interface OutboxRecord {
  input: LookupInput;
  identity: string;
  status: "pending" | "paused" | "synchronized" | "failed";
  attempts: number;
  receipt?: LookupReceipt;
  error?: string;
}
export interface OutboxState {
  records: OutboxRecord[];
}
export interface CaptureReference {
  identity: string;
  eventId: string;
}
interface Dependencies {
  storage: {
    read(): Promise<OutboxState>;
    write(value: OutboxState): Promise<void>;
  };
  settings(): Promise<CollectionSettings & { identity: string }>;
  remoteSettings(signal?: AbortSignal): Promise<CollectionSettings>;
  send(
    input: LookupInput,
    identity: string,
    signal?: AbortSignal,
  ): Promise<LookupReceipt>;
}
export class Outbox {
  private discardRevision = 0;
  private storageOperation: Promise<unknown> = Promise.resolve();
  private flushing?: Promise<void>;
  private controller?: AbortController;
  constructor(private deps: Dependencies) {}
  // Only local state operations share this lock. Never hold it across HTTP calls.
  private local<T>(fn: () => Promise<T>): Promise<T> {
    const next = this.storageOperation.then(fn);
    this.storageOperation = next.catch(() => {});
    return next;
  }
  enqueueLookup(
    value: LookupInput,
    expectedIdentity?: string,
  ): Promise<CaptureReference | undefined> {
    return this.local(async () => {
      const input = lookupSchema.parse(value);
      const settings = await this.deps.settings();
      if (
        !this.allowed(input, settings) ||
        !settings.identity ||
        (expectedIdentity !== undefined &&
          settings.identity !== expectedIdentity)
      )
        return;
      const reference = { identity: settings.identity, eventId: input.eventId };
      const state = await this.deps.storage.read();
      if (state.records.some((record) => this.matches(record, reference)))
        return reference;
      if (
        state.records.filter((r) => r.status !== "synchronized").length >= 200
      )
        throw new Error(
          "Outbox is full. Synchronize or discard queued queries first.",
        );
      state.records = state.records
        .filter((r) => r.status !== "synchronized")
        .concat(
          state.records.filter((r) => r.status === "synchronized").slice(-20),
        );
      state.records.push({
        input,
        identity: settings.identity,
        status: "pending",
        attempts: 0,
      });
      await this.deps.storage.write(state);
      return reference;
    });
  }
  getRecord(eventId: string, identity: string) {
    return this.local(async () => {
      const state = await this.deps.storage.read();
      return state.records.find((r) => this.matches(r, { eventId, identity }));
    });
  }
  discard() {
    ++this.discardRevision;
    this.controller?.abort();
    return this.local(async () => {
      const settings = await this.deps.settings();
      const state = await this.deps.storage.read();
      state.records = state.records.filter(
        (r) => r.identity !== settings.identity,
      );
      await this.deps.storage.write(state);
    });
  }
  flushOutbox(): Promise<void> {
    if (this.flushing) return this.flushing;
    const controller = new AbortController();
    this.controller = controller;
    const task = this.drain(this.discardRevision, controller.signal).finally(
      () => {
        if (this.flushing === task) {
          this.flushing = undefined;
          this.controller = undefined;
        }
      },
    );
    this.flushing = task;
    return task;
  }
  private async drain(revision: number, signal: AbortSignal) {
    const visited = new Set<string>();
    for (let processed = 0; processed < 20; processed++) {
      const record = await this.local(async () => {
        const settings = await this.deps.settings();
        const state = await this.deps.storage.read();
        const retained = state.records.filter(
          (r) =>
            r.identity !== settings.identity ||
            r.status === "synchronized" ||
            this.allowed(r.input, settings),
        );
        if (retained.length !== state.records.length) {
          state.records = retained;
          await this.deps.storage.write(state);
        }
        return state.records.find(
          (r) =>
            r.identity === settings.identity &&
            !visited.has(r.input.eventId) &&
            ["pending", "paused"].includes(r.status),
        );
      });
      if (!record || revision !== this.discardRevision) return;
      const ref = { identity: record.identity, eventId: record.input.eventId };
      visited.add(ref.eventId);
      const local = await this.deps.settings();
      if (local.identity !== ref.identity) return;
      if (!this.allowed(record.input, local)) {
        await this.prune(ref.identity, local, revision);
        continue;
      }
      try {
        const remote = await this.deps.remoteSettings(signal);
        if (signal.aborted || revision !== this.discardRevision) return;
        const latest = await this.deps.settings();
        if (latest.identity !== ref.identity) return;
        if (
          !this.allowed(record.input, latest) ||
          !this.allowed(record.input, remote)
        ) {
          await this.prune(ref.identity, latest, revision);
          await this.prune(ref.identity, remote, revision);
          continue;
        }
        const exists = await this.getRecord(ref.eventId, ref.identity);
        if (!exists || signal.aborted || revision !== this.discardRevision)
          return;
        const receipt = await this.deps.send(
          record.input,
          ref.identity,
          signal,
        );
        await this.mutate(ref, revision, (current) => ({
          ...current,
          receipt,
          status: "synchronized",
          error: undefined,
        }));
      } catch (error) {
        if (signal.aborted || revision !== this.discardRevision) return;
        const status = (error as { status?: number }).status;
        await this.mutate(ref, revision, (current) => {
          if (status === 403) return;
          if (status === 401)
            return {
              ...current,
              status: "paused",
              error: "Reconnect with a fresh developer token.",
            };
          const attempts = current.attempts + 1;
          return {
            ...current,
            attempts,
            status:
              (status && status >= 400 && status < 500) || attempts >= 5
                ? "failed"
                : "pending",
            error:
              status === 409
                ? "Event conflict. Discard this query."
                : "Synchronization unavailable.",
          };
        });
        return;
      }
    }
  }
  private prune(
    identity: string,
    policy: CollectionSettings,
    revision: number,
  ) {
    return this.local(async () => {
      if (revision !== this.discardRevision) return;
      const state = await this.deps.storage.read();
      state.records = state.records.filter(
        (r) =>
          r.identity !== identity ||
          r.status === "synchronized" ||
          this.allowed(r.input, policy),
      );
      await this.deps.storage.write(state);
    });
  }
  private mutate(
    ref: CaptureReference,
    revision: number,
    update: (record: OutboxRecord) => OutboxRecord | undefined,
  ) {
    return this.local(async () => {
      if (revision !== this.discardRevision) return;
      const state = await this.deps.storage.read();
      const index = state.records.findIndex((record) =>
        this.matches(record, ref),
      );
      if (index < 0) return;
      const next = update(state.records[index]);
      if (next) state.records[index] = next;
      else state.records.splice(index, 1);
      await this.deps.storage.write(state);
    });
  }
  private matches(record: OutboxRecord, ref: CaptureReference) {
    return (
      record.identity === ref.identity && record.input.eventId === ref.eventId
    );
  }
  private allowed(input: LookupInput, settings: CollectionSettings) {
    return (
      settings.enabled &&
      (!input.sourceUrl ||
        !settings.excludedOrigins.includes(new URL(input.sourceUrl).origin))
    );
  }
}
