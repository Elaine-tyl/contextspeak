"use client";
import { aiLanguages } from "@contextspeak/contracts";
import { LearningNotes } from "./LearningNotes";
import { useEffect, useRef, useState } from "react";
import type {
  Candidate,
  CollectionSettings,
  LookupRecord,
  Page,
} from "@contextspeak/contracts";
import { toggleCollection } from "../../src/collection";
import { API, api, type WebSession } from "../../src/api";
import "./library.css";
export default function Library() {
  const [connected, setConnected] = useState(false);
  const [identity, setIdentity] = useState("");
  const [history, setHistory] = useState<LookupRecord[]>([]);
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [view, setView] = useState<"history" | "candidates">("history");
  const [filters, setFilters] = useState({
    q: "",
    sourceLanguage: "",
    targetLanguage: "",
  });
  const activeFilters = useRef(filters);
  function applyFilters(next: typeof filters) {
    activeFilters.current = next;
    setFilters(next);
    historyRequest.current++;
    setHistory([]);
    setCandidates([]);
    setCursor(null);
    void refresh(token.current, false, true);
  }
  const filtered = !!(
    filters.q ||
    filters.sourceLanguage ||
    filters.targetLanguage
  );
  const blockedSession = useRef(false);
  const token = useRef("");
  const generation = useRef(0);
  const browsingMore = useRef(false);
  const historyRequest = useRef(0);
  async function refresh(value = token.current, append = false, reset = false) {
    if (!value || (!append && browsingMore.current && !reset)) return;
    if (append) browsingMore.current = true;
    if (reset) browsingMore.current = false;
    const request = ++historyRequest.current;
    const current = generation.current;
    setLoading(true);
    try {
      const query = new URLSearchParams();
      for (const [key, field] of Object.entries(activeFilters.current))
        if (field.trim()) query.set(key, field.trim());
      const itemQuery = query.toString();
      if (append && cursor) query.set("cursor", cursor);
      const [lookups, items, settings] = await Promise.all([
        api<Page<LookupRecord>>(
          value,
          "/v1/lookups" + (query.size ? "?" + query.toString() : ""),
        ),
        api<Page<Candidate>>(
          value,
          "/v1/learning-items" + (itemQuery ? "?" + itemQuery : ""),
        ),
        api<CollectionSettings>(value, "/v1/settings"),
      ]);
      if (current !== generation.current || request !== historyRequest.current)
        return;
      setHistory((previous) =>
        append ? [...previous, ...lookups.items] : lookups.items,
      );
      setCursor(lookups.nextCursor);
      setCandidates(items.items);
      setEnabled(settings.enabled);
    } catch (e) {
      if (current === generation.current && request === historyRequest.current)
        setMessage(
          e instanceof Error ? e.message : "Could not load your library.",
        );
    } finally {
      if (current === generation.current && request === historyRequest.current)
        setLoading(false);
    }
  }
  function clearSession() {
    generation.current++;
    token.current = "";
    browsingMore.current = false;
    setHistory([]);
    setCandidates([]);
    setCursor(null);
    setConnected(false);
    setIdentity("");
    setEnabled(false);
    setLoading(false);
  }
  async function connect() {
    if (blockedSession.current) return;
    const current = generation.current;
    try {
      const verified = await api<WebSession>("", "/auth/session");
      if (current !== generation.current) return;
      if (token.current && token.current !== verified.csrfToken) {
        blockedSession.current = true;
        clearSession();
        setMessage("The signed-in account changed. Reload to continue.");
        return;
      }
      token.current = verified.csrfToken;
      setIdentity(verified.identity.subject);
      setConnected(true);
      await refresh();
    } catch {
      if (current === generation.current) clearSession();
    }
  }
  useEffect(() => {
    sessionStorage.removeItem("contextspeak-token");
    void connect();
    const timer = setInterval(() => {
      if (document.visibilityState === "visible") void connect();
    }, 3000);
    return () => clearInterval(timer);
  }, []);
  async function toggle() {
    try {
      const applied = await toggleCollection({
        session: () => ({
          token: token.current,
          generation: generation.current,
        }),
        get: (value) => api<CollectionSettings>(value, "/v1/settings"),
        put: (value, settings) =>
          api<CollectionSettings>(value, "/v1/settings", "PUT", settings),
        apply: (value) => setEnabled(value.enabled),
      });
      if (applied)
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
  async function disconnect() {
    blockedSession.current = true;
    const previous = token.current;
    clearSession();
    try {
      await api(previous, "/auth/logout", "POST");
      setMessage(
        "Signed out. Paired extensions remain connected until revoked.",
      );
    } catch {
      setMessage("Could not confirm sign-out. Try signing in again.");
    }
  }
  async function revokeExtensions() {
    try {
      await api(token.current, "/auth/extensions/revoke", "POST");
      setMessage("All paired extensions revoked. Pair again to reconnect.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Revocation failed.");
    }
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
          <span className="status-dot" /> DEVELOPMENT
          <p>
            Capture today.
            <br />
            Practice comes next.
          </p>
          <small>Your everyday language learning.</small>
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
                Sign out
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
        <section className="connect-panel">
          {connected ? (
            <>
              <p>Signed in · {identity}</p>
              <a href="/pair">Pair an extension</a>{" "}
              <button className="link-button" onClick={revokeExtensions}>
                Revoke all extensions
              </button>
            </>
          ) : (
            <>
              <p>
                Sign in to keep your library private and connect your extension.
              </p>
              <a className="primary" href={API + "/auth/login"}>
                Sign in with Google
              </a>
            </>
          )}
        </section>
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
              onClick={() => refresh(token.current, false, true)}
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
          <div
            className="library-filters"
            role="search"
            aria-label="Filter library"
          >
            <label>
              Search
              <input
                type="search"
                aria-label="Search library"
                maxLength={200}
                value={filters.q}
                placeholder={
                  view === "history"
                    ? "Words, context or translations"
                    : "Words or phrases"
                }
                onChange={(event) =>
                  applyFilters({ ...filters, q: event.target.value })
                }
              />
            </label>
            <label>
              Source language
              <select
                aria-label="Source language filter"
                value={filters.sourceLanguage}
                onChange={(event) =>
                  applyFilters({
                    ...filters,
                    sourceLanguage: event.target.value,
                  })
                }
              >
                <option value="">All source languages</option>
                <option value="auto">Auto-detected queries</option>
                {aiLanguages.map((language) => (
                  <option key={language.code} value={language.code}>
                    {language.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Target language
              <select
                aria-label="Target language filter"
                value={filters.targetLanguage}
                onChange={(event) =>
                  applyFilters({
                    ...filters,
                    targetLanguage: event.target.value,
                  })
                }
              >
                <option value="">All target languages</option>
                {aiLanguages.map((language) => (
                  <option key={language.code} value={language.code}>
                    {language.name}
                  </option>
                ))}
              </select>
            </label>
            <button
              className="secondary"
              onClick={() =>
                applyFilters({ q: "", sourceLanguage: "", targetLanguage: "" })
              }
              disabled={!filtered}
            >
              Clear filters
            </button>
          </div>
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
                      ? "Translated"
                      : item.translationStatus}
                  </span>
                </div>
                {item.context && <blockquote>“{item.context}”</blockquote>}
                {item.meaning && <p className="meaning">{item.meaning}</p>}
                <LearningNotes value={item.learningDetails} />
                {item.errorCode && (
                  <div className="failure">
                    <p>
                      Translation unavailable ({item.errorCode}). This query is
                      saved. Check the local translator and retry.
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
                  <span>
                    {item.sourceLanguage} → {item.targetLanguage} · Candidate ·
                    sense unresolved
                  </span>
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
                  {item.sourceLanguage} → {item.targetLanguage}
                </p>
                <p className="section-copy">
                  Review activation and scheduling will follow in the next
                  slice.
                </p>
              </article>
            ))}
          </div>
          {loading && <p aria-live="polite">Loading library…</p>}
          {!loading &&
          filtered &&
          (view === "history" ? history : candidates).length === 0 ? (
            <div className="empty" role="status">
              <h3>No matching vocabulary.</h3>
              <p>Try another search or clear the language filters.</p>
            </div>
          ) : (
            !loading &&
            (view === "history" ? history : candidates).length === 0 && (
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
            )
          )}
          {view === "history" && cursor && (
            <button
              className="secondary"
              onClick={() => refresh(token.current, true)}
              disabled={loading}
            >
              Load more
            </button>
          )}
        </section>
        <footer className="page-footer">
          CONTEXTSPEAK <span>Local translation · development</span>
        </footer>
      </main>
    </div>
  );
}
