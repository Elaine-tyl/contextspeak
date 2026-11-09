import type { AlignedWord } from "./practice-feedback";
import type { SpeakingAssessment } from "./speaking";
import { z } from "zod";
export const scenarioSchema = z.enum([
  "introductions",
  "making-plans",
  "asking-for-help",
]);
export type ScenarioId = z.infer<typeof scenarioSchema>;
export const conversationScenarios: { id: ScenarioId; title: string }[] = [
  { id: "introductions", title: "Introductions" },
  { id: "making-plans", title: "Making plans" },
  { id: "asking-for-help", title: "Asking for help" },
];
export const practiceQuerySchema = z
  .object({ lookup: z.uuid().optional(), scenario: scenarioSchema.optional() })
  .strict();
export const practiceSubmissionSchema = z
  .object({
    eventId: z.uuid(),
    lookupId: z.uuid(),
    scenario: scenarioSchema.optional(),
    mode: z.enum(["listening", "speaking"]),
    activity: z.literal("dictation").optional(),
    selfCheck: z.enum(["needs-practice", "comfortable"]).optional(),
    plays: z.number().int().min(0).max(20),
    completedPlays: z.number().int().min(0).max(20),
    revealed: z.boolean(),
    replayed: z.boolean(),
    response: z.string().trim().max(2000),
    durationMs: z.number().int().min(0).max(60000),
  })
  .strict()
  .superRefine((value, context) => {
    if (
      (value.activity === "dictation"
        ? value.mode !== "listening" ||
          value.revealed ||
          value.selfCheck !== undefined ||
          !/[\p{L}\p{N}]/u.test(value.response)
        : !value.selfCheck) ||
      value.completedPlays > value.plays ||
      (value.mode === "listening" &&
        (!value.completedPlays || !value.response)) ||
      (value.mode === "speaking" && (!value.durationMs || !value.replayed))
    )
      context.addIssue({
        code: "custom",
        message:
          "Complete playback and your answer, or record a response first.",
      });
  });
export type PracticeSubmission = z.infer<typeof practiceSubmissionSchema>;
export interface PracticeReceipt {
  scenario?: ScenarioId;
  id: string;
  mode: PracticeSubmission["mode"];
  selfCheck?: PracticeSubmission["selfCheck"];
  feedback?: DictationFeedback;
  speakingAssessment?: SpeakingAssessment;
  dialogue?: DialogueTurn;
  plays: number;
  revealed: boolean;
  durationMs: number;
  createdAt: string;
}
export interface PracticeTask {
  itemId: string;
  lookupId: string;
  version: 1;
  expression: string;
  sourceText: string;
  speakingQuestion?: string;
  translation: string;
  language: string | null;
  scenario?: { id: ScenarioId; title: string; goal: string };
  recent: PracticeReceipt[];
}

export interface PracticeItem {
  id: string;
  text: string;
  sourceLanguage: string;
  targetLanguage: string;
  lookupId: string;
}

export type DictationFeedback = {
  alignment?: AlignedWord[];
  method: "word-error-rate-v1";
  transcript: string;
  score: number;
  correctWords: number;
  totalWords: number;
  missing: string[];
  extra: string[];
  replacements: { expected: string; actual: string }[];
};

export type DialogueTurn = {
  turn: number;
  history: { partner: string; response: string }[];
};
