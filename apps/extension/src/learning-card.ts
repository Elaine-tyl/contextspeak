import { learningDetailsSchema } from "@contextspeak/contracts";
// Never insert model HTML: definitions and examples are untrusted text.
export function renderLearningCard(container: HTMLElement, value: unknown) {
  container.replaceChildren();
  const parsed = learningDetailsSchema.safeParse(value);
  if (!parsed.success) return;
  const details = parsed.data;
  function line(tag: string, text: string) {
    const element = document.createElement(tag);
    element.textContent = text;
    container.append(element);
    return element;
  }
  line("small", "AI-generated learning notes");
  if (details.partOfSpeech) line("p", details.partOfSpeech);
  line("h3", "Meaning");
  line("p", details.definition);
  if (details.collocations.length) {
    line("h3", "Common phrases");
    for (const pair of details.collocations) {
      line("p", `${pair.source} — ${pair.translation}`);
    }
  }
  line("h3", "Examples");
  for (const pair of details.examples) {
    line("p", pair.source);
    line("p", pair.translation).className = "secondary example-translation";
  }
  if (details.notes) {
    line("h3", "Usage");
    line("p", details.notes);
  }
}
