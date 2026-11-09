"use client";
import { SpeakerButton } from "./SpeakerButton";
import { learningDetailsSchema } from "@contextspeak/contracts";
export function LearningNotes({
  value,
  sourceLanguage,
  token,
}: {
  value: unknown;
  sourceLanguage: string | null;
  token: string;
}) {
  const parsed = learningDetailsSchema.safeParse(value);
  if (!parsed.success) return null;
  const details = parsed.data;
  return (
    <section className="learning-notes" data-testid="learning-notes">
      <small>AI-generated learning notes</small>
      {details.partOfSpeech && <p>{details.partOfSpeech}</p>}
      <h4>Meaning</h4>
      <p>{details.definition}</p>
      {details.collocations.length > 0 && (
        <>
          <h4>Common phrases</h4>
          <ul>
            {details.collocations.map((pair, index) => (
              <li key={index}>
                {pair.source}
                <SpeakerButton
                  token={token}
                  text={pair.source}
                  language={sourceLanguage}
                  label="Listen to phrase"
                />
                <br />
                {pair.translation}
              </li>
            ))}
          </ul>
        </>
      )}
      <h4>Examples</h4>
      <ol>
        {details.examples.map((pair, index) => (
          <li key={index}>
            {pair.source}
            <SpeakerButton
              token={token}
              text={pair.source}
              language={sourceLanguage}
              label="Listen to example"
            />
            <br />
            {pair.translation}
          </li>
        ))}
      </ol>
      {details.expansion &&
        (
          [
            ["Similar meanings", details.expansion.synonyms],
            ["Similar spellings", details.expansion.lookalikes],
          ] as const
        ).map(
          ([title, words]) =>
            words.length > 0 && (
              <section key={title} aria-label={title}>
                <h4>{title}</h4>
                <ul>
                  {words.map((word) => (
                    <li key={word.term}>
                      <strong>{word.term}</strong>
                      <SpeakerButton
                        token={token}
                        text={word.term}
                        language={sourceLanguage}
                        label="Listen to related word"
                      />
                      <p>{word.difference}</p>
                      <p>
                        {word.example.source}
                        <SpeakerButton
                          token={token}
                          text={word.example.source}
                          language={sourceLanguage}
                          label="Listen to related example"
                        />
                        <br />
                        {word.example.translation}
                      </p>
                    </li>
                  ))}
                </ul>
              </section>
            ),
        )}
      {details.notes && (
        <>
          <h4>Usage</h4>
          <p>{details.notes}</p>
        </>
      )}
    </section>
  );
}
