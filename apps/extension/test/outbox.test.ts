import { describe, it, expect } from "vitest";
import { randomUUID } from "node:crypto";
import { Outbox } from "../src/outbox";
const input = () => ({
  eventId: randomUUID(),
  text: "available",
  context: "Are you available?",
  sourceUrl: "https://example.com/page",
  sourceLanguage: "en" as const,
  targetLanguage: "en" as const,
});
function harness() {
  let stored: any = { records: [] };
  let settings = { enabled: true, excludedOrigins: [] as string[] };
  let identity = "learner-a";
  let token = "valid";
  const accepted = new Map<string, string>();
  let behavior = "online";
  const deps = {
    storage: {
      read: async () => structuredClone(stored),
      write: async (v: unknown) => {
        stored = structuredClone(v);
      },
    },
    settings: async () => ({ ...settings, identity }),
    remoteSettings: async () => {
      if (behavior === "offline") throw new Error("offline");
      if (token === "expired")
        throw Object.assign(new Error("expired"), { status: 401 });
      return settings;
    },
    send: async (value: any) => {
      if (behavior === "offline") throw new Error("offline");
      if (token === "expired")
        throw Object.assign(new Error("expired"), { status: 401 });
      accepted.set(value.eventId, value.text);
      if (behavior === "lost-ack") {
        behavior = "online";
        throw new Error("lost response");
      }
      return {
        lookupId: "saved-" + value.eventId,
        candidateId: "candidate",
        translationStatus: "pending",
      };
    },
  };
  return {
    deps,
    queue: () => stored.records,
    accepted,
    setBehavior: (v: string) => {
      behavior = v;
    },
    setSettings: (v: Partial<typeof settings>) => {
      settings = { ...settings, ...v };
    },
    setIdentity: (v: string) => {
      identity = v;
    },
    setToken: (v: string) => {
      token = v;
    },
  };
}
describe("durable consent-aware outbox", () => {
  it("offline_replay_survives_restart", async () => {
    const h = harness();
    h.setBehavior("offline");
    await new Outbox(h.deps).enqueueLookup(input());
    await new Outbox(h.deps).flushOutbox();
    expect(h.queue()).toHaveLength(1);
    expect(h.accepted.size).toBe(0);
    h.setBehavior("online");
    await new Outbox(h.deps).flushOutbox();
    expect(h.queue()[0].status).toBe("synchronized");
    expect(h.accepted.size).toBe(1);
  });
  it("ack_lost_replays_same_id", async () => {
    const h = harness();
    const value = input();
    await new Outbox(h.deps).enqueueLookup(value);
    h.setBehavior("lost-ack");
    await new Outbox(h.deps).flushOutbox();
    expect(h.queue()[0].status).toBe("pending");
    await new Outbox(h.deps).flushOutbox();
    expect([...h.accepted.keys()]).toEqual([value.eventId]);
    expect(h.queue()[0].status).toBe("synchronized");
  });
  it("revoked_consent_discards_pending", async () => {
    const h = harness();
    const outbox = new Outbox(h.deps);
    await outbox.enqueueLookup(input());
    h.setSettings({ enabled: false });
    await outbox.flushOutbox();
    expect(h.queue()).toHaveLength(0);
    expect(h.accepted.size).toBe(0);
  });
  it("excluded_origin_never_captures", async () => {
    const h = harness();
    h.setSettings({ excludedOrigins: ["https://example.com"] });
    const outbox = new Outbox(h.deps);
    await outbox.enqueueLookup(input());
    await outbox.flushOutbox();
    expect(h.queue()).toHaveLength(0);
    expect(h.accepted.size).toBe(0);
  });
  it("expired_identity_pauses_without_loss", async () => {
    const h = harness();
    const outbox = new Outbox(h.deps);
    await outbox.enqueueLookup(input());
    h.setToken("expired");
    await outbox.flushOutbox();
    expect(h.queue()[0].status).toBe("paused");
    expect(h.accepted.size).toBe(0);
    h.setToken("valid");
    await outbox.flushOutbox();
    expect(h.queue()[0].status).toBe("synchronized");
  });
  it("does_not_upload_another_accounts_pending_content", async () => {
    const h = harness();
    const outbox = new Outbox(h.deps);
    await outbox.enqueueLookup(input());
    h.setIdentity("learner-b");
    await outbox.flushOutbox();
    expect(h.accepted.size).toBe(0);
    expect(h.queue()[0].status).toBe("pending");
    h.setIdentity("learner-a");
    await outbox.flushOutbox();
    expect(h.accepted.size).toBe(1);
  });
  it("does_not_lose_concurrent_captures", async () => {
    const h = harness();
    const outbox = new Outbox(h.deps);
    await Promise.all([
      outbox.enqueueLookup(input()),
      outbox.enqueueLookup(input()),
    ]);
    expect(h.queue()).toHaveLength(2);
  });
  it("drops_sensitive_url_components_and_rechecks_new_exclusions", async () => {
    const h = harness();
    const outbox = new Outbox(h.deps);
    await outbox.enqueueLookup({
      ...input(),
      sourceUrl: "https://name:password@example.com/page?secret=value#fragment",
    });
    expect(h.queue()[0].input.sourceUrl).toBe("https://example.com/page");
    h.setSettings({ excludedOrigins: ["https://example.com"] });
    await outbox.flushOutbox();
    expect(h.queue()).toHaveLength(0);
    expect(h.accepted.size).toBe(0);
  });
});

