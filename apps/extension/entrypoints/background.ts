import { ExtensionSpeech } from "../src/speech-session";
import { playSpeech, stopSpeech } from "../src/speech-player";
import { speechInputSchema, speechAudioSchema } from "@contextspeak/contracts";
import { EXTENSION_PROTOCOL, localLanguages } from "../src/translation-ui";
import { completePairing } from "../src/pairing";
import { LookupFlow } from "../src/lookup-flow";
import { ConnectionManager } from "../src/connection-manager";
import { defineBackground } from "wxt/utils/define-background";
import { browser } from "wxt/browser";
import {
  defaultTranslationPreferences,
  translationPreferencesSchema,
  type TranslationPreferences,
  type TranslationOptions,
  lookupSchema,
  translationSchema,
  settingsSchema,
  type LookupReceipt,
  type LookupTranslationResult,
} from "@contextspeak/contracts";
import { Outbox, type OutboxState } from "../src/outbox";
import {
  api,
  API,
  WEB,
  connection,
  verifiedIdentity,
  serverSettings,
} from "../src/settings";
export default defineBackground(() => {
  const speech = new ExtensionSpeech({
    session: async () => {
      const current = await connection();
      if (
        !current.token ||
        (await verifiedIdentity(current.token)) !== current.identity
      )
        throw new Error("Extension connection changed");
      return current;
    },
    generate: async (input, token, signal) =>
      speechAudioSchema.parse(
        await api("/v1/speech", "POST", input, token, signal),
      ),
    play: playSpeech,
    stop: stopSpeech,
  });
  browser.tabs.onRemoved.addListener((tabId) => speech.stopTab(tabId));
  browser.tabs.onUpdated.addListener((tabId, change) => {
    if (change.status === "loading" || change.url) speech.stopTab(tabId);
  });
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
    retry: (id, token) =>
      api(`/v1/lookups/${id}/retry`, "POST", undefined, token),
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
      const translationPreferences = await api<TranslationPreferences>(
        "/v1/translation-preferences",
        "GET",
        undefined,
        token,
      );
      return { token, identity, ...remote, translationPreferences };
    },
    putTranslationPreferences: (value, token) =>
      api("/v1/translation-preferences", "PUT", value, token),
    putSettings: (value, token) => api("/v1/settings", "PUT", value, token),
  });
  type Pairing = {
    deviceCode: string;
    userCode: string;
    verificationUrl: string;
  };
  async function pendingPairing() {
    return (await browser.storage.local.get("pairing")).pairing as
      Pairing | undefined;
  }
  let pairingRevision = 0;
  let polling: Promise<unknown> | undefined;
  async function pairingRequest(path: string, body: unknown) {
    const response = await fetch(API + path, {
      method: "POST",
      credentials: "omit",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      signal: AbortSignal.timeout(8000),
    });
    if (response.status === 429) return { pending: true };
    if (!response.ok) throw new Error("Pairing expired. Start again.");
    return response.json();
  }
  async function pollPairing() {
    const revision = pairingRevision;
    const pairing = await pendingPairing();
    if (!pairing) return;
    const result = await pairingRequest("/auth/pairings/poll", {
      deviceCode: pairing.deviceCode,
    });
    if (!result.token) return;
    const completed = await completePairing(result.token, {
      current: async () => {
        const latest = await pendingPairing();
        return (
          revision === pairingRevision &&
          latest?.deviceCode === pairing.deviceCode
        );
      },
      connect: (token) => manager.connect(token),
      revoke: async (token) => {
        await api("/auth/extension/disconnect", "POST", undefined, token);
      },
    });
    if (!completed) return false;
    if (revision === pairingRevision)
      await browser.storage.local.remove("pairing");
    void outbox.flushOutbox();
    return true;
  }
  void browser.storage.local.setAccessLevel({
    accessLevel: "TRUSTED_CONTEXTS",
  });
  browser.alarms.create("sync", { periodInMinutes: 1 });
  browser.alarms.onAlarm.addListener(() => {
    void outbox.flushOutbox();
  });
  browser.runtime.onMessage.addListener((message, sender) => {
    if (sender.id !== browser.runtime.id || message?.type === "speech-player")
      return;
    // Webpage content scripts may translate only; credentials/settings stay in extension pages.
    if (
      message?.type !== "translate" &&
      message?.type !== "lookup-result" &&
      message?.type !== "lookup-retry" &&
      message?.type !== "translation-options" &&
      message?.type !== "speech-start" &&
      message?.type !== "speech-stop" &&
      !sender.url?.startsWith(`chrome-extension://${browser.runtime.id}/`)
    )
      return Promise.resolve({ ok: false, error: "Not permitted" });
    return (async () => {
      try {
        if (message.type === "speech-start" || message.type === "speech-stop") {
          if (
            sender.tab?.id === undefined ||
            !sender.tab.url ||
            !/^https?:/.test(sender.tab.url) ||
            typeof message.requestId !== "string" ||
            !/^[a-f0-9-]{36}$/i.test(message.requestId)
          )
            return { ok: false, error: "Not permitted" };
          const owner = `${sender.tab.id}:${sender.frameId ?? 0}`;
          if (message.type === "speech-stop") {
            speech.stop(owner, message.requestId);
            return { ok: true };
          }
          const parsed = speechInputSchema.safeParse({
            text: message.text,
            language: message.language,
          });
          if (!parsed.success)
            return {
              ok: false,
              error: "Select shorter text in a supported language.",
            };
          return speech.start(owner, message.requestId, parsed.data);
        }
        if (message.type === "pairing-start") {
          const revision = ++pairingRevision;
          const result = await pairingRequest("/auth/pairings", {});
          if (revision !== pairingRevision || !result.deviceCode)
            throw new Error("Pairing changed");
          const url = new URL(result.verificationUrl);
          if (url.origin !== WEB || url.pathname !== "/pair")
            throw new Error("Invalid pairing URL");
          await browser.storage.local.set({ pairing: result });
          return {
            ok: true,
            userCode: result.userCode,
            verificationUrl: result.verificationUrl,
          };
        }
        if (message.type === "pairing-poll") {
          if (!polling)
            polling = pollPairing().finally(() => {
              polling = undefined;
            });
          const changed = await polling;
          return { ok: true, changed: Boolean(changed) };
        }
        if (message.type === "disconnect") {
          speech.stop();
          ++pairingRevision;
          const previous = await connection();
          const disconnecting = manager.disconnect();
          await browser.storage.local.remove("pairing");
          await disconnecting;
          if (previous.token) {
            try {
              await api(
                "/auth/extension/disconnect",
                "POST",
                undefined,
                previous.token,
              );
            } catch {
              return {
                ok: false,
                error:
                  "Disconnected locally. To confirm server revocation, use Revoke all extensions on the website.",
              };
            }
          }
          return { ok: true };
        }
        if (message.type === "translation-options") {
          const current = await connection();
          let options: TranslationOptions;
          try {
            options = await api<TranslationOptions>(
              "/v1/translation-options",
              "GET",
              undefined,
              current.token,
            );
            await browser.storage.local.set({ translationOptions: options });
          } catch {
            // Language discovery must never block saving a lookup while offline.
            options = ((await browser.storage.local.get("translationOptions"))
              .translationOptions as TranslationOptions) ?? {
              provider: "libretranslate-local",
              languages: localLanguages,
            };
          }
          const preferences =
            current.translationPreferences ??
            (await api<TranslationPreferences>(
              "/v1/translation-preferences",
              "GET",
              undefined,
              current.token,
            ).catch(() => defaultTranslationPreferences));
          const latest = await connection();
          if (
            latest.identity !== current.identity ||
            latest.token !== current.token
          )
            return {
              ok: false,
              protocolVersion: EXTENSION_PROTOCOL,
              code: "ACCOUNT_CHANGED",
            };
          return {
            ok: true,
            protocolVersion: EXTENSION_PROTOCOL,
            ...options,
            preferences,
          };
        }
        if (message.type === "translation-preferences") {
          await manager.saveTranslationPreferences(
            translationPreferencesSchema.parse(message.value),
          );
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
            protocolVersion: EXTENSION_PROTOCOL,
            pairing: (await pendingPairing())?.userCode,
            connection: {
              ...safe,
              translationPreferences:
                safe.translationPreferences ?? defaultTranslationPreferences,
            },
            records: state.records
              .filter((x) => x.identity === safe.identity)
              .map((x) => ({
                text: x.input.text,
                status: x.status,
                error: x.error,
              })),
          };
        }
        if (
          message.type === "translate" ||
          message.type === "lookup-result" ||
          message.type === "lookup-retry"
        ) {
          if (!sender.tab?.url || !/^https?:/.test(sender.tab.url))
            throw new Error("Select text on a supported webpage.");
          if (
            message.type === "lookup-result" ||
            message.type === "lookup-retry"
          ) {
            if (
              typeof message.eventId !== "string" ||
              message.eventId.length > 64
            )
              throw new Error("Invalid event ID");
            return message.type === "lookup-retry"
              ? lookupFlow.retry(message.eventId)
              : lookupFlow.result(message.eventId);
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
