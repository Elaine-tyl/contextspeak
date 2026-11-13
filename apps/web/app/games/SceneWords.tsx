"use client";
import { useEffect, useRef, useState } from "react";
import type { GameRound, SceneWordNotes } from "@contextspeak/contracts";
import { SpeakerButton } from "../library/SpeakerButton";
import { useSiteMotion } from "../SiteMotion";
import type { StudioActivity } from "./studioThemes";
import type { WordRoom, WordRoomState } from "./wordRoomScene";
import "./scene-words.css";

export function SceneWords({
  round,
  value,
  disabled,
  activity,
  score,
  onChange,
  onSubmit,
}: {
  round: GameRound;
  value: string;
  disabled: boolean;
  activity: StudioActivity;
  score: number | null | undefined;
  onChange(value: string): void;
  onSubmit(): void;
}) {
  const moving = useSiteMotion();
  const host = useRef<HTMLDivElement>(null),
    canvas = useRef<HTMLCanvasElement>(null),
    input = useRef<HTMLInputElement>(null);
  const world = useRef<WordRoom | null>(null);
  const latest = useRef<WordRoomState>({ moving, activity, score });
  latest.current = { moving, activity, score };
  const [renderer, setRenderer] = useState("loading");
  const room = round.scene?.room ?? "bedroom",
    focus = round.scene?.focus ?? "wardrobe";
  useEffect(() => {
    let active = true;
    let scene: WordRoom | undefined;
    void import("./wordRoomScene")
      .then(({ createWordRoom }) => {
        if (!active || !host.current || !canvas.current) return;
        scene = createWordRoom(
          host.current,
          canvas.current,
          { room, focus },
          () => {
            if (active) setRenderer("fallback");
          },
        );
        world.current = scene;
        scene.update(latest.current);
        setRenderer("webgl");
      })
      .catch(() => {
        if (active) setRenderer("fallback");
      });
    return () => {
      active = false;
      world.current = null;
      scene?.dispose();
    };
  }, [room, focus]);
  useEffect(() => {
    world.current?.update({ moving, activity, score });
  }, [moving, activity, score]);
  useEffect(() => {
    if (!disabled) input.current?.focus();
  }, [disabled]);
  return (
    <section
      className="scene-word-stage"
      data-testid="scene-word-stage"
      data-room={room}
      data-motion={moving ? "on" : "off"}
    >
      <div
        ref={host}
        className="scene-word-room studio-visual"
        data-renderer={renderer}
        data-state={activity}
        aria-hidden="true"
      >
        <canvas ref={canvas} />
        {renderer !== "webgl" && (
          <div className="scene-room-fallback">
            <i className="fallback-window" />
            <i className="fallback-cabinet" />
            <i className="fallback-floor" />
          </div>
        )}
      </div>
      <span className="scene-room-caption">
        {room === "cafe" ? "Café" : room[0].toUpperCase() + room.slice(1)}{" "}
        <span>· Listen & discover</span>
      </span>
      {score !== undefined && score !== null && (
        <div
          className={`scene-verdict ${score === 100 ? "is-perfect" : "is-practice"}`}
          role="status"
        >
          {score === 100 ? "PERFECT!" : "Keep exploring"}
        </div>
      )}
      <div className="scene-word-panel">
        <span className="scene-word-kicker">Complete the sentence</span>
        <label className="scene-word-sentence">
          {round.gapText?.[0] ?? "Listen, then enter "}
          <input
            ref={input}
            aria-label="Missing word"
            value={value}
            maxLength={80}
            style={{
              width: `${Math.min(18, Math.max(9, value.length + 1))}ch`,
            }}
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
            disabled={disabled}
            onChange={(event) => onChange(event.target.value)}
            onKeyDown={(event) => {
              if (
                event.key === "Enter" &&
                !event.nativeEvent.isComposing &&
                value.trim()
              ) {
                event.preventDefault();
                onSubmit();
              }
            }}
          />
          {round.gapText?.[1] ?? ""}
        </label>
        <small>
          {score !== undefined
            ? "Your result is saved below."
            : disabled
              ? "Play the challenge above to unlock the blank."
              : "Type one word · Enter to check"}
        </small>
      </div>
    </section>
  );
}

export function SceneWordNotebook({
  notes,
  token,
}: {
  notes: SceneWordNotes;
  token: string;
}) {
  return (
    <section className="scene-word-notebook" aria-label="Word notebook">
      <header>
        <div>
          <span className="scene-note-label">Word notebook</span>
          <h3>{notes.word}</h3>
        </div>
        <SpeakerButton
          token={token}
          text={notes.word}
          language="en"
          label={`Hear ${notes.word}`}
        />
      </header>
      <p className="scene-word-translation">{notes.translation}</p>
      <p>{notes.meaning}</p>
      <div className="scene-notes-grid">
        <div>
          <h4>Common phrases</h4>
          <ul>
            {notes.phrases.map((phrase) => (
              <li key={phrase}>{phrase}</li>
            ))}
          </ul>
        </div>
        <div>
          <h4>
            In a sentence{" "}
            <SpeakerButton
              token={token}
              text={notes.example}
              language="en"
              label="Hear the example sentence"
            />
          </h4>
          <p>{notes.example}</p>
        </div>
      </div>
    </section>
  );
}
