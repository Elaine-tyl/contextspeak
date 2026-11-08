import { mountPronunciation, speakerIcon } from "./pronunciation";
import { learningDetailsSchema } from "@contextspeak/contracts";
// Never insert model HTML: definitions and examples are untrusted text.
export function renderLearningCard(
  container: HTMLElement,
  value: unknown,
  language: () => string,
  report: (message: string) => void,
) {
  const disposers: (() => void)[] = [];
  const dispose = () => disposers.forEach((dispose) => dispose());
  container.replaceChildren();
  const parsed = learningDetailsSchema.safeParse(value);
  if (!parsed.success) return dispose;
  const details = parsed.data;
  function line(tag: string, text: string) {
    const element = document.createElement(tag);
    element.textContent = text;
    container.append(element);
    return element;
  }
  function speaker(row: HTMLElement, text: string, label: string) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "pronounce";
    button.setAttribute("aria-label", label);
    button.innerHTML = speakerIcon;
    row.append(button);
    disposers.push(mountPronunciation(button, text, language, report).dispose);
  }
  line("small", "AI-generated learning notes");
  if (details.partOfSpeech) line("p", details.partOfSpeech);
  line("h3", "Meaning");
  line("p", details.definition);
  if (details.collocations.length) {
    line("h3", "Common phrases");
    for (const pair of details.collocations) {
      const row = line("p", pair.source);
      speaker(row, pair.source, "Listen to phrase");
      row.append(document.createTextNode(` — ${pair.translation}`));
    }
  }
  line("h3", "Examples");
  for (const pair of details.examples) {
    speaker(line("p", pair.source), pair.source, "Listen to example");
    line("p", pair.translation).className = "secondary example-translation";
  }
  if (details.notes) {
    line("h3", "Usage");
    line("p", details.notes);
  }
  return dispose;
}
