"use client";
import { useEffect, useRef, useState } from "react";
import type {
  Candidate,
  CollectionSettings,
  LookupRecord,
  Page,
} from "@contextspeak/contracts";
import { api } from "../../src/api";
import "./library.css";
export default function Library() {
  const [draft, setDraft] = useState("");
  const [connected, setConnected] = useState(false);
  const [identity, setIdentity] = useState("");
  const [history, setHistory] = useState<LookupRecord[]>([]);
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [view, setView] = useState<"history" | "candidates">("history");
  const token = useRef("");
  const generation = useRef(0);
  async function refresh(value = token.current, append = false) {
    if (!value) return;
    const current = generation.current;
    try {
      const [lookups, items, settings] = await Promise.all([
        api<Page<LookupRecord>>(
          value,
          "/v1/lookups" +
            (append && cursor ? "?cursor=" + encodeURIComponent(cursor) : ""),
        ),
        api<Page<Candidate>>(value, "/v1/learning-items"),
        api<CollectionSettings>(value, "/v1/settings"),
      ]);
      if (current !== generation.current) return;
      setHistory((previous) =>
        append ? [...previous, ...lookups.items] : lookups.items,
      );
      setCursor(lookups.nextCursor);
      setCandidates(items.items);
      setEnabled(settings.enabled);
    } catch (e) {
      if (current === generation.current)
        setMessage(
          e instanceof Error ? e.message : "Could not load your library.",
        );
    }
  }
  async function connect(value = draft) {
    const current = ++generation.current;
    token.current = "";
    setHistory([]);
    setCandidates([]);
    setCursor(null);
    setConnected(false);
    setBusy(true);
    setMessage("");
    sessionStorage.removeItem("contextspeak-token");
    try {
      const verified = await api<{ issuer: string; subject: string }>(
        value,
        "/v1/identity",
      );
      if (current !== generation.current) return;
      token.current = value;
      sessionStorage.setItem("contextspeak-token", value);
      setIdentity(verified.subject);
      setConnected(true);
      setDraft("");
      await refresh(value);
    } catch (e) {
      if (current === generation.current)
        setMessage(e instanceof Error ? e.message : "Connection failed.");
    } finally {
      if (current === generation.current) setBusy(false);
    }
  }
  useEffect(() => {
    const saved = sessionStorage.getItem("contextspeak-token");
    if (saved) void connect(saved);
    const timer = setInterval(() => {
      if (token.current && document.visibilityState === "visible")
        void refresh();
    }, 3000);
    return () => clearInterval(timer);
  }, []);
  async function toggle() {
    try {
      const current = await api<CollectionSettings>(
        token.current,
        "/v1/settings",
      );
      const updated = await api<CollectionSettings>(
        token.current,
        "/v1/settings",
        "PUT",
        { ...current, enabled: !current.enabled },
      );
      setEnabled(updated.enabled);
      setMessage(
        "Collection preference updated. The extension checks this before syncing.",
      );
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Could not update settings.");
    }
  }
  async function retry(id: string) {
    try {
      await api(token.current, `/v1/lookups/${id}/retry`, "POST");
      setMessage("Retry requested.");
      await refresh();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Retry failed.");
    }
  }
  function disconnect() {
    generation.current++;
    token.current = "";
    sessionStorage.removeItem("contextspeak-token");
    setConnected(false);
    setIdentity("");
    setHistory([]);
    setCandidates([]);
    setCursor(null);
    setMessage("Disconnected from this browser tab.");
  }
  return (
    <div className="shell">
      <aside>
        <a className="brand" href="/library">
          <span className="brand-mark">c</span> ContextSpeak
        </a>
        <p className="nav-label">YOUR LEARNING SPACE</p>
        <nav>
          <button
            className={view === "history" ? "nav active" : "nav"}
            onClick={() => setView("history")}
          >
            ◷ Query history
          </button>
          <button
            className={view === "candidates" ? "nav active" : "nav"}
            onClick={() => setView("candidates")}
          >
            ▤ Vocabulary candidates
          </button>
        </nav>
        <div className="aside-note">
          <span className="status-dot" /> FIXTURE DEVELOPMENT
          <p>
            Capture today.
            <br />
            Practice comes next.
          </p>
          <small>No paid translation services.</small>
        </div>
      </aside>
      <main>
        <header>
          <span className="eyebrow">YOUR PERSONAL ENGLISH LIBRARY</span>
          <div className="connection">
            <span className={"status-dot " + (connected ? "online" : "")} />
            <span>{connected ? "Connected" : "Not connected"}</span>
            {connected && (
              <button className="link-button" onClick={disconnect}>
                Disconnect
              </button>
            )}
          </div>
        </header>
        <section className="intro">
          <h1>
            Your English,
            <br />
            <em>in context.</em>
          </h1>
          <p>
            Every useful expression has a story.
            <br />
            Keep the context. Build your vocabulary, one encounter at a time.
          </p>
        </section>
        <details className="connect-panel" open={!connected}>
          <summary>
            {connected
              ? `Developer connection · ${identity}`
              : "Connect your local developer identity"}
          </summary>
          <p>
            Copy tokenA from <code>.local/demo.json</code> into both this page
            and the extension. It expires after one hour.
          </p>
          <div className="connect-form">
            <label>
              Developer token
              <textarea
                aria-label="Developer token"
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                spellCheck={false}
                autoComplete="off"
                placeholder="Paste your short-lived token"
              />
            </label>
            <button
              className="primary"
              disabled={busy || !draft.trim()}
              onClick={() => connect()}
            >
              {busy ? "Connecting…" : "Connect"}
            </button>
          </div>
        </details>
        {message && (
          <p className="notice" role="status">
            {message}
          </p>
        )}
        <section className="stats">
          <article>
            <span>RECENT ENCOUNTERS</span>
            <strong>
              {history.length}
              {cursor ? "+" : ""}
            </strong>
            <p>Selected expressions and their original context</p>
          </article>
          <article>
            <span>VOCABULARY CANDIDATES</span>
            <strong>{candidates.length}</strong>
            <p>Collected for consideration, not yet active reviews</p>
          </article>
          <article className="collection">
            <span>AUTOMATIC COLLECTION</span>
            <strong>{enabled ? "On" : "Off"}</strong>
            <p>
              {connected
                ? "You control what becomes part of your history."
                : "Connect before enabling collection."}
            </p>
            {connected && (
              <button className="link-button" onClick={toggle}>
                {enabled ? "Pause collection" : "Enable collection"}
              </button>
            )}
          </article>
        </section>
        <section className="library-section">
          <div className="section-head">
            <div>
              <span className="eyebrow">
                {view === "history"
                  ? "FROM YOUR EVERYDAY READING"
                  : "YOUR VOCABULARY MEMORY"}
              </span>
              <h2>
                {view === "history"
                  ? "Recent encounters"
                  : "Vocabulary candidates"}
              </h2>
            </div>
            <button
              className="secondary"
              onClick={() => refresh()}
              disabled={!connected}
            >
              Refresh
            </button>
          </div>
          <p className="section-copy">
            {view === "history"
              ? "A lookup is a starting point, not a test of what you know."
              : "Repeated encounters share a candidate. Word senses remain unresolved until there is enough evidence."}
          </p>
          <div className={view === "history" ? "cards" : "cards hidden"}>
            {history.map((item) => (
              <article
                className="lookup-card"
                data-testid="lookup-card"
                key={item.id}
              >
                <div className="card-head">
                  <h3>{item.text}</h3>
                  <span className={"badge " + item.translationStatus}>
                    {item.translationStatus === "succeeded"
                      ? "Fixture explained"
                      : item.translationStatus}
                  </span>
                </div>
                {item.context && <blockquote>“{item.context}”</blockquote>}
                {item.meaning && <p className="meaning">{item.meaning}</p>}
                {item.errorCode && (
                  <div className="failure">
                    <p>
                      {item.errorCode} — this query is saved. The fixture has no
                      explanation or is unavailable.
                    </p>
                    <button
                      className="link-button"
                      onClick={() => retry(item.id)}
                    >
                      Retry translation
                    </button>
                  </div>
                )}
                <footer>
                  <span>
                    {new Date(item.createdAt).toLocaleDateString("en-GB", {
                      day: "numeric",
                      month: "short",
                    })}{" "}
                    ·{" "}
                    {item.sourceUrl
                      ? new URL(item.sourceUrl).hostname
                      : "Manual query"}
                  </span>
                  <span>Candidate · sense unresolved</span>
                </footer>
              </article>
            ))}
          </div>
          <div className={view === "candidates" ? "cards" : "cards hidden"}>
            {candidates.map((item) => (
              <article
                className="lookup-card"
                data-testid="candidate-card"
                key={item.id}
              >
                <div className="card-head">
                  <h3>{item.text}</h3>
                  <span className="badge">{item.status}</span>
                </div>
                <p>
                  {item.encounterCount} encounter
                  {item.encounterCount === 1 ? "" : "s"} · {item.senseStatus}{" "}
                  sense
                </p>
                <p className="section-copy">
                  Review activation and scheduling will follow in the next
                  slice.
                </p>
              </article>
            ))}
          </div>
          {(view === "history" ? history : candidates).length === 0 && (
            <div className="empty">
              <span>↗</span>
              <h3>
                {connected
                  ? "Your next encounter starts here."
                  : "A little context goes a long way."}
              </h3>
              <p>
                {connected
                  ? "Open the selection playground, select “available”, and use the extension."
                  : "Connect the same local identity in the extension and this website to see your captured expressions."}
              </p>
              <a href="/sample.html" target="_blank" rel="noreferrer">
                Open selection playground →
              </a>
            </div>
          )}
          {view === "history" && cursor && (
            <button
              className="secondary"
              onClick={() => refresh(token.current, true)}
            >
              Load more
            </button>
          )}
        </section>
        <footer className="page-footer">
          CONTEXTSPEAK{" "}
          <span>Original English fixtures · local development slice</span>
        </footer>
      </main>
    </div>
  );
}
