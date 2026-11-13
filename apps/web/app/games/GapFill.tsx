import { Fragment, useEffect, useRef } from "react";
import type { GapFillFeedback } from "@contextspeak/contracts";
import { gapWordLengths } from "./gapLetters";
import { LetterWord } from "./LetterWord";

export function GapFillSentence({
  parts,
  audioText,
  answers,
  disabled,
  onChange,
  onSubmit,
  legend = "Fill the letter boxes",
}: {
  parts: string[];
  audioText: string;
  answers: string[];
  disabled: boolean;
  onChange(value: string[]): void;
  onSubmit(): void;
  legend?: string;
}) {
  const inputs = useRef<(HTMLInputElement | null)[]>([]);
  const lengths = gapWordLengths(audioText, parts);
  useEffect(() => {
    // Start typing after listening, without moving focus again on each keystroke.
    if (!disabled)
      inputs.current.find((input) => input && !input.value.trim())?.focus();
  }, [disabled]);

  return (
    <fieldset className="gap-fill" disabled={disabled}>
      <legend>{legend}</legend>
      <p className="gap-fill-sentence">
        {parts.map((part, index) => (
          <Fragment key={index}>
            {part}
            {index < answers.length && (
              <LetterWord
                inputRef={(input) => {
                  inputs.current[index] = input;
                }}
                label={`Missing word ${index + 1}`}
                value={answers[index]}
                length={lengths[index]}
                onComplete={() => inputs.current[index + 1]?.focus()}
                onKeyDown={(event) => {
                  if (event.key !== "Enter" || event.nativeEvent.isComposing)
                    return;
                  event.preventDefault();
                  if (!answers[index].trim()) return;
                  const nextEmpty = inputs.current.find(
                    (input) => input && !input.value.trim(),
                  );
                  if (index < answers.length - 1)
                    inputs.current[index + 1]?.focus();
                  else if (nextEmpty) nextEmpty.focus();
                  else onSubmit();
                }}
                onChange={(value) =>
                  onChange(
                    answers.map((word, position) =>
                      position === index ? value : word,
                    ),
                  )
                }
              />
            )}
          </Fragment>
        ))}
      </p>
      <div className="gap-typing-footer">
        <small id="gap-typing-hint">
          One letter per box · Tab to switch words · Enter to check
        </small>
        <small>
          {
            answers.filter((word, index) => word.length === lengths[index])
              .length
          }{" "}
          / {answers.length} filled
        </small>
      </div>
    </fieldset>
  );
}

export function GapFillResult({
  feedback,
  crossword = false,
}: {
  feedback: GapFillFeedback;
  crossword?: boolean;
}) {
  const correct = feedback.words.filter((word) => word.correct).length;
  return (
    <section
      className="gap-fill-result"
      aria-label={crossword ? "Crossword feedback" : "Missing words feedback"}
    >
      <h3>Your result: {feedback.score}/100</h3>
      <p>
        {correct} of {feedback.words.length}{" "}
        {crossword ? "clues" : "missing words"} correct.
      </p>
      <ol>
        {feedback.words.map((word, index) => (
          <li key={index}>
            <span>
              {crossword ? "Clue" : "Blank"} {index + 1}
            </span>{" "}
            {word.correct ? (
              <strong className="gap-fill-correct">
                Correct: {word.expected}
              </strong>
            ) : (
              <>
                <span>Correction: </span>
                <mark className="practice-error">
                  {word.answer} → {word.expected}
                </mark>
              </>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}

export function GapFillResources() {
  return (
    <details className="game-resources">
      <summary>Free news and IELTS practice</summary>
      <p>
        Choose Academic study for original academic exercises, or a News theme
        for education, technology, work and environment reports. These are
        editorial practice levels. Game levels are not IELTS band scores.
      </p>
      <ul>
        <li>
          <a
            href="https://downloads.bbc.co.uk/learningenglish/features/LEFTN/250827_LEFTN_heatwaves_speed_up_ageing_process_study_says_worksheet.pdf"
            target="_blank"
            rel="noreferrer"
          >
            BBC Learning English · news vocabulary
          </a>
          <small>Official worksheet: heatwaves and ageing. Opens on BBC.</small>
        </li>
        <li>
          <a
            href="https://www.bbc.co.uk/learningenglish"
            target="_blank"
            rel="noreferrer"
          >
            BBC Learning English · programmes and courses
          </a>
        </li>
        <li>
          <a
            href="https://takeielts.britishcouncil.org/prepare/ielts-free-practice-mock-tests/academic/listening"
            target="_blank"
            rel="noreferrer"
          >
            British Council · free IELTS listening practice
          </a>
        </li>
        <li>
          <a
            href="https://ielts.org/take-a-test/preparation-resources/sample-test-questions"
            target="_blank"
            rel="noreferrer"
          >
            IELTS · official sample questions
          </a>
        </li>
        <li>
          <a
            href="https://learningenglish.voanews.com/"
            target="_blank"
            rel="noreferrer"
          >
            VOA Learning English · news and learning
          </a>
        </li>
      </ul>
      <small>
        BBC and official IELTS materials open on their publishers’ websites.
        ContextSpeak academic exercises are independently written.
      </small>
    </details>
  );
}
