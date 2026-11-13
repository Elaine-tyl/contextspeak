"use client";
import { useRef, useState, type CSSProperties } from "react";
import type { CrosswordEntry } from "@contextspeak/contracts";
import { LetterWord } from "./LetterWord";

type Cell = { word: number; offset: number };
export function Crossword({
  entries,
  answers,
  disabled,
  onChange,
  onSubmit,
}: {
  entries: CrosswordEntry[];
  answers: string[];
  disabled: boolean;
  onChange(value: string[]): void;
  onSubmit(): void;
}) {
  const inputs = useRef<(HTMLInputElement | null)[]>([]);
  const [active, setActive] = useState(0);
  const cells = new Map<string, Cell[]>();
  let rows = 0;
  let columns = 0;
  entries.forEach((entry, word) => {
    for (let offset = 0; offset < entry.length; offset++) {
      const row = entry.row + (entry.direction === "down" ? offset : 0);
      const column = entry.column + (entry.direction === "across" ? offset : 0);
      const key = `${row}:${column}`;
      cells.set(key, [...(cells.get(key) ?? []), { word, offset }]);
      rows = Math.max(rows, row + 1);
      columns = Math.max(columns, column + 1);
    }
  });
  function conflict(cell: Cell[]) {
    return (
      new Set(
        cell
          .map(({ word, offset }) => answers[word]?.[offset]?.toLowerCase())
          .filter(Boolean),
      ).size > 1
    );
  }
  const conflicts = [...cells.values()].filter(conflict).length;
  return (
    <section className="crossword" aria-label="Crossword puzzle">
      <div
        className="crossword-grid"
        aria-hidden="true"
        style={{ "--crossword-columns": columns } as CSSProperties}
      >
        {Array.from({ length: rows * columns }, (_, index) => {
          const row = Math.floor(index / columns);
          const column = index % columns;
          const cell = cells.get(`${row}:${column}`);
          if (!cell) return <span key={index} />;
          const selected = cell.find((item) => item.word === active) ?? cell[0];
          const letter =
            answers[selected.word]?.[selected.offset] ||
            cell
              .map(({ word, offset }) => answers[word]?.[offset])
              .find(Boolean) ||
            "";
          const starts = entries.flatMap((entry, word) =>
            entry.row === row && entry.column === column ? [word + 1] : [],
          );
          return (
            <span
              key={index}
              className={`crossword-cell ${cell.some((item) => item.word === active) ? "is-active" : ""} ${conflict(cell) ? "is-conflict" : ""}`}
              onPointerDown={(event) => {
                if (disabled) return;
                event.preventDefault();
                inputs.current[selected.word]?.focus();
                inputs.current[selected.word]?.setSelectionRange(
                  selected.offset,
                  selected.offset + 1,
                );
              }}
            >
              {starts.length > 0 && <small>{starts.join("/")}</small>}
              {letter}
            </span>
          );
        })}
      </div>
      <p
        className={conflicts ? "crossword-conflict" : "section-copy"}
        role="status"
      >
        {conflicts
          ? "Crossing letters do not match. Check both clues."
          : "Two clues, one shared letter."}
      </p>
      <fieldset className="crossword-clues" disabled={disabled}>
        <legend>Solve the clues</legend>
        {entries.map((entry, index) => (
          <div
            key={index}
            className="crossword-clue"
            onFocusCapture={() => setActive(index)}
          >
            <strong>
              {index + 1} · {entry.direction === "across" ? "Across" : "Down"} ·{" "}
              {entry.length} letters
            </strong>
            <p>{entry.clue}</p>
            <LetterWord
              value={answers[index] ?? ""}
              length={entry.length}
              label={`Clue ${index + 1}: ${entry.clue}`}
              inputRef={(input) => {
                inputs.current[index] = input;
              }}
              onChange={(value) =>
                onChange(
                  answers.map((answer, word) =>
                    word === index
                      ? value.replace(/[^a-z]/giu, "").toLowerCase()
                      : answer,
                  ),
                )
              }
              onComplete={() => inputs.current[index + 1]?.focus()}
              onKeyDown={(event) => {
                if (event.key !== "Enter" || event.nativeEvent.isComposing)
                  return;
                event.preventDefault();
                if (index < entries.length - 1)
                  inputs.current[index + 1]?.focus();
                else if (answers.every((answer) => answer.trim())) onSubmit();
              }}
            />
          </div>
        ))}
        <p id="gap-typing-hint" className="section-copy">
          One letter per box. Tab switches clues. Enter checks the last clue.
        </p>
      </fieldset>
    </section>
  );
}
