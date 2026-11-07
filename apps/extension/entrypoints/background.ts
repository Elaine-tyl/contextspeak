import { LookupFlow } from "../src/lookup-flow";
import { ConnectionManager } from "../src/connection-manager";
import { defineBackground } from "wxt/utils/define-background";
import { browser } from "wxt/browser";
import {
  lookupSchema,
  translationSchema,
  settingsSchema,
  type LookupReceipt,
  type LookupTranslationResult,
} from "@contextspeak/contracts";
import { Outbox, type OutboxState } from "../src/outbox";
import {
  api,
  connection,
  verifiedIdentity,
  serverSettings,
} from "../src/settings";
export default defineBackground(() => {
  const outbox = new Outbox({
    storage: {
      read: async () =>
        ((await browser.storage.local.get("outbox")).outbox ?? {
          records: [],
        }) as OutboxState,
      write: async (value) => {
        await browser.storage.local.set({ outbox: value });
      },
    },
    settings: connection,
    remoteSettings: serverSettings,
    send: async (value, expectedIdentity, signal) => {
      const current = await connection();
      if (current.identity !== expectedIdentity)
        throw Object.assign(new Error("Identity changed"), { status: 401 });
      if (
        !current.enabled ||
        (value.sourceUrl &&
          current.excludedOrigins.includes(new URL(value.sourceUrl).origin))
      )
        throw Object.assign(new Error("Collection changed"), { status: 403 });
      if (signal?.aborted) throw signal.reason;
      return api<LookupReceipt>(
        "/v1/lookups",
        "POST",
        value,
        current.token,
        signal,
      );
    },
  });
  const lookupFlow = new LookupFlow({
    outbox,
    session: connection,
    translate: (value, token) => api("/v1/translations", "POST", value, token),
    result: (id, token) =>
      api<LookupTranslationResult>(
        `/v1/lookups/${id}/translation`,
        "GET",
        undefined,
        token,
      ),
  });
  const manager = new ConnectionManager({
    read: connection,
    write: async (value) => {
      await browser.storage.local.set({ connection: value });
    },
    authenticate: async (token) => {
      const identity = await verifiedIdentity(token);
      const remote = await api<{ enabled: boolean; excludedOrigins: string[] }>(
        "/v1/settings",
        "GET",
        undefined,
        token,
      );
      return { token, identity, ...remote };
    },
    putSettings: (value, token) => api("/v1/settings", "PUT", value, token),
  });
  browser.alarms.create("sync", { periodInMinutes: 1 });
  browser.alarms.onAlarm.addListener(() => {
    void outbox.flushOutbox();
  });
  browser.runtime.onMessage.addListener((message, sender) => {
    if (sender.id !== browser.runtime.id) return;
    // Webpage content scripts may translate only; credentials/settings stay in extension pages.
    if (
      message?.type !== "translate" &&
      message?.type !== "lookup-result" &&
      !sender.url?.startsWith(`chrome-extension://${browser.runtime.id}/`)
    )
      return Promise.resolve({ ok: false, error: "Not permitted" });
    return (async () => {
      try {
        if (message.type === "connect") {
          const token = String(message.token ?? "");
          await manager.connect(token);
          void outbox.flushOutbox();
          return { ok: true };
        }
        if (message.type === "settings") {
          const settings = settingsSchema.parse(message.value);
          await manager.saveSettings(settings);
          await outbox.flushOutbox();
          return { ok: true };
        }
        if (message.type === "sync") {
          await outbox.flushOutbox();
          return { ok: true };
        }
        if (message.type === "discard") {
          await outbox.discard();
          return { ok: true };
        }
        if (message.type === "status") {
          const { token, ...safe } = await connection();
          const state = ((await browser.storage.local.get("outbox")).outbox ?? {
            records: [],
          }) as OutboxState;
          return {
            ok: true,
            connection: safe,
            records: state.records
              .filter((x) => x.identity === safe.identity)
              .map((x) => ({
                text: x.input.text,
                status: x.status,
                error: x.error,
              })),
          };
        }
        if (message.type === "translate" || message.type === "lookup-result") {
          if (!sender.tab?.url || !/^https?:/.test(sender.tab.url))
            throw new Error("Select text on a supported webpage.");
          if (message.type === "lookup-result") {
            if (
              typeof message.eventId !== "string" ||
              message.eventId.length > 64
            )
              throw new Error("Invalid event ID");
            return lookupFlow.result(message.eventId);
          }
          const value = lookupSchema.parse({
            ...message.input,
            sourceUrl: sender.tab.url,
          });
          return lookupFlow.start(value);
        }
        return { ok: false, error: "Unknown request" };
      } catch {
        return {
          ok: false,
          error:
            "Request failed. Check connection, collection settings, and queued status.",
        };
      }
    })();
  });
  void outbox.flushOutbox();
});
