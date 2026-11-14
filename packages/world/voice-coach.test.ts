import { expect, it } from "vitest";
import { reviewSlice, sliceModelOutput, sliceInstruction } from "./slice";
const input = {
  band: "IELTS 3" as const,
  npc: "Maya",
  npcLine: "What would you like to drink?",
  task: "Order a drink",
  goal: "Order a drink",
  text: "I'd like a latte.",
};
const reply = {
  relevant: true,
  assessedGoal: true,
  goalMet: true,
  correction: "",
  explanation: "",
  acknowledgement: "Sure.",
  lexicalTip: "Could I have a latte, please?",
};
it("accepts optional transcript practice marks only with an exact current-answer quote", () => {
  const scores = {
    coherence: 80,
    lexical: 75,
    grammar: 90,
    evidence: input.text,
  };
  const marked = reviewSlice(input, { ...reply, languageScores: scores });
  expect(marked.languageScores).toEqual(scores);
  expect(
    reviewSlice(input, {
      ...reply,
      languageScores: { ...scores, evidence: "Invented answer." },
    }).languageScores,
  ).toBeNull();
});
it("rejects acoustic and band fields and out-of-range language marks", () => {
  const base = {
    ...reply,
    languageScores: {
      coherence: 80,
      lexical: 75,
      grammar: 101,
      evidence: input.text,
    },
  };
  expect(sliceModelOutput.safeParse(base).success).toBe(false);
  expect(
    sliceModelOutput.safeParse({ ...reply, pronunciation: 9 }).success,
  ).toBe(false);
});
it("keeps valid simple language error-free while offering a separate natural alternative", () => {
  const result = reviewSlice(input, reply);
  expect(result.correction).toBe("");
  expect(result.lexicalTip).toContain("latte");
  expect(sliceInstruction(input)).toContain("fluency cannot be measured");
});

it("requires concrete score evidence in the live local model schema", async () => {
  const { sliceModelOutputFor } = await import("./slice");
  const schema = sliceModelOutputFor(input);
  expect(schema.safeParse(reply).success).toBe(false);
  expect(
    schema.safeParse({
      ...reply,
      languageScores: {
        coherence: 80,
        lexical: 75,
        grammar: 90,
        evidence: input.text,
      },
    }).success,
  ).toBe(true);
  expect(
    schema.safeParse({
      ...reply,
      languageScores: {
        coherence: 80,
        lexical: 75,
        grammar: 90,
        evidence: "Invented answer.",
      },
    }).success,
  ).toBe(false);
});
