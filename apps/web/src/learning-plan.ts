import {
  conversationScenarios,
  type Personalization,
  type PriorityItem,
  type ScenarioId,
} from "@contextspeak/contracts";
export const goalLabel = (id: ScenarioId) =>
  conversationScenarios.find((goal) => goal.id === id)!.title;
export function priorityHref(
  item: PriorityItem,
  goals: ScenarioId[],
  mode: "listening" | "speaking" = "listening",
) {
  const query = new URLSearchParams({
    lookup: item.lookupId,
    mode,
    returnTo: "/progress",
  });
  const scenario =
    item.matchedGoals?.find((goal) => goals.includes(goal)) ?? goals[0];
  if (scenario) query.set("scenario", scenario);
  return `/practice/${encodeURIComponent(item.id)}?${query}`;
}
// A quick-start task should not force a long translated passage into a word prompt.
export function dailyExpression(plan: Personalization | undefined) {
  return plan?.priorities.find((item) => {
    const word = item.word.trim();
    return (
      word.length > 0 && [...word].length <= 60 && word.split(/\s+/).length <= 6
    );
  });
}
export function practiceEntry(
  plan: Personalization | undefined,
  mode: "listening" | "speaking",
) {
  const item = dailyExpression(plan);
  return item && plan
    ? priorityHref(item, plan.profile.goals, mode)
    : `/library?view=practice&practiceMode=${mode}`;
}
