import type { ReactNode } from "react";
import { alignWords, type DictationFeedback } from "@contextspeak/contracts";
export function DictationResult({
  feedback,
  response = "",
  language,
  children,
}: {
  feedback: DictationFeedback;
  response?: string;
  language?: string | null;
  children?: ReactNode;
}) {
  return (
    <section className="practice-answer" aria-label="Dictation feedback">
      <h3>Your result: {feedback.score}/100</h3>
      <p>
        {feedback.correctWords} of {feedback.totalWords} expected words matched.
      </p>
      <h4>Checked answer</h4>
      <p aria-label="Highlighted answer">
        {(
          feedback.alignment ??
          alignWords(feedback.transcript, response, language)
        ).map((word, index) => (
          <span key={index}>
            {word.status === "correct" ? (
              word.actual
            ) : (
              <mark
                className="practice-error"
                title={
                  word.status === "missing"
                    ? `Missing: ${word.expected}`
                    : word.status === "extra"
                      ? "Extra word"
                      : `Expected: ${word.expected}`
                }
              >
                {word.status === "missing"
                  ? `[missing: ${word.expected}]`
                  : word.status === "extra"
                    ? `${word.actual} [extra]`
                    : `${word.actual} → ${word.expected}`}
              </mark>
            )}{" "}
          </span>
        ))}
      </p>
      <small>
        Red highlights: replaced, missing or extra words. Case and punctuation
        are ignored.
      </small>
      <h4>Transcript</h4>
      <p>{feedback.transcript}</p>
      {feedback.score === 100 ? (
        <p>Well done. Your words match the audio.</p>
      ) : (
        <p>Listen again, compare the transcript, then start a new attempt.</p>
      )}
      {children}
      <small>
        Word accuracy: case and punctuation are ignored. Missing, extra and
        replaced words reduce the mark. This is practice feedback, not a mastery
        assessment.
      </small>
    </section>
  );
}
