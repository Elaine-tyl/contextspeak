import { learningFixture } from "../../../tests/fixtures/learning";
import { it, expect } from "vitest";
import { randomUUID } from "node:crypto";
import { LookupFlow } from "../src/lookup-flow";
import { Outbox, type OutboxState } from "../src/outbox";
const value = () => ({
  eventId: randomUUID(),
  text: "available",
  sourceLanguage: "en" as const,
  targetLanguage: "en" as const,
});
function harness(enabled = true) {
  let state: OutboxState = { records: [] };
  let session = { identity: "A", token: "token-a" };
  let translations = 0;
  let reads = 0;
  let retries = 0;
  let retryError = false;
  let resultStatus: "pending" | "succeeded" | "failed" = "succeeded";
  const outbox = new Outbox({
    storage: {
      read: async () => structuredClone(state),
      write: async (v) => {
        state = structuredClone(v);
      },
    },
    settings: async () => ({ ...session, enabled, excludedOrigins: [] }),
    remoteSettings: async () => ({ enabled, excludedOrigins: [] }),
    send: async () => ({
      lookupId: "stored",
      candidateId: "candidate",
      translationStatus: "pending",
    }),
  });
  const flow = new LookupFlow({
    outbox,
    session: async () => session,
    translate: async () => {
      translations++;
      return {
        meaning: "Transient explanation",
        provider: "fixture",
        detectedSourceLanguage: "en",
        learningDetails: learningFixture,
      };
    },
    retry: async () => {
      retries++;
      if (retryError) throw new Error("offline");
      resultStatus = "succeeded";
    },
    result: async () => {
      reads++;
      return {
        lookupId: "stored",
        translationStatus: resultStatus,
        meaning: resultStatus === "succeeded" ? "Stored explanation" : null,
        provider: "fixture",
        detectedSourceLanguage: "en",
        learningDetails: learningFixture,
        errorCode: resultStatus === "failed" ? "FIXTURE_NOT_FOUND" : null,
      };
    },
  });
  return {
    flow,
    failRetry: () => {
      retryError = true;
    },
    outbox,
    state: () => state,
    counts: () => ({ translations, reads }),
    retries: () => retries,
    setResult: (status: typeof resultStatus) => {
      resultStatus = status;
    },
    switchAccount: () => {
      session = { identity: "B", token: "token-b" };
    },
  };
}
it("collected_popup_reads_the_shared_result_without_translating_again", async () => {
  const h = harness();
  const input = value();
  expect(await h.flow.start(input)).toEqual({
    ok: true,
    status: "queued",
    eventId: input.eventId,
  });
  await h.outbox.flushOutbox();
  expect(await h.flow.result(input.eventId)).toMatchObject({
    status: "succeeded",
    meaning: "Stored explanation",
  });
  expect(await h.flow.result(input.eventId)).toMatchObject({
    meaning: "Stored explanation",
  });
  expect(h.counts()).toEqual({ translations: 0, reads: 2 });
});
it("collection_off_translates_once_without_creating_a_record", async () => {
  const h = harness(false);
  expect(await h.flow.start(value())).toMatchObject({
    ok: true,
    status: "succeeded",
    meaning: "Transient explanation",
  });
  expect(h.state().records).toHaveLength(0);
  expect(h.counts()).toEqual({ translations: 1, reads: 0 });
});
it("shared_failure_does_not_fall_back_to_another_translation", async () => {
  const h = harness();
  const input = value();
  await h.flow.start(input);
  await h.outbox.flushOutbox();
  h.setResult("failed");
  expect(await h.flow.result(input.eventId)).toMatchObject({
    ok: false,
    status: "failed",
    error: "Translation is unavailable in fixture mode. Your lookup is saved.",
  });
  expect(h.counts().translations).toBe(0);
});
it("discard_and_account_switch_stop_observation_of_old_results", async () => {
  const h = harness();
  const input = value();
  await h.flow.start(input);
  await h.outbox.flushOutbox();
  h.switchAccount();
  expect(await h.flow.result(input.eventId)).toMatchObject({
    status: "cancelled",
  });
  expect(h.counts().reads).toBe(0);
});

