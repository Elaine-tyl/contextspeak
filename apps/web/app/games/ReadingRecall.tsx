import { Fragment } from "react";
import type { GameRound } from "@contextspeak/contracts";
import { GapFillSentence } from "./GapFill";
import { gapWordLengths } from "./gapLetters";

/** The marked preview disappears only after a complete, successful narration. */
export function ReadingRecall({
  round,
  completed,
  answers,
  disabled,
  onChange,
  onSubmit,
}: {
  round: GameRound;
  completed: boolean;
  answers: string[];
  disabled: boolean;
  onChange(value: string[]): void;
  onSubmit(): void;
}) {
  const parts = round.gapText!;
  const lengths = gapWordLengths(round.audioText, parts);
  let cursor = 0;
  return (
    <section className="reading-recall" aria-label="Read and recall">
      <p className="recall-instruction" role="status">
        {completed
          ? "The highlighted words are now hidden. Fill every blank, then check your answers."
          : `${lengths.length} highlighted words will disappear after the audio finishes. Press Start to listen and read.`}
      </p>
      {completed ? (
        <GapFillSentence
          parts={parts}
          audioText={round.audioText}
          answers={answers}
          disabled={disabled}
          legend="Fill the highlighted words"
          onChange={onChange}
          onSubmit={onSubmit}
        />
      ) : (
        <div
          className="recall-preview text-game-passage"
          aria-label="Reading passage"
        >
          {parts.map((part, index) => {
            const start = cursor + part.length;
            const word = round.audioText.slice(
              start,
              start + (lengths[index] ?? 0),
            );
            cursor = start + word.length;
            return (
              <Fragment key={index}>
                {part}
                {index < lengths.length && (
                  <mark className="recall-keyword">
                    {word}
                    <sup aria-hidden="true">{index + 1}</sup>
                  </mark>
                )}
              </Fragment>
            );
          })}
        </div>
      )}
    </section>
  );
}
