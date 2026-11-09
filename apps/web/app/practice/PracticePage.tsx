"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import {
  conversationScenarios,
  type ScenarioId,
} from "@contextspeak/contracts";
import type {
  PracticeTask,
  PracticeSubmission,
  PracticeReceipt,
} from "@contextspeak/contracts";
import { api, ApiError, API, type WebSession } from "../../src/api";
import {
  libraryReturn,
  withLibraryReturn,
  practiceReturn,
} from "../../src/navigation";
import { updateQuery } from "../../src/page-query";
import { DialogueSession } from "./DialogueSession";
import { PracticeSession } from "./PracticeSession";
import "../library/library.css";
import "./practice.css";
export function PracticePage({
  id,
  lookupId,
  scenarioId,
  returnTo = "/library",
}: {
  id: string;
  lookupId?: string;
  scenarioId?: ScenarioId;
  returnTo?: string;
}) {
  const [scenario, setScenario] = useState<ScenarioId | "">(scenarioId ?? "");
  useEffect(() => {
    const restore = () =>
      setScenario(
        conversationScenarios.find(
          (value) =>
            value.id ===
            new URLSearchParams(window.location.search).get("scenario"),
        )?.id ?? "",
      );
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);
  const [task, setTask] = useState<PracticeTask | null>(null);
  const [token, setToken] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const [mode, setMode] = useState<PracticeSubmission["mode"] | "conversation">(
    "listening",
  );
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
          const query = new URLSearchParams();
          if (lookupId) query.set("lookup", lookupId);
          if (scenario) query.set("scenario", scenario);
          const value = await api<PracticeTask>(
            sessionToken,
            `/v1/practice/${encodeURIComponent(id)}?${query}`,
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
            scenario
              ? "Scenario unavailable. Choose a successful saved English translation with a known English source language, or check your connection and retry."
              : "Practice unavailable. Choose a successful saved translation, or check your connection and retry.",
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
  }, [id, lookupId, scenario, retry]);
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
  const back = withLibraryReturn(
    `/library/item/${encodeURIComponent(id)}${lookupId ? `?lookup=${encodeURIComponent(lookupId)}` : ""}`,
    libraryReturn(returnTo),
  );
  return (
    <main className="vocabulary-detail practice-page">
      <Link href={back}>Back to vocabulary</Link>
      <h1>Listening and speaking</h1>
      <p className="section-copy">
        Practice with cached AI voices and local recording. Submit what you hear
        for dictation marks and corrections. Text is sent to Google for AI
        audio; microphone recordings stay in this tab unless you consent and
        submit for AI assessment. Results stay separate from vocabulary recall.
      </p>
      {token && (
        <label className="scenario-picker">
          Conversation scenario
          <select
            value={scenario}
            disabled={locked}
            onChange={(event) => {
              setTask(null);
              setLoading(true);
              setMode("listening");
              setRound(0);
              setScenario(event.target.value as ScenarioId | "");
              updateQuery({ scenario: event.target.value }, true);
            }}
          >
            <option value="">Saved example</option>
            {conversationScenarios.map((value) => (
              <option key={value.id} value={value.id}>
                {value.title}
              </option>
            ))}
          </select>
        </label>
      )}
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
          {task.scenario && (
            <section className="scenario-brief" aria-label="Conversation goal">
              <h2>{task.scenario.title}</h2>
              <p>{task.scenario.goal}</p>
              <p className="section-copy">
                Curated English role-play · reply in your own words. The meaning
                guide is in English.
              </p>
            </section>
          )}
          <div className="practice-tabs" aria-label="Practice mode">
            {(
              [
                "listening",
                "speaking",
                ...(task.scenario ? ["conversation" as const] : []),
              ] as const
            ).map((value) => (
              <button
                className={mode === value ? "primary" : "secondary"}
                key={value}
                aria-pressed={mode === value}
                disabled={locked}
                onClick={() => setMode(value)}
              >
                {value === "listening"
                  ? "Listening"
                  : value === "speaking"
                    ? "Speaking"
                    : "Conversation"}
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
          {mode === "conversation" ? (
            <DialogueSession
              key={`${task.lookupId}:${task.scenario?.id}:${round}`}
              task={task}
              token={token}
              onSaved={saved}
              onLocked={setLocked}
              onExpired={expire}
            />
          ) : (
            <PracticeSession
              key={`${task.lookupId}:${task.scenario?.id ?? "example"}:${mode}:${round}`}
              task={task}
              nextLibrary={practiceReturn(returnTo)}
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
          )}
          <section aria-label="Recent practice">
            <h2>Recent practice</h2>
            {task.recent.length ? (
              <ul>
                {task.recent.map((value) => (
                  <li key={value.id}>
                    {value.scenario
                      ? `${conversationScenarios.find((scenario) => scenario.id === value.scenario)?.title} · `
                      : ""}
                    {value.dialogue
                      ? `Conversation turn ${value.dialogue.turn}`
                      : value.mode === "listening"
                        ? "Listening"
                        : "Speaking"}{" "}
                    ·{" "}
                    {value.speakingAssessment
                      ? value.speakingAssessment.overall === null
                        ? "AI: no reliable score"
                        : `AI estimate ${value.speakingAssessment.overall}/100`
                      : value.feedback
                        ? `${value.feedback.score}/100`
                        : value.selfCheck === "comfortable"
                          ? "Comfortable"
                          : "Needs practice"}{" "}
                    ·{" "}
                    {value.feedback
                      ? "Transcript checked after submission"
                      : value.revealed
                        ? "Text shown"
                        : "Text hidden"}{" "}
                    · {new Date(value.createdAt).toLocaleString()}
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
