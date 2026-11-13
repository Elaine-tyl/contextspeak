"use client";
import {
  useEffect,
  useRef,
  useState,
  type CSSProperties,
  type ReactNode,
} from "react";
import {
  gameModes,
  type GameKind,
  type GameRound,
} from "@contextspeak/contracts";
import { useSiteMotion } from "../SiteMotion";
import {
  activityLabels,
  studioThemes,
  type StudioActivity,
} from "./studioThemes";
import type { StudioScene } from "./studioScene";
import "./studios.css";

export function StudioVisual({
  kind,
  level,
  activity,
  count,
}: {
  kind: GameKind;
  level: number;
  activity: StudioActivity;
  count: number;
}) {
  const moving = useSiteMotion();
  const host = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const scene = useRef<StudioScene | null>(null);
  const latest = useRef({ moving, activity, count });
  latest.current = { moving, activity, count };
  const [renderer, setRenderer] = useState("loading");
  useEffect(() => {
    let active = true;
    let world: StudioScene | undefined;
    void import("./studioScene")
      .then(({ createStudioScene }) => {
        if (!active || !host.current || !canvas.current) return;
        world = createStudioScene(
          host.current,
          canvas.current,
          kind,
          level,
          () => {
            if (active) setRenderer("fallback");
          },
        );
        scene.current = world;
        world.update(latest.current);
        setRenderer("webgl");
      })
      .catch(() => {
        if (active) setRenderer("fallback");
      });
    return () => {
      active = false;
      scene.current = null;
      world?.dispose();
    };
  }, [kind, level]);
  useEffect(() => {
    scene.current?.update({ moving, activity, count });
  }, [moving, activity, count]);
  return (
    <div
      ref={host}
      className="studio-visual"
      data-renderer={renderer}
      data-state={activity}
      aria-hidden="true"
    >
      <div className="studio-orbit" />
      <canvas ref={canvas} />
      {renderer !== "webgl" && (
        <span className="studio-fallback">{studioThemes[kind].glyph}</span>
      )}
      <span className="studio-state">
        <i />
        {activityLabels[activity]}
      </span>
    </div>
  );
}

export function GameStudio({
  round,
  activity,
  count,
  action,
  children,
}: {
  round: GameRound;
  activity: StudioActivity;
  count: number;
  action: ReactNode;
  children: ReactNode;
}) {
  const moving = useSiteMotion();
  const theme = studioThemes[round.kind];
  const dialog = useRef<HTMLDialogElement>(null);
  const help = useRef<HTMLButtonElement>(null);
  const mode = gameModes.find((item) => item.kind === round.kind)!;
  return (
    <article
      className={`game-round game-studio ${activity === "recording" ? "is-recording" : ""}`}
      data-testid="game-studio"
      data-kind={round.kind}
      data-motion={moving ? "on" : "off"}
      style={
        {
          "--studio-accent": theme.accent,
          "--studio-soft": theme.soft,
        } as CSSProperties
      }
    >
      <header className="studio-hero">
        <div className="studio-intro">
          <div className="studio-topline">
            <span className="studio-name">{theme.name}</span>
            <span className="studio-level">Level {round.level} / 10</span>
          </div>
          <h2>{round.title}</h2>
          <p>
            {round.kind === "evidence-hunt"
              ? "Read the passage, then find the supporting evidence."
              : round.instruction}
          </p>
          <div className="studio-controls">
            {action}
            <button
              ref={help}
              type="button"
              className="studio-help"
              aria-haspopup="dialog"
              disabled={
                activity === "recording" ||
                activity === "checking" ||
                activity === "listening" ||
                (round.kind === "true-false" && activity === "answering")
              }
              onClick={() => dialog.current?.showModal()}
            >
              How to play <span aria-hidden="true">?</span>
            </button>
          </div>
        </div>
        {round.kind !== "scene-words" && (
          <StudioVisual
            key={`${round.kind}:${round.level}`}
            kind={round.kind}
            level={round.level}
            activity={activity}
            count={count}
          />
        )}
      </header>
      <div className="studio-workspace">{children}</div>
      <dialog
        ref={dialog}
        className="studio-dialog"
        aria-label="How to play"
        onClose={() => help.current?.focus()}
      >
        <form method="dialog">
          <button
            className="studio-dialog-close"
            aria-label="Close instructions"
          >
            ×
          </button>
        </form>
        <p className="studio-name">{theme.name}</p>
        <h2>{mode.label}</h2>
        <p>{mode.description}</p>
        <p>
          {round.recall
            ? "Press Start to hear the passage once. Highlighted words stay visible during playback, then become blanks. Fill them in and check your answer. Replay audio keeps your answers and does not reveal the original words."
            : theme.hint}
        </p>
        <small>
          Tab moves between controls. Your score is calculated after you answer.
        </small>
        <form method="dialog">
          <button className="primary">Got it</button>
        </form>
      </dialog>
    </article>
  );
}
