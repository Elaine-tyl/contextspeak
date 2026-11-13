"use client";
import type { CSSProperties } from "react";
import { gameModes, gameRequiresAudio } from "@contextspeak/contracts";
import { StudioVisual } from "./GameStudio";
import { studioThemes } from "./studioThemes";
import { useGameSound } from "./GameEffects";
import { DifficultyGuide } from "./GameContext";
import { GapFillResources } from "./GapFill";
import "./setup.css";
import type {
  GameKind,
  GameRound,
  PracticeRecommendation,
} from "@contextspeak/contracts";
export type GameSettings = { level: number; topic: string; source: string };
export function GameSetup({
  kind,
  rounds,
  unlocked,
  recommendation,
  busy,
  settings,
  onChange,
  onStart,
  onRefresh,
}: {
  kind: GameKind;
  rounds: GameRound[];
  unlocked: number;
  recommendation?: PracticeRecommendation;
  busy: boolean;
  settings: GameSettings;
  onChange(value: GameSettings): void;
  onStart(): void;
  onRefresh(): void;
}) {
  const { level, topic, source } = settings;
  const theme = studioThemes[kind];
  const mode = gameModes.find((item) => item.kind === kind)!;
  const sound = useGameSound();
  const available = rounds.filter((round) => round.level === level);
  const themes = [
    ...new Set(
      available
        .map((round) => round.topic)
        .filter((value): value is string => !!value),
    ),
  ];
  // Keep a chosen source visible even at a level with no matching excerpts.
  const sources = [
    ...new Set(
      rounds.flatMap((round) => (round.source ? [round.source.provider] : [])),
    ),
  ];
  const visible = available.filter(
    (round) =>
      (topic === "all" || round.topic === topic) &&
      (source === "all" ||
        (round.source?.provider ?? "ContextSpeak") === source),
  );
  const recommended = Math.min(recommendation?.level ?? 1, unlocked);
  const questionCount = Math.min(3, visible.length);
  return (
    <section
      className="game-setup"
      data-testid="game-setup"
      data-kind={kind}
      style={
        {
          "--studio-accent": theme.accent,
          "--studio-soft": theme.soft,
        } as CSSProperties
      }
    >
      <header className="setup-hero">
        <div>
          <span className="studio-name">{theme.name}</span>
          <h2>{mode.label}</h2>
          <p>{mode.description}</p>
          <span className="setup-selected">
            Level {level} <span aria-hidden="true">/</span>
            <span className="sr-only">of</span> 10
          </span>
        </div>
        <StudioVisual
          key={`${kind}:${level}`}
          kind={kind}
          level={level}
          activity="ready"
          count={0}
        />
      </header>
      <div className="setup-body">
        <div className="setup-level-heading">
          <h3>Choose your level</h3>
          <small>{unlocked} / 10 unlocked</small>
        </div>
        <div className="setup-levels" role="group" aria-label="Game difficulty">
          {Array.from({ length: 10 }, (_, index) => index + 1).map((value) => {
            const locked = value > unlocked;
            return (
              <button
                key={value}
                type="button"
                className="setup-level"
                disabled={busy || locked}
                aria-pressed={level === value}
                aria-label={`Level ${value}${locked ? " · Locked" : ""}`}
                onClick={() => {
                  sound("select");
                  onChange({ level: value, topic: "all", source: "all" });
                }}
              >
                <span className="setup-node" aria-hidden="true">
                  {locked ? (
                    <svg
                      viewBox="0 0 24 24"
                      width="18"
                      height="18"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="1.8"
                    >
                      <rect x="5" y="10" width="14" height="11" rx="3" />
                      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
                    </svg>
                  ) : (
                    value
                  )}
                </span>
                <span className="setup-level-label">Level {value}</span>
              </button>
            );
          })}
        </div>
        {recommendation && (
          <section
            className="setup-recommendation"
            aria-label="Practice recommendation"
          >
            <div>
              <strong>Recommended: Level {recommended}</strong>
              <details>
                <summary>Why this level?</summary>
                <p>
                  {recommendation.level > unlocked
                    ? "Build evidence in this game mode before trying a higher level."
                    : recommendation.reason}
                </p>
                {recommendation.focus && (
                  <p>
                    Focus:{" "}
                    {recommendation.focus.replace(
                      "taskResponse",
                      "task response",
                    )}
                  </p>
                )}
              </details>
            </div>
            <button
              className="secondary"
              aria-label="Use recommended level"
              disabled={busy}
              onClick={() =>
                onChange({ level: recommended, topic: "all", source: "all" })
              }
            >
              Use level {recommended} <span aria-hidden="true">↗</span>
            </button>
          </section>
        )}
        <div className="setup-filters">
          <div>
            <label htmlFor="game-theme">Theme</label>
            <select
              id="game-theme"
              disabled={busy}
              value={topic}
              onChange={(event) =>
                onChange({ ...settings, topic: event.target.value })
              }
            >
              <option value="all">All themes</option>
              {themes.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="game-content-source">Content source</label>
            <select
              id="game-content-source"
              disabled={busy}
              value={source}
              onChange={(event) =>
                onChange({ ...settings, source: event.target.value })
              }
            >
              <option value="all">All sources</option>
              <option value="ContextSpeak">ContextSpeak originals</option>
              {sources.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="setup-launch">
          <p className="setup-availability" role="status">
            {visible.length
              ? `${questionCount} ${questionCount === 1 ? "question" : "questions"} · Level ${level}`
              : !rounds.length
                ? "This game's questions are not loaded on the server. Refresh after the server updates."
                : "No matching questions. Choose another theme or All sources."}
          </p>
          {!!rounds.length && !visible.length && (
            <button
              className="secondary"
              disabled={busy}
              onClick={() =>
                onChange({ ...settings, topic: "all", source: "all" })
              }
            >
              Clear filters
            </button>
          )}
          <div className="setup-actions">
            <button
              className="primary setup-start"
              disabled={busy || !visible.length}
              onClick={onStart}
            >
              {busy ? "Starting level…" : "Start level"}{" "}
              <span aria-hidden="true">→</span>
            </button>
            <button
              className="secondary setup-refresh"
              disabled={busy}
              onClick={onRefresh}
              aria-label="Refresh progress"
              title="Refresh progress"
            >
              ↻
            </button>
          </div>
        </div>
        <details className="setup-guide">
          <summary>Difficulty and resources</summary>
          <DifficultyGuide />
          {kind === "gap-fill" && <GapFillResources />}
          {kind === "pk" && (
            <p>Echo is a simulated training opponent, not a real player.</p>
          )}
          <p>
            {rounds.some(gameRequiresAudio)
              ? "AI-generated audio. Reviewed open texts are credited inside each exercise."
              : "Original text exercises. No audio playback or AI voice quota is required."}{" "}
            Game levels are practice difficulty, not IELTS band scores.
          </p>
        </details>
      </div>
    </section>
  );
}
