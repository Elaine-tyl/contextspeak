"use client";
import { aiLanguages } from "@contextspeak/contracts";
import Link from "next/link";
import { PracticeHub } from "./PracticeHub";
import { ReviewQueue } from "./ReviewQueue";
import { LearningSculpture } from "../LearningSculpture";
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
  const isList = view === "history" || view === "candidates";
  const title =
    view === "reviews"
      ? "Review"
      : view === "practice"
        ? "Listen & speak"
        : "Library";
  return (
    <main
      className={
        "page library-page" +
        (view === "reviews"
          ? " tone-review"
          : view === "practice"
            ? " tone-speak"
            : "")
      }
    >
      <div className="page-head depth-hero">
        <div className="hero-copy">
          <h1>{title}</h1>
          <div className="connection" aria-live="polite">
            <span className={"status-dot " + (connected ? "online" : "")} />
            <span>{connected ? "Connected" : "Not connected"}</span>
            {connected && (
              <button className="link-button" onClick={disconnect}>
                Sign out
              </button>
            )}
          </div>
        </div>
        <LearningSculpture />
      </div>
      {connected ? (
        isList && (
          <details className="connect-panel account-settings">
            <summary>Account &amp; extension settings</summary>
            <div className="settings-actions">
              <a className="secondary" href="/pair">
                Pair an extension
              </a>
              <button className="link-button" onClick={revokeExtensions}>
                Revoke all extensions
              </button>
            </div>
          </details>
        )
      ) : (
        <section className="connect-panel sign-in-panel">
          <div>
            <h2>Sign in to see your words</h2>
            <p>
              Your library is private. Sign in, then connect the browser
              extension to start collecting words.
            </p>
          </div>
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
      {isList && connected && (
        <section className="stats" aria-label="Library summary">
          <article>
            <span>Lookups</span>
            <strong>
              {history.length}
              {cursor ? "+" : ""}
            </strong>
          </article>
          <article>
            <span>Words</span>
            <strong>{candidates.length}</strong>
          </article>
          <article className="collection">
            <span>Auto-save lookups</span>
            <strong>{enabled ? "On" : "Off"}</strong>
            <button className="link-button" onClick={toggle}>
              {enabled ? "Pause collection" : "Enable collection"}
            </button>
          </article>
        </section>
      )}
      {view === "practice" ? (
        connected ? (
          <PracticeHub key={identity} token={token.current} />
        ) : (
          <p className="empty-line">Sign in to practise your saved words.</p>
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
          <p className="empty-line">Sign in to review your saved words.</p>
        )
      ) : (
        <section className="library-section">
          <h2 className="sr-only">
            {view === "history" ? "Recent lookups" : "Word list"}
          </h2>
          <div className="library-toolbar">
            <div className="segmented" role="group" aria-label="Library view">
              <button
                aria-pressed={view === "history"}
                onClick={() => changeView("history")}
              >
                Recent lookups
              </button>
              <button
                aria-pressed={view === "candidates"}
                onClick={() => changeView("candidates")}
              >
                Word list
              </button>
            </div>
            <button
              className="secondary"
              onClick={() => refresh(token.current, false, true)}
              disabled={!connected}
            >
              Refresh
            </button>
          </div>
          <div
            className="library-filters"
            role="search"
            aria-label="Filter library"
          >
            <label className="filter-search">
              <span className="sr-only">Search</span>
              <input
                type="search"
                aria-label="Search library"
                maxLength={200}
                value={filters.q}
                placeholder={
                  view === "history"
                    ? "Search words, sentences or translations"
                    : "Search words or phrases"
                }
                onChange={(event) =>
                  applyFilters({ ...filters, q: event.target.value })
                }
              />
            </label>
            <label>
              From
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
                <option value="">Any language</option>
                <option value="auto">Auto-detected</option>
                {aiLanguages.map((language) => (
                  <option key={language.code} value={language.code}>
                    {language.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              To
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
                <option value="">Any language</option>
                {aiLanguages.map((language) => (
                  <option key={language.code} value={language.code}>
                    {language.name}
                  </option>
                ))}
              </select>
            </label>
            {filtered && (
              <button
                className="link-button"
                onClick={() =>
                  applyFilters({
                    q: "",
                    sourceLanguage: "",
                    targetLanguage: "",
                  })
                }
              >
                Clear filters
              </button>
            )}
          </div>
          <ul
            className={
              view === "history" ? "vocabulary-list" : "vocabulary-list hidden"
            }
            aria-label="Recent lookups"
          >
            {history.map((item) => (
              <li
                className="lookup-card history-card"
                data-testid="lookup-card"
                key={item.id}
              >
                <div className="card-content">
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
                    {item.translationStatus !== "succeeded" && (
                      <span className={"badge " + item.translationStatus}>
                        {item.translationStatus === "pending"
                          ? "Translating…"
                          : "Not translated"}
                      </span>
                    )}
                    <span className="card-open" aria-hidden="true">
                      ↗
                    </span>
                  </div>
                  {item.meaning && <p className="meaning">{item.meaning}</p>}
                  {item.errorCode && (
                    <div className="failure">
                      <p title={item.errorCode}>
                        Translation unavailable. Lookup saved.
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
              </li>
            ))}
          </ul>
          <ul
            className={
              view === "candidates"
                ? "vocabulary-list"
                : "vocabulary-list hidden"
            }
            aria-label="Word list"
          >
            {candidates.map((item) => (
              <li
                className="lookup-card candidate-card"
                data-testid="candidate-card"
                key={item.id}
              >
                <div className="card-content">
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
                    {item.reviewDueAt && (
                      <span className="badge due">In review</span>
                    )}
                    <span className="card-open" aria-hidden="true">
                      ↗
                    </span>
                  </div>
                  {item.reviewDueAt && (
                    <p className="candidate-meta">
                      {`Next review ${new Date(item.reviewDueAt).toLocaleString(
                        "en-GB",
                        {
                          day: "numeric",
                          month: "short",
                          hour: "2-digit",
                          minute: "2-digit",
                        },
                      )}`}
                    </p>
                  )}
                  <div className="review-actions">
                    {item.reviewDueAt ? (
                      <>
                        <button
                          className="secondary tone-review"
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
                        className="secondary tone-review"
                        disabled={!!reviewBusy}
                        onClick={() => void updateReview(item.id, "enroll")}
                      >
                        {reviewBusy === item.id ? "Saving…" : "Start reviewing"}
                      </button>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>
          {loading && (
            <p className="empty-line" aria-live="polite">
              Loading your library…
            </p>
          )}
          {!loading &&
          filtered &&
          (view === "history" ? history : candidates).length === 0 ? (
            <div className="empty" role="status">
              <h3>No matching vocabulary.</h3>
              <p>Try a different search, or clear the filters.</p>
            </div>
          ) : (
            !loading &&
            connected &&
            (view === "history" ? history : candidates).length === 0 && (
              <div className="empty">
                <h3>No words yet — let’s look one up.</h3>
                <ol className="empty-steps">
                  <li>Install and pair the ContextSpeak browser extension.</li>
                  <li>Select any English word on a web page.</li>
                  <li>It appears here with its translation and sentence.</li>
                </ol>
                <a
                  className="secondary"
                  href="/sample.html"
                  target="_blank"
                  rel="noreferrer"
                >
                  Try it on a practice page
                </a>
              </div>
            )
          )}
          {view === "history" && cursor && (
            <div className="load-more">
              <button
                className="secondary"
                onClick={() => refresh(token.current, true)}
                disabled={loading}
              >
                Load more
              </button>
            </div>
          )}
        </section>
      )}
    </main>
  );
}
