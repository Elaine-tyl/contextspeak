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
