import { Fragment } from "react";
import type { GapFillFeedback } from "@contextspeak/contracts";

export function GapFillSentence({
  parts,
  answers,
  disabled,
  onChange,
  onSubmit,
}: {
  parts: string[];
  answers: string[];
  disabled: boolean;
  onChange(value: string[]): void;
  onSubmit(): void;
}) {
  return (
    <fieldset
      className="gap-fill"
      disabled={disabled}
      onKeyDown={(event) => {
        if (event.key === "Enter" && answers.every((word) => word.trim())) {
          event.preventDefault();
          onSubmit();
        }
      }}
    >
      <legend>Fill in the missing words</legend>
      <p className="gap-fill-sentence">
        {parts.map((part, index) => (
          <Fragment key={index}>
            {part}
            {index < answers.length && (
              <input
                aria-label={`Missing word ${index + 1}`}
                value={answers[index]}
                maxLength={80}
                // Grow from entered text only; do not hint at the hidden word's length.
                size={Math.max(12, Math.min(26, answers[index].length + 3))}
                autoComplete="off"
                autoCapitalize="none"
                spellCheck={false}
                onChange={(event) =>
                  onChange(
                    answers.map((word, position) =>
                      position === index ? event.target.value : word,
                    ),
                  )
                }
              />
            )}
          </Fragment>
        ))}
      </p>
    </fieldset>
  );
}

export function GapFillResult({ feedback }: { feedback: GapFillFeedback }) {
  const correct = feedback.words.filter((word) => word.correct).length;
  return (
    <section className="gap-fill-result" aria-label="Missing words feedback">
      <h3>Your result: {feedback.score}/100</h3>
      <p>
        {correct} of {feedback.words.length} missing words correct.
      </p>
      <ol>
        {feedback.words.map((word, index) => (
          <li key={index}>
            <span>Blank {index + 1}</span>{" "}
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
