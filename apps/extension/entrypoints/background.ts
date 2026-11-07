import { defineBackground } from "wxt/utils/define-background";
import { browser } from "wxt/browser";
import {
  lookupSchema,
  translationSchema,
  settingsSchema,
  type LookupReceipt,
  type LookupInput,
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
    send: async (value, expectedIdentity) => {
      const current = await connection();
      if (current.identity !== expectedIdentity)
        throw Object.assign(new Error("Identity changed"), { status: 401 });
      if (
        !current.enabled ||
        (value.sourceUrl &&
          current.excludedOrigins.includes(new URL(value.sourceUrl).origin))
      )
        throw Object.assign(new Error("Collection changed"), { status: 403 });
      return api<LookupReceipt>("/v1/lookups", "POST", value, current.token);
    },
  });
  let settingsOperation = Promise.resolve();
  browser.alarms.create("sync", { periodInMinutes: 1 });
  browser.alarms.onAlarm.addListener(() => {
    void outbox.flushOutbox();
  });
  browser.runtime.onMessage.addListener((message, sender) => {
    if (sender.id !== browser.runtime.id) return;
    // Webpage content scripts may translate only; credentials/settings stay in extension pages.
    if (
      message?.type !== "translate" &&
      !sender.url?.startsWith(`chrome-extension://${browser.runtime.id}/`)
    )
      return Promise.resolve({ ok: false, error: "Not permitted" });
    return (async () => {
      try {
        if (message.type === "connect") {
          const token = String(message.token ?? "");
          const identity = await verifiedIdentity(token);
          const remote = await api<{
            enabled: boolean;
            excludedOrigins: string[];
          }>("/v1/settings", "GET", undefined, token);
          await browser.storage.local.set({
            connection: { token, identity, ...remote },
          });
          void outbox.flushOutbox();
          return { ok: true };
        }
        if (message.type === "settings") {
          const settings = settingsSchema.parse(message.value);
          // Disable locally first so queued content is not transmitted while settings are updated.
          const work = settingsOperation.then(async () => {
            const current = await connection();
            await browser.storage.local.set({
              connection: { ...current, ...settings, enabled: false },
            });
            await api("/v1/settings", "PUT", settings);
            await browser.storage.local.set({
              connection: { ...current, ...settings },
            });
          });
          settingsOperation = work.catch(() => {});
          await work;
          await outbox.flushOutbox();
          return { ok: true };
        }
        if (message.type === "sync") {
          await outbox.flushOutbox();
          return { ok: true };
        }
        if (message.type === "discard") {
          await browser.storage.local.set({ outbox: { records: [] } });
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
        if (message.type === "translate") {
          if (!sender.tab?.url || !/^https?:/.test(sender.tab.url))
            throw new Error("Select text on a supported webpage.");
          const value = lookupSchema.parse({
            ...message.input,
            sourceUrl: sender.tab.url,
          });
          await outbox.enqueueLookup(value);
          void outbox.flushOutbox();
          const { eventId, ...input } = value;
          try {
            return {
              ok: true,
              ...(await api<{ meaning: string; provider: string }>(
                "/v1/translations",
                "POST",
                translationSchema.parse(input),
              )),
            };
          } catch {
            return {
              ok: false,
              error:
                "Fixture unavailable or offline. Eligible lookup intent remains queued.",
            };
          }
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
