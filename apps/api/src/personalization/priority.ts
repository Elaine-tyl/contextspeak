import type { PriorityItem, ScenarioId } from "@contextspeak/contracts";
const terms: Record<ScenarioId, string[]> = {
  introductions: [
    "hello",
    "introduce",
    "name",
    "hobby",
    "hobbies",
    "friend",
    "interests",
  ],
  "making-plans": [
    "meet",
    "meeting",
    "weekend",
    "schedule",
    "reschedule",
    "available",
    "appointment",
    "plan",
    "plans",
    "coffee",
  ],
  "asking-for-help": [
    "help",
    "problem",
    "assist",
    "assistance",
    "lost",
    "directions",
    "repair",
    "borrow",
  ],
};
export function goalClues(goals: ScenarioId[], text: string) {
  const words = new Set(text.toLowerCase().match(/[a-z]+/g) ?? []);
  return [
    ...new Set(
      goals.flatMap((goal) => terms[goal].filter((word) => words.has(word))),
    ),
  ];
}
export function rankPriority(input: {
  encounters: number;
  goal: number | null;
  frequency: { rank: number; count: number } | null;
  gap: number | null;
  due: boolean;
}) {
  const signals: PriorityItem["signals"] = {
    goal: input.goal,
    encounters: Math.min(1, Math.log2(input.encounters + 1) / Math.log2(11)),
    frequency: input.frequency
      ? Math.max(0, 1 - Math.log(input.frequency.rank) / Math.log(5001))
      : null,
    gap: input.gap,
    due: input.due ? 1 : 0,
  };
  const weights = {
    goal: 0.3,
    encounters: 0.25,
    frequency: 0.2,
    gap: 0.15,
    due: 0.1,
  };
  let value = 0,
    total = 0;
  for (const key of Object.keys(weights) as (keyof typeof weights)[])
    if (signals[key] !== null) {
      value += signals[key]! * weights[key];
      total += weights[key];
    }
  return { score: Math.round((100 * value) / total), signals };
}
