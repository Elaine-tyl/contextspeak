import {
  EXTENSION_PROTOCOL,
  RELOAD_EXTENSION,
  localLanguages,
  readTranslationOptions,
} from "../../src/translation-ui";
import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  defaultTranslationPreferences,
  type TranslationPreferences,
  type TranslationLanguage,
} from "@contextspeak/contracts";
import { WEB, type Connection } from "../../src/settings";
import { browser } from "wxt/browser";
function Popup() {
  const [preferences, setPreferences] = useState<TranslationPreferences>(
    defaultTranslationPreferences,
  );
  const [languages, setLanguages] =
    useState<TranslationLanguage[]>(localLanguages);
  const refreshGeneration = useRef(0);
  const [connectionReady, setConnectionReady] = useState(false);
  const [languagesReady, setLanguagesReady] = useState(false);
  const [languageError, setLanguageError] = useState("");
  const [provider, setProvider] = useState("");
  const [pairing, setPairing] = useState("");
  const [enabled, setEnabled] = useState(false);
  const [excluded, setExcluded] = useState("");
  const [records, setRecords] = useState<
    { text: string; status: string; error?: string }[]
  >([]);
  const [identity, setIdentity] = useState("");
  const [status, setStatus] = useState("");
  async function refresh() {
    const generation = ++refreshGeneration.current;
    setConnectionReady(false);
    setLanguagesReady(false);
    try {
      const r = await browser.runtime.sendMessage({ type: "status" });
      if (generation !== refreshGeneration.current) return;
      if (!r.ok)
        throw new Error("Could not read extension status. " + RELOAD_EXTENSION);
      setPairing(r.pairing ?? "");
      setEnabled(r.connection.enabled);
      setExcluded(r.connection.excludedOrigins.join("\n"));
      setIdentity(r.connection.identity);
      setRecords(r.records);
      if (r.protocolVersion !== EXTENSION_PROTOCOL)
        throw new Error(RELOAD_EXTENSION);
      setConnectionReady(true);
      const reply = await browser.runtime.sendMessage({
        type: "translation-options",
      });
      if (generation !== refreshGeneration.current) return;
      const options = readTranslationOptions(reply);
      setLanguages(options.languages);
      setPreferences(options.preferences);
      setProvider(options.provider);
      setLanguagesReady(true);
      setLanguageError("");
    } catch (error) {
      if (generation !== refreshGeneration.current) return;
      setLanguagesReady(false);
      setLanguageError(
        error instanceof Error ? error.message : RELOAD_EXTENSION,
      );
    }
  }
  useEffect(() => {
    void refresh();
    const changed = (
      changes: Record<string, { newValue?: unknown; oldValue?: unknown }>,
      area: string,
    ) => {
      if (area !== "local") return;
      if (changes.pairing)
        setPairing(
          (changes.pairing.newValue as { userCode?: string } | undefined)
            ?.userCode ?? "",
        );
      const next = changes.connection?.newValue as Connection | undefined;
      const previous = changes.connection?.oldValue as Connection | undefined;
      if (next?.token !== previous?.token) void refresh();
    };
    browser.storage.onChanged.addListener(changed);
    const timer = setInterval(() => {
      void browser.runtime
        .sendMessage({ type: "pairing-poll" })
        .then((result) => {
          if (result.changed) void refresh();
          if (!result.ok) setStatus(result.error);
        })
        .catch(() => {});
    }, 5500);
    return () => {
      clearInterval(timer);
      browser.storage.onChanged.removeListener(changed);
    };
  }, []);
  async function send(value: unknown) {
    const r = await browser.runtime.sendMessage(value);
    setStatus(r.ok ? "Updated." : r.error);
    await refresh();
  }
  return (
    <main>
      <small>CONTEXTSPEAK</small>
      <h1>Capture settings</h1>
      <p>
        Sign in on the website and approve this extension. Your Google
        credentials stay outside the extension.
      </p>
      <button
        onClick={async () => {
          const result = await browser.runtime.sendMessage({
            type: "pairing-start",
          });
          if (!result.ok) {
            setStatus(result.error);
            return;
          }
          setPairing(result.userCode);
          await browser.tabs.create({ url: result.verificationUrl });
        }}
      >
        Pair extension
      </button>{" "}
      <button onClick={() => send({ type: "disconnect" })}>Disconnect</button>
      {pairing && (
        <p>
          Pairing code: <strong data-testid="pairing-code">{pairing}</strong>.
          Match it on the website, then approve. Keep this window open or reopen
          it to finish pairing.
        </p>
      )}
      <p>{identity ? "Connected" : "Not connected"}</p>
      <h2>Translation languages</h2>
      <p>
        {provider === "fixture"
          ? "Fixture test mode"
          : "Free local translation"}{" "}
        · maximum 2,000 characters per selection.
      </p>
      {!languagesReady && !languageError && <p>Loading language settings…</p>}
      {languageError && <p role="alert">{languageError}</p>}
      <label>
        Source language
        <select
          disabled={!languagesReady}
          aria-label="Source language"
          value={preferences.sourceLanguage}
          onChange={(e) =>
            setPreferences({ ...preferences, sourceLanguage: e.target.value })
          }
        >
          <option value="auto">Detect automatically</option>
          {languages.map((language) => (
            <option key={language.code} value={language.code}>
              {language.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Target language
        <select
          disabled={!languagesReady}
          aria-label="Target language"
          value={preferences.targetLanguage}
          onChange={(e) =>
            setPreferences({ ...preferences, targetLanguage: e.target.value })
          }
        >
          {languages.map((language) => (
            <option key={language.code} value={language.code}>
              {language.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        <input
          type="checkbox"
          checked={preferences.autoTranslate}
          onChange={(e) =>
            setPreferences({ ...preferences, autoTranslate: e.target.checked })
          }
        />{" "}
        Translate automatically after selecting text
      </label>
      <button
        disabled={!languagesReady || !identity}
        onClick={() =>
          send({ type: "translation-preferences", value: preferences })
        }
      >
        Save language settings
      </button>
      {languageError && (
        <button onClick={() => void refresh()}>Retry loading languages</button>
      )}
      <h2>Collection</h2>
      <label>
        <input
          type="checkbox"
          aria-label="Automatically collect selection-translation queries"
          checked={enabled}
          onChange={(e) => setEnabled(e.target.checked)}
        />{" "}
        Automatically collect selection-translation queries
      </label>
      <p>
        Only selected text and a small surrounding excerpt are stored. Review
        enrollment is separate.
      </p>
      <label>
        Excluded origins
        <textarea
          aria-label="Excluded origins"
          value={excluded}
          onChange={(e) => setExcluded(e.target.value)}
          placeholder="https://example.com"
        />
      </label>
      <button
        disabled={!connectionReady || !identity}
        onClick={() =>
          send({
            type: "settings",
            value: {
              enabled,
              excludedOrigins: excluded
                .split("\n")
                .map((s) => s.trim())
                .filter(Boolean),
            },
          })
        }
      >
        Save collection settings
      </button>
      <p role="status">{status}</p>
      <h2>Synchronization</h2>
      <button onClick={() => send({ type: "sync" })}>Sync now</button>{" "}
      <button onClick={() => send({ type: "discard" })}>
        Discard local queue
      </button>
      <ul>
        {records.map((r, i) => (
          <li key={i}>
            {r.text} — {r.status}
            {r.error && <p>{r.error}</p>}
          </li>
        ))}
      </ul>
      <a href={WEB + "/library"} target="_blank" rel="noreferrer">
        Open learning library
      </a>
    </main>
  );
}
const style = document.createElement("style");
style.textContent =
  "body{margin:0;width:360px;background:#f4f7f6;color:#183a35;font:14px system-ui}main{padding:24px}h1{font-size:25px}small{letter-spacing:1px;color:#53746b;font-size:10px}label{display:block;margin:16px 0 8px}select{display:block;width:100%;padding:8px;margin-top:6px}textarea{box-sizing:border-box;width:100%;height:64px;margin-top:6px;border:1px solid #b6ccc4;border-radius:8px;padding:8px}button{border:0;border-radius:8px;background:#176b60;color:white;padding:10px 12px;cursor:pointer}p{line-height:1.5;color:#536c65}li{padding:6px 0}a{color:#176b60}";
document.head.append(style);
createRoot(document.getElementById("root")!).render(<Popup />);
