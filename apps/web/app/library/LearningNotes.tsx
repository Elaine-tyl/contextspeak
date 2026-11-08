import { learningDetailsSchema } from "@contextspeak/contracts";
export function LearningNotes({ value }: { value: unknown }) {
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
            <br />
            {pair.translation}
          </li>
        ))}
      </ol>
      {details.notes && (
        <>
          <h4>Usage</h4>
          <p>{details.notes}</p>
        </>
      )}
    </section>
  );
}
