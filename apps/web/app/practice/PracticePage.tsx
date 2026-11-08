"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import type {
  PracticeTask,
  PracticeSubmission,
  PracticeReceipt,
} from "@contextspeak/contracts";
import { api, ApiError, API, type WebSession } from "../../src/api";
import { PracticeSession } from "./PracticeSession";
import "../library/library.css";
import "./practice.css";
export function PracticePage({
  id,
  lookupId,
}: {
  id: string;
  lookupId?: string;
}) {
  const [task, setTask] = useState<PracticeTask | null>(null);
  const [token, setToken] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const [mode, setMode] = useState<PracticeSubmission["mode"]>("listening");
  const [round, setRound] = useState(0);
  const [locked, setLocked] = useState(false);
  function expire(message = "Your session expired or changed. Sign in again.") {
    setTask(null);
    setToken("");
    setLocked(false);
    setError(message);
  }
  useEffect(() => {
    let active = true;
    let stopped = false;
    let inFlight = false;
    let sessionToken = "";
    let loaded = false;
    let expiry: ReturnType<typeof setTimeout>;
    setTask(null);
    setLoading(true);
    setError("");
    setLocked(false);
    async function load() {
      if (!active || stopped || inFlight) return;
      inFlight = true;
      try {
        const session = await api<WebSession>("", "/auth/session");
        if (!active) return;
        if (sessionToken && sessionToken !== session.csrfToken) {
          stopped = true;
          expire();
          return;
        }
        sessionToken = session.csrfToken;
        const remaining = Date.parse(session.expiresAt) - Date.now();
        if (remaining <= 0) {
          stopped = true;
          expire();
          return;
        }
        clearTimeout(expiry);
        if (Number.isFinite(remaining))
          expiry = setTimeout(
            () => {
              stopped = true;
              expire();
            },
            Math.min(remaining, 2147483647),
          );
        setToken(sessionToken);
        if (!loaded) {
          const query = lookupId
            ? `?lookup=${encodeURIComponent(lookupId)}`
            : "";
          const value = await api<PracticeTask>(
            sessionToken,
            `/v1/practice/${encodeURIComponent(id)}${query}`,
          );
          if (!active || stopped) return;
          setTask(value);
          loaded = true;
          setError("");
        }
      } catch (error) {
        if (!active) return;
        if (error instanceof ApiError && [401, 403].includes(error.status)) {
          stopped = true;
          expire();
        } else if (!loaded)
          setError(
            "Practice unavailable. Choose a successful saved translation, or check your connection and retry.",
          );
      } finally {
        inFlight = false;
        if (active) setLoading(false);
      }
    }
    void load();
    const timer = setInterval(() => void load(), 3000);
    return () => {
      active = false;
      clearInterval(timer);
      clearTimeout(expiry);
    };
  }, [id, lookupId, retry]);
  function saved(receipt: PracticeReceipt) {
    setTask((previous) =>
      previous
        ? {
            ...previous,
            recent: [
              receipt,
              ...previous.recent.filter((value) => value.id !== receipt.id),
            ].slice(0, 10),
          }
        : null,
    );
  }
  const back = `/library/item/${encodeURIComponent(id)}${lookupId ? `?lookup=${encodeURIComponent(lookupId)}` : ""}`;
  return (
    <main className="vocabulary-detail practice-page">
      <Link href={back}>Back to vocabulary</Link>
      <h1>Listening and speaking</h1>
      <p className="section-copy">
        Practice your saved vocabulary with on-device voices and local
        recording. Written answers and self-checks are saved separately from
        vocabulary recall; no audio is uploaded.
      </p>
      {error && <p role="alert">{error}</p>}
      {loading && !task && <p role="status">Loading practice…</p>}
      {!task && !loading && (
        <>
          <button
            className="secondary"
            onClick={() => setRetry((value) => value + 1)}
          >
            Retry loading practice
          </button>
          {!token && <a href={API + "/auth/login"}>Sign in with Google</a>}
        </>
      )}
      {task && (
        <>
          <div className="practice-tabs" aria-label="Practice mode">
            {(["listening", "speaking"] as const).map((value) => (
              <button
                className={mode === value ? "primary" : "secondary"}
                key={value}
                aria-pressed={mode === value}
                disabled={locked}
                onClick={() => setMode(value)}
              >
                {value === "listening" ? "Listening" : "Speaking"}
              </button>
            ))}
            <button
              className="secondary"
              disabled={locked}
              onClick={() => setRound((value) => value + 1)}
            >
              New attempt
            </button>
          </div>
          <PracticeSession
            key={`${task.lookupId}:${mode}:${round}`}
            task={task}
            token={token}
            mode={mode}
            onSaved={saved}
            onLocked={setLocked}
            onExpired={expire}
            onDiscard={() => {
              setLocked(false);
              setRound((value) => value + 1);
            }}
          />
          <section aria-label="Recent practice">
            <h2>Recent practice</h2>
            {task.recent.length ? (
              <ul>
                {task.recent.map((value) => (
                  <li key={value.id}>
                    {value.mode === "listening" ? "Listening" : "Speaking"} ·{" "}
                    {value.selfCheck === "comfortable"
                      ? "Comfortable"
                      : "Needs practice"}{" "}
                    · {value.revealed ? "Text shown" : "Text hidden"} ·{" "}
                    {new Date(value.createdAt).toLocaleString()}
                  </li>
                ))}
              </ul>
            ) : (
              <p>No practice results saved for this encounter yet.</p>
            )}
          </section>
        </>
      )}
    </main>
  );
}
