import type { GameRound } from "@contextspeak/contracts";

/** Keep the complete story readable above the answer controls. */
export function EvidenceHunt({
  passage,
  question,
  choices,
  selected,
  disabled,
  onSelect,
}: {
  passage?: string;
  question: string;
  choices: NonNullable<GameRound["choices"]>;
  selected: string;
  disabled: boolean;
  onSelect(value: string): void;
}) {
  // Older saved rounds may contain only the original ordered sentences.
  const text =
    passage?.trim() || choices.map((sentence) => sentence.label).join(" ");
  return (
    <>
      <section
        className="text-game-passage evidence-story"
        aria-label="Reading passage"
      >
        <h3>Read the passage</h3>
        {text.split(/\n\s*\n/u).map((paragraph, index) => (
          <p key={index}>{paragraph}</p>
        ))}
      </section>
      <fieldset className="evidence-choices" disabled={disabled}>
        <legend>Choose the evidence</legend>
        <p className="evidence-question">{question}</p>
        <p className="evidence-hint">
          Select the sentence that supports your answer.
        </p>
        {choices.map((sentence, index) => (
          <button
            key={sentence.id}
            className={`game-choice evidence-line ${selected === sentence.id ? "selected" : ""}`}
            disabled={disabled}
            aria-pressed={selected === sentence.id}
            aria-label={sentence.label}
            onClick={() => onSelect(sentence.id)}
          >
            <span className="evidence-number" aria-hidden="true">
              {index + 1}
            </span>
            <span>{sentence.label}</span>
          </button>
        ))}
      </fieldset>
    </>
  );
}
