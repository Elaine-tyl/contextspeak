import { ConflictException } from "@nestjs/common";
import {
  conversationScenarios,
  type ScenarioId,
} from "@contextspeak/contracts";

// Original fixed prompts keep practice reproducible and require no model quota.
// Version 1 content is also saved in each attempt's server-side task snapshot.
const turns: Record<
  ScenarioId,
  { sourceText: string; translation: string; goal: string }
> = {
  introductions: {
    sourceText:
      "Hi, I'm Sam. It's nice to meet you. I enjoy cooking and walking. What do you enjoy doing, and what brings you here?",
    translation:
      "Sam introduces themself, mentions two interests, and asks about your interests and why you are here.",
    goal: "You are meeting someone at a community event. Introduce yourself, share an interest, and ask a follow-up question. Work your saved expression into a natural reply; choose another expression if it does not fit.",
  },
  "making-plans": {
    sourceText:
      "Would you like to meet for coffee this weekend? Saturday afternoon works for me, but we can choose another time. What would suit you?",
    translation:
      "Your friend suggests coffee this weekend, offers Saturday afternoon, and asks which time suits you.",
    goal: "Make plans with a friend. Suggest a time and place, explain when you can meet, and confirm one detail. Use your saved expression where it fits naturally.",
  },
  "asking-for-help": {
    sourceText:
      "Of course, I'll try to help. Could you tell me what you need and what you have already tried? Then we can work out the next step together.",
    translation:
      "The other person offers help and asks what you need and what you have already tried before deciding on a next step.",
    goal: "Ask someone for help with an everyday problem. Explain what you need, mention what you have tried, and ask politely for a next step. Use your saved expression where it fits naturally.",
  },
};
export function conversationTask(id: ScenarioId, language: string | null) {
  if (language !== "en")
    throw new ConflictException(
      "Everyday scenarios currently require saved English vocabulary with a known English source language.",
    );
  const { goal, ...turn } = turns[id];
  return {
    ...turn,
    scenario: {
      id,
      title: conversationScenarios.find((value) => value.id === id)!.title,
      goal,
    },
  };
}
