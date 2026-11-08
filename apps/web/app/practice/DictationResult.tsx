import type { DictationFeedback } from "@contextspeak/contracts";
export function DictationResult({ feedback }: { feedback: DictationFeedback }) {
  return (
    <section className="practice-answer" aria-label="Dictation feedback">
      <h3>Your result: {feedback.score}/100</h3>
      <p>
        {feedback.correctWords} of {feedback.totalWords} expected words matched.
      </p>
      <h4>Transcript</h4>
      <p>{feedback.transcript}</p>
      {feedback.score === 100 ? (
        <p>Well done. Your words match the audio.</p>
      ) : (
        <>
          {!!feedback.missing.length && (
            <p>Missing words: {feedback.missing.join(", ")}</p>
          )}
          {!!feedback.extra.length && (
            <p>Extra words: {feedback.extra.join(", ")}</p>
          )}
          {!!feedback.replacements.length && (
            <>
              <h4>Corrections: your word → expected word</h4>
              <ul>
                {feedback.replacements.map((item, index) => (
                  <li key={index}>
                    {item.actual} → {item.expected}
                  </li>
                ))}
              </ul>
            </>
          )}
          <p>Listen again, compare the transcript, then start a new attempt.</p>
        </>
      )}
      <small>
        Word accuracy: case and punctuation are ignored. Missing, extra and
        replaced words reduce the mark. This is practice feedback, not a mastery
        assessment.
      </small>
    </section>
  );
}
