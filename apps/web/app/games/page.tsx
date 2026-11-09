"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type {
  GameCatalog,
  GameRound,
  GameTrack,
} from "@contextspeak/contracts";
import { API, api, ApiError, type WebSession } from "../../src/api";
import { GameRoundView } from "./GameRoundView";
import "../library/library.css";
import "../practice/practice.css";
import "./games.css";
export default function GamesPage() {
  const [token, setToken] = useState("");
  const currentToken = useRef("");
  const [catalog, setCatalog] = useState<GameCatalog | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [track, setTrack] = useState<GameTrack>("listening");
  const [level, setLevel] = useState(1);
  const [round, setRound] = useState<GameRound | null>(null);
  const [locked, setLocked] = useState(false);
  function expire() {
    currentToken.current = "";
    setToken("");
    setCatalog(null);
    setRound(null);
    setLocked(false);
    setError("Your session expired or changed. Sign in again.");
  }
  useEffect(() => {
    const controller = new AbortController();
    let stopped = false;
    let pending = false;
    let loaded = false;
    let expiry: ReturnType<typeof setTimeout>;
    async function load() {
      if (stopped || pending) return;
      pending = true;
      try {
        const session = await api<WebSession>(
          "",
          "/auth/session",
          "GET",
          undefined,
          { signal: controller.signal },
        );
        if (controller.signal.aborted || stopped) return;
        const remaining = Date.parse(session.expiresAt) - Date.now();
        if (
          remaining <= 0 ||
          (currentToken.current && currentToken.current !== session.csrfToken)
        ) {
          stopped = true;
          expire();
          return;
        }
        clearTimeout(expiry);
        expiry = setTimeout(
          () => {
            stopped = true;
            expire();
          },
          Math.min(remaining, 2147483647),
        );
        currentToken.current = session.csrfToken;
        setToken(session.csrfToken);
        if (!loaded) {
          const value = await api<GameCatalog>(
            session.csrfToken,
            "/v1/games",
            "GET",
            undefined,
            { signal: controller.signal },
          );
          if (!controller.signal.aborted && !stopped) {
            setCatalog(value);
            loaded = true;
            setError("");
          }
        }
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error instanceof ApiError && [401, 403].includes(error.status)) {
          stopped = true;
          expire();
        } else if (!loaded)
          setError("Could not load games. Check your connection and refresh.");
      } finally {
        pending = false;
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    const timer = setInterval(() => void load(), 3000);
    return () => {
      controller.abort();
      clearInterval(timer);
      clearTimeout(expiry);
    };
  }, []);
  async function refresh() {
    const owner = token;
    try {
      const value = await api<GameCatalog>(owner, "/v1/games");
      if (currentToken.current === owner) {
        setCatalog(value);
        setError("");
      }
    } catch (error) {
      if (currentToken.current !== owner) return;
      if (error instanceof ApiError && [401, 403].includes(error.status))
        expire();
      else
        setError(
          "Your answer is saved, but progress could not refresh. Try Refresh progress.",
        );
    }
  }
  const progress = catalog?.progress[track];
  const recommendation = catalog?.recommendations?.[track];
  return (
    <main className="games-page">
      <Link href="/library">Back to library</Link>
      <p className="eyebrow">ContextSpeak · play and practise</p>
      <h1>Game practice</h1>
      <p>
        Listen, solve and speak. Pass three different challenges at 70+ to
        unlock the next level. Listening and speaking progress independently.
      </p>
      {error && <p role="alert">{error}</p>}
      {loading && <p role="status">Loading games…</p>}
      {!token && !loading && (
        <a href={API + "/auth/login?returnTo=/games"}>Sign in with Google</a>
      )}
      {catalog && (
        <>
          <div className="practice-tabs" aria-label="Game skill">
            {(["listening", "speaking"] as const).map((value) => (
              <button
                key={value}
                className={track === value ? "primary" : "secondary"}
                disabled={locked}
                aria-pressed={track === value}
                onClick={() => {
                  setTrack(value);
                  setLevel(1);
                  setRound(null);
                }}
              >
                {value === "listening" ? "Listening games" : "Speaking games"}
              </button>
            ))}
          </div>
          <div className="practice-tabs" aria-label="Game difficulty">
            {[1, 2, 3].map((value) => (
              <button
                key={value}
                className={level === value ? "primary" : "secondary"}
                disabled={locked || value > progress!.level}
                aria-pressed={level === value}
                onClick={() => {
                  setLevel(value);
                  setRound(null);
                }}
              >
                Level {value}
                {value > progress!.level ? " · Locked" : ""}
              </button>
            ))}
          </div>
          <p className="game-progress" aria-live="polite">
            Level {level}: {progress!.passed[level - 1]} different challenges
            passed
            {level < 3 ? " · 3 needed for the next level" : " · highest level"}
          </p>
          {recommendation && (
            <section
              className="game-progress"
              aria-label="Practice recommendation"
            >
              <strong>Recommended: Level {recommendation.level}</strong>
              <p>{recommendation.reason}</p>
              {recommendation.focus && (
                <p>
                  Focus:{" "}
                  {recommendation.focus.replace(
                    "taskResponse",
                    "task response",
                  )}
                </p>
              )}
              <button
                className="secondary"
                disabled={locked}
                onClick={() => {
                  setLevel(recommendation.level);
                  setRound(null);
                }}
              >
                Use recommended level
              </button>
              <small>You can still choose any unlocked level.</small>
            </section>
          )}
          {!round ? (
            <>
              <button className="secondary" onClick={() => void refresh()}>
                Refresh progress
              </button>
              <p className="section-copy">
                Original English content · short excerpts · AI speech. PK is a
                simulated AI training opponent, not a real player.
              </p>
              <div className="game-grid">
                {catalog.rounds
                  .filter(
                    (value) => value.track === track && value.level === level,
                  )
                  .map((value) => (
                    <button
                      className="game-tile"
                      key={value.id}
                      onClick={() => setRound(value)}
                    >
                      <small>{value.genre ?? value.kind}</small>
                      <h2>{value.title}</h2>
                      <p>
                        {value.kind === "dictation"
                          ? "Listen and write"
                          : value.kind === "picture"
                            ? "Listen and choose a picture"
                            : value.track === "speaking"
                              ? "Answer aloud · automatic AI feedback"
                              : "Listen and answer"}
                      </p>
                      <span>Play →</span>
                    </button>
                  ))}
              </div>
            </>
          ) : (
            <>
              <button
                className="secondary"
                disabled={locked}
                onClick={() => setRound(null)}
              >
                Choose another game
              </button>
              <GameRoundView
                key={round.id}
                round={round}
                token={token}
                onSaved={refresh}
                onLocked={setLocked}
                onExpired={expire}
                onLeave={() => {
                  setLocked(false);
                  setRound(null);
                  if (recommendation) setLevel(recommendation.level);
                }}
              />
            </>
          )}
        </>
      )}
    </main>
  );
}
