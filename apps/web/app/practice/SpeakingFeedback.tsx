import { Fragment } from "react";
import {
  correctionRanges,
  type SpeakingAssessment,
} from "@contextspeak/contracts";
export function SpeakingFeedback({ result }: { result: SpeakingAssessment }) {
  const ranges = correctionRanges(result.transcript, result.corrections ?? []);
  let offset = 0;
  return (
    <div className="practice-answer" aria-label="Speaking feedback">
      <h3>
        {result.overall === null
          ? "No reliable speaking score"
          : `AI speaking score: ${result.overall}/100`}
      </h3>
      <p>
        AI-estimated practice feedback. This is not an exam grade or a validated
        pronunciation assessment.
      </p>
      {result.scores && (
        <dl className="speaking-scores">
          {(
            [
              ["clarity", "Clarity"],
              ["fluency", "Fluency"],
              ["grammar", "Grammar"],
              ["vocabulary", "Vocabulary"],
              ["taskResponse", "Task response"],
            ] as const
          ).map(([key, label]) => (
            <div key={key}>
              <dt>{label}</dt>
              <dd>{result.scores![key]}/100</dd>
            </div>
          ))}
        </dl>
      )}
      {result.transcript && (
        <>
          <h4>Your transcript</h4>
          <p aria-label="Assessed transcript">
            {ranges.map((range) => {
              const before = result.transcript.slice(offset, range.start);
              offset = range.end;
              return (
                <Fragment key={range.start}>
                  {before}
                  <mark
                    className="practice-error"
                    title={`Suggested: ${range.corrected}. ${range.reason}`}
                  >
                    {range.original}
                  </mark>
                </Fragment>
              );
            })}
            {result.transcript.slice(offset)}
          </p>
          <small>
            Check the transcript against your recording. Recognition errors are
            not necessarily speaking mistakes.
          </small>
        </>
      )}
      {!!ranges.length && (
        <>
          <h4>Suggested corrections</h4>
          <ul>
            {ranges.map((range) => (
              <li key={range.start}>
                <mark className="practice-error">{range.original}</mark> →{" "}
                <strong>{range.corrected}</strong>
                <br />
                {range.reason}
              </li>
            ))}
          </ul>
          <p>
            Red highlights show suggested grammar or vocabulary changes, not a
            pronunciation diagnosis.
          </p>
        </>
      )}
      {!!result.pronunciation?.length && (
        <section aria-label="Pronunciation guidance">
          <h4>Pronunciation · IPA</h4>
          <p>
            AI reference pronunciation. / / encloses sounds; ˈ marks the
            stressed syllable. This does not transcribe exactly how you
            pronounced the word.
          </p>
          <ul>
            {result.pronunciation.map((item, index) => (
              <li key={index}>
                <strong>{item.word}</strong>{" "}
                <span className="ipa">{item.ipa}</span>
                <p>{item.stress}</p>
                <p>{item.tip}</p>
              </li>
            ))}
          </ul>
        </section>
      )}
      {!!result.strengths.length && (
        <>
          <h4>What went well</h4>
          <ul>
            {result.strengths.map((value, index) => (
              <li key={index}>{value}</li>
            ))}
          </ul>
        </>
      )}
      <h4>What to improve</h4>
      <ul>
        {result.improvements.map((value, index) => (
          <li key={index}>{value}</li>
        ))}
      </ul>
      {result.suggestedResponse && (
        <>
          <h4>Suggested response</h4>
          <p>{result.suggestedResponse}</p>
        </>
      )}
    </div>
  );
}
