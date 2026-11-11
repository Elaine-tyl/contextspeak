"use client";
import { useEffect, useRef, useState } from "react";
import type { ReviewReceipt, VocabularyDetail } from "@contextspeak/contracts";
import { API, api, ApiError, type WebSession } from "../../src/api";
import { learningReturn, withLibraryReturn } from "../../src/navigation";
import { SpeakerButton } from "./SpeakerButton";
import { LearningNotes } from "./LearningNotes";
import "./library.css";
import { hostname, languagePair } from "../../src/languages";
export function VocabularyDetailPage({
  id,
  lookupId,
  returnTo,
}: {
  id: string;
  lookupId?: string;
  returnTo?: string;
}) {
  const [detail, setDetail] = useState<VocabularyDetail | null>(null);
  const [token, setToken] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const generation = useRef(0);
  const reload = useRef<(() => void) | null>(null);
  useEffect(() => {
    let active = true;
    let stopped = false;
    let inFlight = false;
    let sessionToken = "";
    let loaded = false;
    let pending = false;
    setDetail(null);
    setToken("");
    setBusy(false);
    setMessage("");
    setLoading(true);
    async function load(force: boolean) {
      if (!active || stopped || inFlight) return;
      inFlight = true;
      try {
        const session = await api<WebSession>("", "/auth/session");
        if (!active) return;
        if (sessionToken && sessionToken !== session.csrfToken) {
          stopped = true;
          generation.current++;
          setDetail(null);
          setToken("");
          setBusy(false);
          setMessage(
            "The signed-in account changed. Return to the library to continue.",
          );
          return;
        }
        sessionToken = session.csrfToken;
        setToken(sessionToken);
        setMessage("");
        if (!force && loaded && !pending) return;
        setLoading(true);
        const query = lookupId ? "?lookup=" + encodeURIComponent(lookupId) : "";
        const value = await api<VocabularyDetail>(
          sessionToken,
          `/v1/learning-items/${encodeURIComponent(id)}${query}`,
        );
        if (!active) return;
        setDetail(value);
        loaded = true;
        pending = value.lookup?.translationStatus === "pending";
      } catch (error) {
        if (!active) return;
        if (error instanceof ApiError && error.status === 401) {
          stopped = true;
          generation.current++;
          setDetail(null);
          setToken("");
          setBusy(false);
          setMessage("Sign in to view your vocabulary.");
        } else {
          if (error instanceof ApiError && error.status === 404)
            setDetail(null);
          setMessage(
            "Could not open this vocabulary. Check your connection and account, then refresh.",
          );
        }
      } finally {
        inFlight = false;
        if (active) setLoading(false);
      }
    }
    reload.current = () => {
      void load(true);
    };
    void load(true);
    // Match library privacy checks; only pending translations need to be fetched again.
    const timer = setInterval(() => {
      void load(false);
    }, 3000);
    return () => {
      active = false;
      generation.current++;
      clearInterval(timer);
      reload.current = null;
    };
  }, [id, lookupId]);
  async function enroll() {
    const current = generation.current;
    setBusy(true);
    try {
      const receipt = await api<ReviewReceipt>(
        token,
        `/v1/reviews/${encodeURIComponent(id)}/enroll`,
        "POST",
        detail?.lookup ? { lookupId: detail.lookup.id } : undefined,
      );
      if (current !== generation.current) return;
      setDetail((previous) =>
        previous
          ? {
              ...previous,
              item: { ...previous.item, reviewDueAt: receipt.dueAt },
            }
          : null,
      );
      setMessage("Added to your review queue.");
    } catch {
      if (current === generation.current)
        setMessage(
          "Could not start reviews. A successful saved translation is required. Check your connection and retry.",
        );
    } finally {
      if (current === generation.current) setBusy(false);
    }
  }
  const lookup = detail?.lookup;
  const actions = detail && (
    <div className="detail-actions">
      {detail.item.reviewDueAt ? (
        <a className="secondary tone-review" href="/library?view=reviews">
          Open review queue
        </a>
      ) : (
        <button
          className="primary tone-review"
          disabled={busy || !lookup?.meaning}
          onClick={() => void enroll()}
        >
          Start reviewing
        </button>
      )}
      {lookup?.translationStatus === "succeeded" && lookup.meaning && (
        <a
          className="secondary tone-speak"
          href={withLibraryReturn(
            `/practice/${encodeURIComponent(id)}?lookup=${encodeURIComponent(lookup.id)}`,
            learningReturn(returnTo),
          )}
        >
          Practice listening and speaking
        </a>
      )}
    </div>
  );
  return (
    <main className="page page-narrow vocabulary-detail">
      <a
        className="back-link"
        href={learningReturn(
          returnTo,
          lookupId ? "/library" : "/library?view=candidates",
        )}
      >
        {returnTo === "/progress" ? "Back to progress" : "Back to library"}
      </a>
      {message && (
        <p className="notice" role="alert">
          {message}
        </p>
      )}
      {!token && !loading && (
        <section className="connect-panel sign-in-panel">
          <div>
            <h2>Sign in to see this word</h2>
            <p>Your saved words are private to your account.</p>
          </div>
          <a className="primary" href={API + "/auth/login"}>
            Sign in with Google
          </a>
        </section>
      )}
      {loading && !detail && <p role="status">Loading vocabulary…</p>}
      {detail && (
        <>
          <div className="section-head">
            <div>
              <div className="vocabulary-title">
                <h1>{lookup?.text ?? detail.item.text}</h1>
                <SpeakerButton
                  key={lookup?.id ?? id}
                  token={token}
                  text={lookup?.text ?? detail.item.text}
                  language={
                    lookup?.detectedSourceLanguage ??
                    (detail.item.sourceLanguage === "auto"
                      ? null
                      : detail.item.sourceLanguage)
                  }
                />
              </div>
              <p className="detail-meta">
                {languagePair(
                  lookup?.detectedSourceLanguage ?? detail.item.sourceLanguage,
                  detail.item.targetLanguage,
                )}{" "}
                · looked up {detail.item.encounterCount}{" "}
                {detail.item.encounterCount === 1 ? "time" : "times"}
              </p>
            </div>
            <button
              className="link-button"
              disabled={loading}
              onClick={() => reload.current?.()}
            >
              Refresh
            </button>
          </div>
          {actions}
          <article className="lookup-card detail-card">
            <span
              className={"badge " + (lookup?.translationStatus ?? "pending")}
            >
              {lookup?.translationStatus === "succeeded"
                ? "Translated"
                : lookup?.translationStatus === "failed"
                  ? "Translation unavailable"
                  : lookup?.translationStatus === "pending"
                    ? "Translating…"
                    : "No translation yet"}
            </span>
            {lookup?.meaning && <p className="meaning">{lookup.meaning}</p>}
            {lookup?.context && (
              <figure className="detail-context">
                <blockquote>{lookup.context}</blockquote>
                <figcaption>
                  Where you found it:{" "}
                  {lookup.sourceUrl ? (
                    <a href={lookup.sourceUrl} target="_blank" rel="noreferrer">
                      {hostname(lookup.sourceUrl)} ↗
                    </a>
                  ) : (
                    "typed manually"
                  )}
                </figcaption>
              </figure>
            )}
            {lookup?.translationStatus === "pending" && (
              <p role="status">Translation is processing…</p>
            )}
            {lookup?.errorCode && (
              <p className="failure" title={lookup.errorCode}>
                We couldn’t translate this yet. Your lookup is saved — try
                Refresh in a moment.
              </p>
            )}
            <LearningNotes
              value={lookup?.learningDetails}
              token={token}
              sourceLanguage={
                lookup?.detectedSourceLanguage ?? detail.item.sourceLanguage
              }
            />
            {lookup?.sourceUrl && !lookup.context && (
              <p>
                <a href={lookup.sourceUrl} target="_blank" rel="noreferrer">
                  Open original webpage
                </a>
              </p>
            )}
          </article>
        </>
      )}
    </main>
  );
}