it("account_switch_before_local_save_cancels_instead_of_capturing_for_the_new_owner", async () => {
  let state: OutboxState = { records: [] };
  let session = { identity: "A", token: "token-a" };
  let first = true;
  let transient = 0;
  const outbox = new Outbox({
    storage: {
      read: async () => structuredClone(state),
      write: async (value) => {
        state = structuredClone(value);
      },
    },
    settings: async () => ({ ...session, enabled: true, excludedOrigins: [] }),
    remoteSettings: async () => ({ enabled: true, excludedOrigins: [] }),
    send: async () => ({
      lookupId: "stored",
      candidateId: "candidate",
      translationStatus: "pending",
    }),
  });
  const flow = new LookupFlow({
    outbox,
    session: async () => {
      const snapshot = session;
      if (first) {
        first = false;
        session = { identity: "B", token: "token-b" };
      }
      return snapshot;
    },
    translate: async () => {
      transient++;
      return { meaning: "Unexpected", provider: "fixture" };
    },
    retry: async () => {},
    result: async () => {
      throw new Error("No lookup should exist");
    },
  });
  expect(await flow.start(value())).toMatchObject({ status: "cancelled" });
  expect(state.records).toHaveLength(0);
  expect(transient).toBe(0);
});

it("discard_during_a_shared_result_read_cancels_observation", async () => {
  let state: OutboxState = { records: [] };
  let release!: () => void;
  let entered!: () => void;
  const gate = new Promise<void>((r) => {
    release = r;
  });
  const started = new Promise<void>((r) => {
    entered = r;
  });
  const outbox = new Outbox({
    storage: {
      read: async () => structuredClone(state),
      write: async (value) => {
        state = structuredClone(value);
      },
    },
    settings: async () => ({
      identity: "A",
      enabled: true,
      excludedOrigins: [],
    }),
    remoteSettings: async () => ({ enabled: true, excludedOrigins: [] }),
    send: async () => ({
      lookupId: "stored",
      candidateId: "item",
      translationStatus: "pending",
    }),
  });
  const flow = new LookupFlow({
    outbox,
    session: async () => ({ identity: "A", token: "A" }),
    translate: async () => {
      throw new Error("No transient call");
    },
    retry: async () => {},
    result: async () => {
      entered();
      await gate;
      return {
        lookupId: "stored",
        translationStatus: "succeeded",
        meaning: "Late result",
        provider: "fixture",
        learningDetails: learningFixture,
        errorCode: null,
      };
    },
  });
  const input = value();
  await flow.start(input);
  await outbox.flushOutbox();
  const reading = flow.result(input.eventId);
  await started;
  await outbox.discard();
  release();
  expect(await reading).toMatchObject({ status: "cancelled" });
});

it("returns_the_same_learning_details_for_stored_and_transient_results", async () => {
  const saved = harness();
  const input = value();
  await saved.flow.start(input);
  await saved.outbox.flushOutbox();
  expect(await saved.flow.result(input.eventId)).toMatchObject({
    learningDetails: learningFixture,
    detectedSourceLanguage: "en",
  });
  expect(saved.counts().translations).toBe(0);
  expect(await harness(false).flow.start(value())).toMatchObject({
    learningDetails: learningFixture,
    detectedSourceLanguage: "en",
  });
});

it("retries_the_owned_saved_row_without_creating_a_new_encounter", async () => {
  const h = harness();
  const input = value();
  await h.flow.start(input);
  await h.outbox.flushOutbox();
  const before = h.state().records.length;
  h.setResult("failed");
  expect(await h.flow.result(input.eventId)).toMatchObject({
    status: "failed",
  });
  expect(await h.flow.retry(input.eventId)).toMatchObject({
    status: "succeeded",
    learningDetails: learningFixture,
  });
  expect(h.state().records.length).toBe(before);
  expect(h.retries()).toBe(1);
  expect(h.counts().translations).toBe(0);
  h.switchAccount();
  expect(await h.flow.retry(input.eventId)).toMatchObject({
    status: "cancelled",
  });
});

it("offline_retry_preserves_the_owned_lookup_saved_indicator", async () => {
  const h = harness();
  const input = value();
  await h.flow.start(input);
  await h.outbox.flushOutbox();
  h.failRetry();
  expect(await h.flow.retry(input.eventId)).toMatchObject({
    ok: false,
    status: "failed",
    saved: "library",
  });
});
