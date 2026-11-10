import { z } from "zod";
import { scenarioSchema } from "./practice";
export const scenarioSuggestionSchema = z
  .object({
    scenario: scenarioSchema,
    confidence: z.number().min(0).max(1),
    reason: z.string().trim().min(1).max(400),
    evidenceIds: z.array(z.uuid()).min(1).max(5),
  })
  .strict();
export const scenarioOutputSchema = z
  .object({ suggestions: z.array(scenarioSuggestionSchema).max(3) })
  .strict();
export const goalSettingsSchema = z
  .object({
    goals: z.array(scenarioSchema).max(3),
    dismissed: z.array(scenarioSchema).max(3).optional(),
    expectedGoals: z.array(scenarioSchema).max(3).optional(),
  })
  .strict();
export const learningProfileSchema = z
  .object({
    version: z.literal(1),
    goals: z.array(scenarioSchema).max(3),
    dismissed: z.array(scenarioSchema).max(3),
    suggestions: z.array(scenarioSuggestionSchema).max(3),
    analyzedContexts: z
      .array(z.string().regex(/^[a-f0-9]{64}$/))
      .max(500)
      .default([]),
    analyzedAt: z.iso.datetime().nullable(),
    analyzedThrough: z.string().nullable(),
    model: z.string().nullable(),
  })
  .strict();
export type LearningProfile = z.infer<typeof learningProfileSchema>;
export type GoalSettings = z.infer<typeof goalSettingsSchema>;
export type ScenarioSuggestion = z.infer<typeof scenarioSuggestionSchema>;
export type PriorityItem = {
  id: string;
  lookupId: string;
  word: string;
  targetLanguage: string;
  encounters: number;
  score: number;
  signals: {
    goal: number | null;
    encounters: number;
    frequency: number | null;
    gap: number | null;
    due: number;
  };
  goalClues: string[];
  matchedGoals: z.infer<typeof scenarioSchema>[];
  frequency: { rank: number; count: number } | null;
  practiceEvidence: number;
  due: boolean;
};
export type Personalization = {
  profile: LearningProfile;
  suggestionAvailable: boolean;
  contextCount: number;
  analysis: {
    status: "ready" | "up-to-date" | "needs-context" | "unavailable";
    newContexts: number;
    minimum: number;
  };
  historySampleSize: number;
  priorities: PriorityItem[];
  frequencySource: {
    title: string;
    url: string;
    license: string;
    licenseUrl: string;
  };
  evidence: { id: string; text: string; context: string | null }[];
};
