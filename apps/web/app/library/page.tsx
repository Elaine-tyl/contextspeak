"use client";
import { aiLanguages } from "@contextspeak/contracts";
import Link from "next/link";
import { PracticeHub } from "./PracticeHub";
import { ReviewQueue } from "./ReviewQueue";
import { LearningNotes } from "./LearningNotes";
import {
  rememberLibraryScroll,
  libraryPosition,
  updateQuery,
  usePageQuery,
  useRestoreLibraryScroll,
} from "../../src/page-query";
import { withLibraryReturn } from "../../src/navigation";
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
  const { search, params } = usePageQuery();
  const requestedView = params.get("view");
  const view =
    requestedView === "candidates" ||
    requestedView === "reviews" ||
    requestedView === "practice"
      ? requestedView
      : "history";
  const filters = {
    q: (params.get("q") ?? "").slice(0, 200),
    sourceLanguage: params.get("sourceLanguage") ?? "",
    targetLanguage: params.get("targetLanguage") ?? "",
  };
  const activeFilters = useRef(filters);
  activeFilters.current = filters;
  const returnTo = "/library" + search;
  const [loadedPages, setLoadedPages] = useState(1);
  const [restorePages, setRestorePages] = useState(1);
  const [restoreFailed, setRestoreFailed] = useState(false);
  const listLoading = useRef(false);
  useEffect(() => {
    setRestorePages(libraryPosition(returnTo)?.pages ?? 1);
  }, [returnTo]);
  function applyFilters(next: typeof filters) {
    updateQuery(next);
  }
  function changeView(value: string) {
    updateQuery({ view: value === "history" ? "" : value }, true);
  }
  useRestoreLibraryScroll(
    returnTo,
    !loading &&
      (view === "history" || view === "candidates") &&
      (history.length > 0 || candidates.length > 0) &&
      (view !== "history" || !cursor || loadedPages >= restorePages),
  );
  useEffect(() => {
    historyRequest.current++;
    setHistory([]);
    setCandidates([]);
    setCursor(null);
    void refresh(token.current, false, true);
  }, [filters.q, filters.sourceLanguage, filters.targetLanguage]);
  useEffect(() => {
    if (
      connected &&
      view === "history" &&
      !loading &&
      !listLoading.current &&
      !restoreFailed &&
      cursor &&
      loadedPages < restorePages
    )
      void refresh(token.current, true);
  }, [
    connected,
    view,
    loading,
    cursor,
    loadedPages,
    restorePages,
    restoreFailed,
  ]);
  const filtered = !!(
    filters.q ||
    filters.sourceLanguage ||
    filters.targetLanguage
  );
  const [reviewBusy, setReviewBusy] = useState<string | null>(null);
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
    listLoading.current = true;
    setRestoreFailed(false);
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
      setLoadedPages((previous) => (append ? previous + 1 : 1));
      setCandidates(items.items);
      setEnabled(settings.enabled);
    } catch (e) {
      if (
        current === generation.current &&
        request === historyRequest.current
      ) {
        setRestoreFailed(append);
        setMessage(
          e instanceof Error ? e.message : "Could not load your library.",
        );
      }
    } finally {
      if (
        current === generation.current &&
        request === historyRequest.current
      ) {
        listLoading.current = false;
        setLoading(false);
      }
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
    setReviewBusy(null);
    setRestoreFailed(false);
    listLoading.current = false;
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
  async function updateReview(id: string, action: "enroll" | "pause") {
    const current = generation.current;
    setReviewBusy(id);
    try {
      await api(token.current, `/v1/reviews/${id}/${action}`, "POST");
      if (current !== generation.current) return;
      setMessage(
        action === "enroll"
          ? "Vocabulary added to your review queue."
          : "Reviews paused.",
      );
      if (action === "enroll") changeView("reviews");
      void refresh(token.current, false, true);
    } catch {
      if (current === generation.current)
        setMessage(
          "Could not update reviews. Check your connection and make sure the item has a saved translation.",
        );
    } finally {
      if (current === generation.current) setReviewBusy(null);
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
            onClick={() => changeView("history")}
          >
            ◷ Query history
          </button>
          <button
            className={view === "candidates" ? "nav active" : "nav"}
            onClick={() => changeView("candidates")}
          >
            ▤ Vocabulary candidates
          </button>
          <button
            className={view === "reviews" ? "nav active" : "nav"}
            onClick={() => changeView("reviews")}
          >
            Review queue
          </button>
          <button
            className={view === "practice" ? "nav active" : "nav"}
            onClick={() => changeView("practice")}
          >
            Listening &amp; speaking
          </button>
          <Link className="nav" href="/games">
            Game practice
          </Link>
        </nav>
        <div className="aside-note">
          <span className="status-dot" /> DEVELOPMENT
          <p>
            Capture today.
            <br />
            Review when due.
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
        <section className="intro compact-intro">
          <h1>
            Your English, <em>in context.</em>
          </h1>
          <p>
            Every useful expression has a story.
            <br />
            Keep the context. Build your vocabulary, one encounter at a time.
          </p>
        </section>
        {connected ? (
          <details className="connect-panel account-settings">
            <summary>Account &amp; extension settings</summary>
            <p>Signed in. Manage your extension connection below.</p>
            <a href="/pair">Pair an extension</a>{" "}
            <button className="link-button" onClick={revokeExtensions}>
              Revoke all extensions
            </button>
          </details>
        ) : (
          <section className="connect-panel">
            <p>
              Sign in to keep your library private and connect your extension.
            </p>
            <a className="primary" href={API + "/auth/login"}>
              Sign in with Google
            </a>
          </section>
        )}
        {message && (
          <p className="notice" role="status">
            {message}
          </p>
        )}
        {view !== "practice" && view !== "reviews" && (
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
              <p>Choose vocabulary for recall practice</p>
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
        )}
        {view === "practice" ? (
          connected ? (
            <PracticeHub key={identity} token={token.current} />
          ) : (
            <p>Sign in to practice saved vocabulary.</p>
          )
        ) : view === "reviews" ? (
          connected ? (
            <ReviewQueue
              key={identity}
              token={token.current}
              onChanged={() => {
                void refresh(token.current, false, true);
              }}
            />
          ) : (
            <p>Sign in to review saved vocabulary.</p>
          )
        ) : (
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
                  applyFilters({
                    q: "",
                    sourceLanguage: "",
                    targetLanguage: "",
                  })
                }
                disabled={!filtered}
              >
                Clear filters
              </button>
            </div>
            <div className={view === "history" ? "cards" : "cards hidden"}>
              {history.map((item) => (
                <article
                  className="lookup-card history-card"
                  data-testid="lookup-card"
                  key={item.id}
                >
                  <div
                    className="card-content"
                    tabIndex={0}
                    aria-label="Scrollable vocabulary card"
                  >
                    <div className="card-head">
                      <h3 title={item.text}>
                        <Link
                          className="card-link"
                          href={withLibraryReturn(
                            `/library/item/${item.candidateId}?lookup=${item.id}`,
                            returnTo,
                          )}
                          onClick={() =>
                            rememberLibraryScroll(
                              view === "history" ? loadedPages : 1,
                            )
                          }
                        >
                          {item.text}
                        </Link>
                      </h3>
                      <span className={"badge " + item.translationStatus}>
                        {item.translationStatus === "succeeded"
                          ? "Translated"
                          : item.translationStatus}
                      </span>
                    </div>
                    {item.meaning && <p className="meaning">{item.meaning}</p>}
                    {(item.context || item.learningDetails) && (
                      <details className="lookup-details">
                        <summary>
                          <span className="details-show">Show details</span>
                          <span className="details-hide">Hide details</span>
                        </summary>
                        {item.context && (
                          <blockquote>“{item.context}”</blockquote>
                        )}
                        <LearningNotes
                          value={item.learningDetails}
                          token={token.current}
                          sourceLanguage={
                            item.detectedSourceLanguage ?? item.sourceLanguage
                          }
                        />
                      </details>
                    )}
                    {item.errorCode && (
                      <div className="failure">
                        <p>
                          Translation unavailable ({item.errorCode}). This query
                          is saved. Check the local translator and retry.
                        </p>
                        <button
                          className="link-button"
                          onClick={() => retry(item.id)}
                        >
                          Retry translation
                        </button>
                      </div>
                    )}
                  </div>
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
                      {item.sourceLanguage} → {item.targetLanguage}
                    </span>
                  </footer>
                </article>
              ))}
            </div>
            <div className={view === "candidates" ? "cards" : "cards hidden"}>
              {candidates.map((item) => (
                <article
                  className="lookup-card candidate-card"
                  data-testid="candidate-card"
                  key={item.id}
                >
                  <div
                    className="card-content"
                    tabIndex={0}
                    aria-label="Scrollable vocabulary card"
                  >
                    <div className="card-head">
                      <h3 title={item.text}>
                        <Link
                          className="card-link"
                          href={withLibraryReturn(
                            `/library/item/${item.id}`,
                            returnTo,
                          )}
                          onClick={() =>
                            rememberLibraryScroll(
                              view === "history" ? loadedPages : 1,
                            )
                          }
                        >
                          {item.text}
                        </Link>
                      </h3>
                      <span className="badge">{item.status}</span>
                    </div>
                    <p>
                      {item.encounterCount} encounter
                      {item.encounterCount === 1 ? "" : "s"} ·{" "}
                      {item.senseStatus} sense
                    </p>
                    <p className="section-copy">
                      {item.sourceLanguage} → {item.targetLanguage}
                    </p>
                    <div className="review-actions">
                      {item.reviewDueAt ? (
                        <>
                          <button
                            className="secondary"
                            onClick={() => changeView("reviews")}
                          >
                            Open review queue
                          </button>
                          <button
                            className="link-button"
                            disabled={!!reviewBusy}
                            onClick={() => void updateReview(item.id, "pause")}
                          >
                            Pause reviews
                          </button>
                        </>
                      ) : (
                        <button
                          className="secondary"
                          disabled={!!reviewBusy}
                          onClick={() => void updateReview(item.id, "enroll")}
                        >
                          {reviewBusy === item.id
                            ? "Saving…"
                            : "Start reviewing"}
                        </button>
                      )}
                    </div>
                    <p className="section-copy">
                      {item.reviewDueAt
                        ? `Next review: ${new Date(item.reviewDueAt).toLocaleString("en-GB")}`
                        : "Choose this item for spaced repetition when you’re ready."}
                    </p>
                  </div>
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
        )}
        <footer className="page-footer">
          CONTEXTSPEAK <span>Personal vocabulary · development</span>
        </footer>
      </main>
    </div>
  );
}