it("discard_cancels_an_unsent_snapshot_without_resurrection", async () => {
  const h = harness();
  let release!: () => void;
  let entered!: () => void;
  const gate = new Promise<void>((r) => {
    release = r;
  });
  const started = new Promise<void>((r) => {
    entered = r;
  });
  const original = h.deps.remoteSettings;
  h.deps.remoteSettings = async () => {
    entered();
    await gate;
    return original();
  };
  const outbox = new Outbox(h.deps);
  await outbox.enqueueLookup(input());
  const flushing = outbox.flushOutbox();
  await started;
  const discarding = outbox.discard();
  release();
  await Promise.all([flushing, discarding]);
  expect(h.accepted.size).toBe(0);
  expect(h.queue()).toHaveLength(0);
});

function deferred() {
  let resolve!: () => void;
  return {
    promise: new Promise<void>((r) => {
      resolve = r;
    }),
    resolve: () => resolve(),
  };
}

for (const boundary of ["settings", "send"] as const) {
  it(`persists_new_lookups_while_${boundary}_is_waiting_and_preserves_them_on_ack`, async () => {
    const h = harness();
    const gate = deferred();
    const entered = deferred();
    if (boundary === "settings") {
      const original = h.deps.remoteSettings;
      h.deps.remoteSettings = async () => {
        entered.resolve();
        await gate.promise;
        return original();
      };
    } else {
      const original = h.deps.send;
      h.deps.send = async (value) => {
        entered.resolve();
        await gate.promise;
        return original(value);
      };
    }
    const outbox = new Outbox(h.deps);
    const first = input();
    const second = input();
    await outbox.enqueueLookup(first);
    const flushing = outbox.flushOutbox();
    await entered.promise;
    const saving = outbox.enqueueLookup(second);
    const immediate = await Promise.race([
      saving.then(() => true),
      new Promise<boolean>((r) => setTimeout(() => r(false), 75)),
    ]);
    const savedBeforeNetwork = h
      .queue()
      .some((x: any) => x.input.eventId === second.eventId);
    gate.resolve();
    await Promise.all([saving, flushing]);
    expect(immediate).toBe(true);
    expect(savedBeforeNetwork).toBe(true);
    expect(new Set(h.queue().map((x: any) => x.input.eventId))).toEqual(
      new Set([first.eventId, second.eventId]),
    );
    expect(
      h.queue().find((x: any) => x.input.eventId === first.eventId).status,
    ).toBe("synchronized");
  });
}

it("discard_finishes_without_waiting_for_network_and_cannot_be_undone_by_ack", async () => {
  const h = harness();
  const gate = deferred();
  const entered = deferred();
  const original = h.deps.send;
  h.deps.send = async (value) => {
    entered.resolve();
    await gate.promise;
    return original(value);
  };
  const outbox = new Outbox(h.deps);
  await outbox.enqueueLookup(input());
  const flushing = outbox.flushOutbox();
  await entered.promise;
  const discarding = outbox.discard();
  const immediate = await Promise.race([
    discarding.then(() => true),
    new Promise<boolean>((r) => setTimeout(() => r(false), 75)),
  ]);
  gate.resolve();
  await Promise.all([flushing, discarding]);
  expect(immediate).toBe(true);
  expect(h.queue()).toHaveLength(0);
});

for (const policy of ["disabled", "excluded"] as const) {
  it(`purges_all_disallowed_queries_beyond_the_network_batch_before_${policy}_is_reversed`, async () => {
    const h = harness();
    const outbox = new Outbox(h.deps);
    for (let i = 0; i < 25; i++) await outbox.enqueueLookup(input());
    h.setSettings(
      policy === "disabled"
        ? { enabled: false }
        : { excludedOrigins: ["https://example.com"] },
    );
    await outbox.flushOutbox();
    expect(h.queue()).toHaveLength(0);
    h.setSettings({ enabled: true, excludedOrigins: [] });
    await outbox.flushOutbox();
    expect(h.accepted.size).toBe(0);
  });
}
it("remote_consent_revocation_purges_the_entire_pending_batch", async () => {
  const h = harness();
  const outbox = new Outbox(h.deps);
  for (let i = 0; i < 25; i++) await outbox.enqueueLookup(input());
  h.deps.remoteSettings = async () => ({ enabled: false, excludedOrigins: [] });
  await outbox.flushOutbox();
  expect(h.queue()).toHaveLength(0);
  expect(h.accepted.size).toBe(0);
});

it("queued_language_choices_survive_preference_changes_and_restarts", async () => {
  const h = harness();
  const value = {
    ...input(),
    sourceLanguage: "auto",
    targetLanguage: "zh-Hans",
  };
  const outbox = new Outbox(h.deps);
  await outbox.enqueueLookup(value);
  value.targetLanguage = "en";
  let sent: unknown;
  const restarted = new Outbox({
    ...h.deps,
    send: async (input) => {
      sent = input;
      return {
        lookupId: "stored",
        candidateId: "candidate",
        translationStatus: "pending",
      };
    },
  });
  await restarted.flushOutbox();
  expect(sent).toMatchObject({
    sourceLanguage: "auto",
    targetLanguage: "zh-Hans",
  });
});
