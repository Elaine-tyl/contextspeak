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
  learningReturn,
  practiceMode,
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
  initialMode = "listening",
}: {
  id: string;
  lookupId?: string;
  scenarioId?: ScenarioId;
  returnTo?: string;
  initialMode?: "listening" | "speaking" | "conversation";
}) {
  const [scenario, setScenario] = useState<ScenarioId | "">(scenarioId ?? "");
  useEffect(() => {
    const restore = () => {
      const query = new URLSearchParams(window.location.search);
      const restored =
        conversationScenarios.find(
          (value) => value.id === query.get("scenario"),
        )?.id ?? "";
      setMode(practiceMode(query.get("mode"), Boolean(restored)));
      setScenario(restored);
    };
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);
  const [task, setTask] = useState<PracticeTask | null>(null);
  const [token, setToken] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const [mode, setMode] = useState<PracticeSubmission["mode"] | "conversation">(
    initialMode,
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
  const back =
    returnTo === "/progress"
      ? "/progress"
      : withLibraryReturn(
          `/library/item/${encodeURIComponent(id)}${lookupId ? `?lookup=${encodeURIComponent(lookupId)}` : ""}`,
          learningReturn(returnTo),
        );
  return (
    <main className="page page-narrow vocabulary-detail practice-page tone-speak">
      <Link className="back-link" href={back}>
        {returnTo === "/progress" ? "Back to progress" : "Back to vocabulary"}
      </Link>
      <div className="page-head">
        <div>
          <h1>{task ? <>Practise “{task.expression}”</> : "Listen & speak"}</h1>
          <p className="page-lead">
            Listen and type what you hear, or answer a question out loud. You
            get feedback straight away.
          </p>
        </div>
      </div>
      {token && (
        <label className="scenario-picker">
          Conversation scenario
          <select
            value={scenario}
            disabled={locked}
            onChange={(event) => {
              setTask(null);
              setLoading(true);
              const nextMode =
                mode === "conversation" && !event.target.value
                  ? "speaking"
                  : mode;
              setMode(nextMode);
              setRound(0);
              setScenario(event.target.value as ScenarioId | "");
              updateQuery(
                { scenario: event.target.value, mode: nextMode },
                true,
              );
            }}
          >
            <option value="">Sentence I saved</option>
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
                Reply in your own words. The guide is in English.
              </p>
            </section>
          )}
          <div className="practice-toolbar">
            <div className="segmented" role="group" aria-label="Practice mode">
              {(
                [
                  "listening",
                  "speaking",
                  ...(task.scenario ? ["conversation" as const] : []),
                ] as const
              ).map((value) => (
                <button
                  key={value}
                  aria-pressed={mode === value}
                  disabled={locked}
                  onClick={() => {
                    setMode(value);
                    updateQuery({ mode: value }, true);
                  }}
                >
                  {value === "listening"
                    ? "Listening"
                    : value === "speaking"
                      ? "Speaking"
                      : "Conversation"}
                </button>
              ))}
            </div>
            <button
              className="link-button"
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
          <section className="recent-practice" aria-label="Recent practice">
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
              <p className="muted">
                No results yet for this word. Finish an exercise above and it
                will show up here.
              </p>
            )}
          </section>
          <p className="fine-print privacy-note">
            How this works: AI voices are generated once and cached. Text is
            sent to Google to create the audio, and finished English recordings
            are sent for AI feedback. Practice results are kept separate from
            your vocabulary review.
          </p>
        </>
      )}
    </main>
  );
}
