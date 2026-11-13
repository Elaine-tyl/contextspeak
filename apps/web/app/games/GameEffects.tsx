"use client";
import {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { GameAudio, type GameCue } from "./gameAudio";
const Sound = createContext<(cue: GameCue) => void>(() => {});
const preference = "contextspeak.game-sound";
export function useGameSound() {
  return useContext(Sound);
}

export function GameEffects({ children }: { children: ReactNode }) {
  const audio = useRef<GameAudio | null>(null);
  const [enabled, setEnabled] = useState(true);
  useEffect(() => {
    const player = new GameAudio();
    audio.current = player;
    try {
      player.muted = localStorage.getItem(preference) === "off";
    } catch {}
    setEnabled(!player.muted);
    return () => {
      player.dispose();
      audio.current = null;
    };
  }, []);
  function toggle() {
    const next = !enabled;
    setEnabled(next);
    if (audio.current) audio.current.muted = !next;
    try {
      localStorage.setItem(preference, next ? "on" : "off");
    } catch {}
    if (next) void audio.current?.unlock();
  }
  return (
    <Sound.Provider value={(cue) => audio.current?.play(cue)}>
      <div
        className="game-experience"
        onPointerDownCapture={() => {
          void audio.current?.unlock();
        }}
        onClickCapture={(event) => {
          void audio.current?.unlock();
          // Never beep over lesson playback or microphone controls.
          if ((event.target as HTMLElement).closest(".tile, .game-choice"))
            audio.current?.play("select");
        }}
      >
        <div className="game-toolbar">
          <span>YOUR DAILY PLAYGROUND</span>
          <button
            type="button"
            className="secondary game-sound"
            aria-pressed={enabled}
            onClick={toggle}
          >
            <span aria-hidden="true">{enabled ? "♫" : "♪"}</span> Sound{" "}
            {enabled ? "on" : "off"}
          </button>
        </div>
        {children}
      </div>
    </Sound.Provider>
  );
}

export function GameCelebration({
  score,
  complete = false,
}: {
  score: number | null;
  complete?: boolean;
}) {
  const play = useContext(Sound);
  const emitted = useRef(false);
  const passed = score !== null && score >= 70;
  useEffect(() => {
    if (emitted.current || score === null) return;
    emitted.current = true;
    play(passed ? (complete ? "complete" : "success") : "retry");
  }, [score, passed, complete, play]);
  return (
    <div className={`game-celebration ${passed ? "passed" : "practice"}`}>
      <div className="game-score-orb">
        <strong>{score ?? "—"}</strong>
        <span>{score === null ? "Not scored" : "out of 100"}</span>
      </div>
      <div>
        <p className="eyebrow">
          {complete ? "LEVEL COMPLETE" : "CHALLENGE CHECKED"}
        </p>
        <h3>
          {score === null
            ? "Let's get clearer audio"
            : passed
              ? "Nicely done!"
              : "Every try is progress"}
        </h3>
        <p>
          {passed
            ? "Keep the momentum going."
            : "Use the feedback below for your next try."}
        </p>
      </div>
      {passed && (
        <div className="game-confetti" aria-hidden="true">
          {Array.from({ length: 12 }, (_, i) => (
            <i key={i} style={{ "--i": i } as React.CSSProperties} />
          ))}
        </div>
      )}
    </div>
  );
}
