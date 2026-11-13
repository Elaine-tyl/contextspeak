"use client";
import { useEffect, useState } from "react";

/** Accessible tile builder for spelling (letters) and sentence builder (words). */
export function TileBuilder({
  tiles,
  placed,
  joiner,
  label,
  disabled,
  onChange,
}: {
  tiles: string[];
  placed: number[];
  joiner: "" | " ";
  label: string;
  disabled: boolean;
  onChange(value: number[]): void;
}) {
  const answer = placed.map((index) => tiles[index]).join(joiner);
  return (
    <fieldset className="tile-builder" disabled={disabled}>
      <legend>{label}</legend>
      <div className="tile-answer" aria-live="polite">
        {placed.length ? (
          placed.map((tileIndex, position) => (
            <button
              key={`${tileIndex}-${position}`}
              type="button"
              className="tile placed"
              aria-label={`Remove ${tiles[tileIndex]}`}
              onClick={() =>
                onChange(placed.filter((_, index) => index !== position))
              }
            >
              {tiles[tileIndex]}
            </button>
          ))
        ) : (
          <span className="tile-hint">Tap the tiles below in order.</span>
        )}
      </div>
      <p className="sr-only" role="status">
        {answer ? `Your answer: ${answer}` : ""}
      </p>
      <div className="tile-pool" aria-label="Available tiles">
        {tiles.map((tile, index) => {
          const used = placed.includes(index);
          return (
            <button
              key={index}
              type="button"
              className="tile"
              disabled={used}
              aria-label={used ? `${tile}, used` : `Add ${tile}`}
              onClick={() => onChange([...placed, index])}
            >
              {tile}
            </button>
          );
        })}
      </div>
      <button
        type="button"
        className="secondary"
        disabled={!placed.length}
        onClick={() => onChange([])}
      >
        Clear
      </button>
    </fieldset>
  );
}

export function QuickFireTimer({
  limit,
  startedAt,
  stopped,
}: {
  limit: number;
  startedAt: number | null;
  stopped: boolean;
}) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (startedAt === null || stopped) return;
    const timer = setInterval(() => setNow(Date.now()), 100);
    return () => clearInterval(timer);
  }, [startedAt, stopped]);
  const left =
    startedAt === null ? limit : Math.max(0, limit - (now - startedAt) / 1000);
  const ratio = left / limit;
  const tone = ratio > 0.5 ? "good" : ratio > 0.2 ? "warn" : "late";
  return (
    <div className="quick-timer">
      <div
        className={`quick-timer-bar ${tone}`}
        role="progressbar"
        aria-label="Time left for full marks"
        aria-valuemin={0}
        aria-valuemax={limit}
        aria-valuenow={Math.round(left)}
      >
        <span style={{ width: `${ratio * 100}%` }} />
      </div>
      <span className="quick-timer-label">
        {startedAt === null
          ? `${limit} s`
          : left > 0
            ? `${left.toFixed(1)} s`
            : "Time's up – you can still answer for 60"}
      </span>
    </div>
  );
}
