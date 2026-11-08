"use client";
import { useEffect, useRef, useState } from "react";
import type { ReviewReceipt, VocabularyDetail } from "@contextspeak/contracts";
import { API, api, ApiError, type WebSession } from "../../src/api";
import { LearningNotes } from "./LearningNotes";
import "./library.css";
export function VocabularyDetailPage({
  id,
  lookupId,
}: {
  id: string;
  lookupId?: string;
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
  return (
    <main className="vocabulary-detail">
      <a href={lookupId ? "/library" : "/library?view=candidates"}>
        Back to library
      </a>
      {message && <p role="alert">{message}</p>}
      {!token && !loading && (
        <a className="primary" href={API + "/auth/login"}>
          Sign in with Google
        </a>
      )}
      {loading && !detail && <p role="status">Loading vocabulary…</p>}
      {detail && (
        <>
          <div className="section-head">
            <h1>{lookup?.text ?? detail.item.text}</h1>
            <button
              className="secondary"
              disabled={loading}
              onClick={() => reload.current?.()}
            >
              Refresh
            </button>
          </div>
          <p className="section-copy">
            {detail.item.sourceLanguage} → {detail.item.targetLanguage}
          </p>
          <article className="lookup-card">
            <span
              className={"badge " + (lookup?.translationStatus ?? "pending")}
            >
              {lookup?.translationStatus ?? "No translation yet"}
            </span>
            {lookup?.meaning && <p className="meaning">{lookup.meaning}</p>}
            {lookup?.context && <blockquote>{lookup.context}</blockquote>}
            <LearningNotes value={lookup?.learningDetails} />
            {lookup?.translationStatus === "pending" && (
              <p role="status">Translation is processing…</p>
            )}
            {lookup?.errorCode && (
              <p>
                Translation unavailable ({lookup.errorCode}). Your lookup is
                saved.
              </p>
            )}
            {lookup?.sourceUrl && (
              <p>
                <a href={lookup.sourceUrl} target="_blank" rel="noreferrer">
                  Open original webpage
                </a>
              </p>
            )}
          </article>
          <div className="detail-actions">
            {lookup?.translationStatus === "succeeded" && lookup.meaning && (
              <a
                className="secondary"
                href={`/practice/${encodeURIComponent(id)}?lookup=${encodeURIComponent(lookup.id)}`}
              >
                Practice listening and speaking
              </a>
            )}
            {detail.item.reviewDueAt ? (
              <a className="secondary" href="/library?view=reviews">
                Open review queue
              </a>
            ) : (
              <button
                className="primary"
                disabled={busy || !lookup?.meaning}
                onClick={() => void enroll()}
              >
                Start reviewing
              </button>
            )}
          </div>
        </>
      )}
    </main>
  );
}
