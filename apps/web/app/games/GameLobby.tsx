"use client";
import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type CSSProperties,
} from "react";
import {
  gameModes,
  gameTrackLabels,
  type GameCatalog,
  type GameKind,
  type GameTrack,
} from "@contextspeak/contracts";
import { useSiteMotion } from "../SiteMotion";
import { useGameSound } from "./GameEffects";
import type { LobbyScene } from "./lobbyScene";
import "./lobby.css";

export function GameLobby({
  catalog,
  track,
  onChoose,
}: {
  catalog: GameCatalog;
  track: GameTrack;
  onChoose(kind: GameKind): void;
}) {
  const animated = useSiteMotion();
  const sound = useGameSound();
  const [view, setView] = useState<"world" | "list">("world");
  const [renderer, setRenderer] = useState("loading");
  const [hovered, setHovered] = useState<number | null>(null);
  const host = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const markers = useRef<(HTMLButtonElement | null)[]>([]);
  const scene = useRef<LobbyScene | null>(null);
  const choose = useRef(onChoose);
  const play = useRef(sound);
  const motionEnabled = useRef(animated);
  choose.current = onChoose;
  play.current = sound;
  motionEnabled.current = animated;
  const modes = useMemo(
    () =>
      gameModes
        .filter((mode) => mode.track === track)
        .map((mode) => ({
          ...mode,
          level:
            catalog.modeProgress?.[mode.kind]?.level ??
            catalog.progress[track]?.level ??
            1,
        })),
    [catalog, track],
  );
  // A disposed WebGL context cannot be reused. Every scene configuration owns its canvas.
  const sceneKey = `${view}:${modes.map((mode) => `${mode.kind}:${mode.level}`).join(",")}`;

  function select(index: number) {
    play.current("select");
    choose.current(modes[index].kind);
  }
  useEffect(() => {
    if (view === "list") {
      setRenderer("list");
      return;
    }
    let active = true;
    let world: LobbyScene | undefined;
    setRenderer("loading");
    setHovered(null);
    void import("./lobbyScene")
      .then(({ createLobbyScene }) => {
        if (!active || !host.current || !canvas.current) return;
        world = createLobbyScene(host.current, canvas.current, modes, {
          select(index) {
            play.current("select");
            choose.current(modes[index].kind);
          },
          hover: setHovered,
          project(index, x, y) {
            const marker = markers.current[index];
            if (marker) {
              const left = `${Math.round(x)}px`,
                top = `${Math.round(y)}px`;
              if (marker.style.left !== left) marker.style.left = left;
              if (marker.style.top !== top) marker.style.top = top;
            }
          },
          failed() {
            if (active) setRenderer("fallback");
          },
        });
        scene.current = world;
        world.motion(motionEnabled.current);
        setRenderer("webgl");
      })
      .catch(() => {
        if (active) setRenderer("fallback");
      });
    return () => {
      active = false;
      world?.dispose();
      scene.current = null;
    };
  }, [sceneKey]);
  useEffect(() => {
    scene.current?.motion(animated);
  }, [animated, renderer]);

  return (
    <section
      className="game-lobby"
      style={
        {
          "--lobby-rows": Math.ceil(modes.length / 4),
          "--mobile-rows": Math.ceil(modes.length / 2),
        } as CSSProperties
      }
      data-renderer={renderer}
      aria-label="Game world"
    >
      <div className="lobby-hud">
        <div>
          <p className="eyebrow">{gameTrackLabels[track]}</p>
          <h2>Choose your challenge</h2>
        </div>
        <div className="lobby-controls">
          {renderer === "webgl" && (
            <button
              className="secondary"
              onClick={() => scene.current?.reset()}
            >
              Reset view
            </button>
          )}
          {renderer !== "fallback" && (
            <button
              className="secondary"
              onClick={() => setView(view === "world" ? "list" : "world")}
            >
              {view === "world" ? "List view" : "3D view"}
            </button>
          )}
        </div>
      </div>
      <div className="lobby-stage" ref={host}>
        <canvas
          key={sceneKey}
          ref={canvas}
          role="img"
          aria-label="3D game world"
        />
        {renderer === "loading" && (
          <p role="status" className="lobby-status">
            Building your islands…
          </p>
        )}
        {renderer === "fallback" && (
          <p role="status">
            3D isn’t available on this browser. Choose a game below.
          </p>
        )}
        <div className="lobby-options" role="group" aria-label="Game modes">
          {modes.map((mode, index) => (
            <button
              key={mode.kind}
              ref={(button) => {
                markers.current[index] = button;
              }}
              className="lobby-marker"
              aria-label={mode.label}
              title={mode.description}
              onClick={() => select(index)}
              onFocus={() => scene.current?.highlight(index)}
              onMouseEnter={() => scene.current?.highlight(index)}
              onMouseLeave={() => scene.current?.highlight(null)}
              onBlur={() => scene.current?.highlight(null)}
            >
              <strong>{mode.label}</strong>
              <small>Level {mode.level} of 10</small>
            </button>
          ))}
        </div>
      </div>
      {renderer === "webgl" && (
        <div className="lobby-footer">
          <span>
            {hovered === null ? (
              <>
                Tap an island to play
                <span className="lobby-drag-hint"> · Drag to look around</span>
              </>
            ) : (
              modes[hovered]?.description
            )}
          </span>
          <span aria-hidden="true">✦ {modes.length} challenges</span>
        </div>
      )}
    </section>
  );
}
