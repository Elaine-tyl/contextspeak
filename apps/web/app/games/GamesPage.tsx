"use client";
import { useEffect, useRef, useState } from "react";
import {
  gameModes,
  gameTracks,
  gameTrackLabels,
  type GameCatalog,
  type GameTrack,
  type GameKind,
  type GameLevelRun,
  type GameLevelStart,
} from "@contextspeak/contracts";
import { API, api, ApiError, type WebSession } from "../../src/api";
import { GameSetup } from "./GameSetup";
import { LevelResult } from "./LevelResult";
import { GameEffects } from "./GameEffects";
import { GameLobby } from "./GameLobby";
import { GameRoundView } from "./GameRoundView";
import "../library/library.css";
import "../practice/practice.css";
import "./games.css";
import { DifficultyGuide } from "./GameContext";
import { GapFillResources } from "./GapFill";
export function GamesPage({ basePath = "/games" }: { basePath?: string }) {
  const [token, setToken] = useState("");
  const currentToken = useRef("");
  const [catalog, setCatalog] = useState<GameCatalog | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [track, setTrack] = useState<GameTrack>("listening");
  const [level, setLevel] = useState(1);
  const [source, setSource] = useState("all");
  const [kind, setKind] = useState<GameKind | null>(null);
  const [topic, setTopic] = useState("all");
  const [run, setRun] = useState<GameLevelRun | null>(null);
  const [question, setQuestion] = useState(0);
  const [starting, setStarting] = useState(false);
  const pendingStart = useRef<GameLevelStart | null>(null);
  const [locked, setLocked] = useState(false);
  function expire() {
    currentToken.current = "";
    setToken("");
    setCatalog(null);
    setRun(null);
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
            const id = new URLSearchParams(window.location.search).get("run");
            const selectedMode = gameModes.find(
              (mode) =>
                mode.kind ===
                new URLSearchParams(window.location.search).get("mode"),
            );
            if (!id && selectedMode) {
              setKind(selectedMode.kind);
              setTrack(selectedMode.track);
              setLevel(value.modeProgress?.[selectedMode.kind]?.level ?? 1);
            }
            if (id) {
              const resumed = await api<GameLevelRun>(
                session.csrfToken,
                `/v1/games/levels/${id}`,
                "GET",
                undefined,
                { signal: controller.signal },
              );
              if (!controller.signal.aborted && !stopped) {
                setRun(resumed);
                setKind(resumed.kind);
                setLevel(resumed.level);
                setTrack(
                  gameModes.find((mode) => mode.kind === resumed.kind)!.track,
                );
                setTopic(resumed.topic ?? "all");
                setSource(resumed.source ?? "all");
                setQuestion(
                  Math.min(resumed.receipts.length, resumed.rounds.length - 1),
                );
              }
            }
          }
        }
      } catch (error) {
        if (controller.signal.aborted) return;
        if (error instanceof ApiError && [401, 403].includes(error.status)) {
          stopped = true;
          expire();
        } else
          setError(
            loaded
              ? "Could not restore this level. Choose a game to start another run."
              : "Could not load games. Check your connection and refresh.",
          );
      } finally {
        pending = false;
        if (!controller.signal.aborted) setLoading(false);
      }
    }
    void load();
    const timer = setInterval(() => void load(), 3000);
    return () => {
      currentToken.current = "";
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
          "Could not refresh the game catalog. Check your connection and try Refresh progress.",
        );
    }
  }
  function leaveRun() {
    setRun(null);
    setLocked(false);
    window.history.replaceState(null, "", basePath);
  }
  async function refreshRun() {
    if (!run) return;
    const owner = token;
    try {
      const saved = await api<GameLevelRun>(
        owner,
        `/v1/games/levels/${run.id}`,
      );
      if (currentToken.current !== owner) return;
      setRun(saved);
      if (saved.completed) setLocked(false);
      setError("");
      await refresh();
      return saved;
    } catch (error) {
      if (currentToken.current !== owner) return;
      if (error instanceof ApiError && [401, 403].includes(error.status))
        expire();
      else
        setError(
          "Your answer may be saved. Use Refresh level to recover your confirmed result.",
        );
    }
  }
  async function startRun(target = level, allowFallback = false) {
    if (!kind || starting) return;
    let selectedTopic = topic;
    let selectedSource = source;
    const matches = () =>
      catalog!.rounds.filter(
        (round) =>
          round.kind === kind &&
          round.level === target &&
          (selectedTopic === "all" || round.topic === selectedTopic) &&
          (selectedSource === "all" ||
            (round.source?.provider ?? "ContextSpeak") === selectedSource),
      );
    if (allowFallback && !matches().length) selectedTopic = "all";
    if (allowFallback && !matches().length) selectedSource = "all";
    const settings = {
      kind,
      level: target,
      ...(selectedTopic !== "all" ? { topic: selectedTopic } : {}),
      ...(selectedSource !== "all"
        ? { source: selectedSource as GameLevelStart["source"] }
        : {}),
    };
    const previous = pendingStart.current;
    if (
      !previous ||
      previous.kind !== kind ||
      previous.level !== target ||
      previous.topic !== settings.topic ||
      previous.source !== settings.source
    )
      pendingStart.current = { ...settings, eventId: crypto.randomUUID() };
    const owner = token;
    setStarting(true);
    setError("");
    try {
      const saved = await api<GameLevelRun>(
        owner,
        "/v1/games/levels",
        "POST",
        pendingStart.current,
      );
      if (currentToken.current !== owner) return;
      pendingStart.current = null;
      setRun(saved);
      setQuestion(0);
      setLevel(saved.level);
      setLocked(false);
      setTopic(saved.topic ?? "all");
      setSource(saved.source ?? "all");
      window.history.replaceState(null, "", `${basePath}?run=${saved.id}`);
    } catch (error) {
      if (currentToken.current !== owner) return;
      if (error instanceof ApiError && [401, 403].includes(error.status))
        expire();
      else
        setError(
          "Could not confirm the level start. Retry with the same settings to recover it.",
        );
    } finally {
      if (currentToken.current === owner) setStarting(false);
    }
  }
  const mode = gameModes.find((mode) => mode.kind === kind);
  const unlocked = kind
    ? (catalog?.modeProgress?.[kind]?.level ??
      catalog?.progress[track]?.level ??
      1)
    : 1;
  const recommendation = catalog?.recommendations?.[track];
  const frozen = locked || starting;
  return (
    <main className="page games-page">
      <GameEffects>
        <div className="page-head">
          <h1>Games</h1>
          <a className="secondary" href="/world">
            Life world ↗
          </a>
        </div>
        {error && <p role="alert">{error}</p>}
        {loading && <p role="status">Loading games…</p>}
        {!token && !loading && (
          <section className="connect-panel sign-in-panel">
            <div>
              <h2>Sign in to play</h2>
              <p>Your levels are saved to your account.</p>
            </div>
            <a
              className="primary"
              href={API + "/auth/login?returnTo=" + basePath}
            >
              Sign in with Google
            </a>
          </section>
        )}
        {catalog && (
          <>
            {!run && (
              <div
                className="segmented game-tracks"
                role="group"
                aria-label="Game skill"
              >
                {gameTracks.map((value) => (
                  <button
                    key={value}
                    disabled={frozen}
                    aria-pressed={track === value}
                    onClick={() => {
                      setTrack(value);
                      setKind(null);
                      setLevel(1);
                      setTopic("all");
                      setSource("all");
                    }}
                  >
                    {gameTrackLabels[value]}
                  </button>
                ))}
              </div>
            )}
            {kind && (
              <button
                className="back-link game-back"
                disabled={frozen}
                onClick={() => {
                  leaveRun();
                  setKind(null);
                }}
              >
                All games
              </button>
            )}
            {!kind ? (
              <GameLobby
                catalog={catalog}
                track={track}
                onChoose={(value) => {
                  setKind(value);
                  setLevel(1);
                  setTopic("all");
                  setSource("all");
                }}
              />
            ) : (
              <>
                {run ? (
                  <>
                    <div className="section-head game-mode-head">
                      <h2>{mode!.label}</h2>
                    </div>
                    <details className="game-round-guide">
                      <summary>Difficulty and resources</summary>
                      <DifficultyGuide />
                      {kind === "gap-fill" && <GapFillResources />}
                    </details>
                    <button
                      className="link-button"
                      disabled={frozen}
                      onClick={() => void refreshRun()}
                    >
                      Refresh level
                    </button>
                    {run.completed ? (
                      <LevelResult
                        run={run}
                        token={token}
                        busy={starting}
                        onRetry={() => void startRun(run.level, true)}
                        onNext={() => {
                          if (run.nextLevel) void startRun(run.nextLevel, true);
                        }}
                      />
                    ) : (
                      <>
                        <div
                          className="game-session-meter"
                          role="progressbar"
                          aria-label="Level completion"
                          aria-valuemin={0}
                          aria-valuemax={run.rounds.length}
                          aria-valuenow={question}
                        >
                          <span
                            style={{
                              width: `${(question / run.rounds.length) * 100}%`,
                            }}
                          />
                        </div>
                        <p className="game-progress" role="status">
                          Level {run.level} · Question {question + 1} of{" "}
                          {run.rounds.length}
                        </p>
                        <GameRoundView
                          key={`${run.id}:${question}`}
                          round={run.rounds[question]}
                          token={token}
                          levelSessionId={run.id}
                          onSaved={async () => {
                            await refreshRun();
                          }}
                          onLocked={setLocked}
                          onExpired={expire}
                          onLeave={leaveRun}
                          onContinue={async () => {
                            const saved = await refreshRun();
                            if (saved && !saved.completed)
                              setQuestion(saved.receipts.length);
                          }}
                        />
                      </>
                    )}
                  </>
                ) : (
                  <GameSetup
                    kind={kind}
                    rounds={catalog.rounds.filter(
                      (round) => round.kind === kind,
                    )}
                    unlocked={unlocked}
                    recommendation={recommendation}
                    busy={frozen}
                    settings={{ level, topic, source }}
                    onChange={(value) => {
                      setLevel(value.level);
                      setTopic(value.topic);
                      setSource(value.source);
                    }}
                    onStart={() => void startRun()}
                    onRefresh={() => void refresh()}
                  />
                )}
              </>
            )}
          </>
        )}
      </GameEffects>
    </main>
  );
}
