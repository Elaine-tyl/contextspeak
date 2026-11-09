import { expect, it, vi } from "vitest";
import { dialogueContext } from "../src/practice/dialogue";
const task = {
  itemId: "item-a",
  sourceText: "When can we meet?",
  scenario: { id: "making-plans" },
};
const input = {
  lookupId: "lookup-a",
  scenario: "making-plans",
  conversation: true,
  previousTurnId: "turn-a",
};
function row(patch = {}) {
  return {
    payload: { conversation: true, scenario: "making-plans" },
    task: {
      ...task,
      dialogue: { turn: 1, history: [] },
      speakingAssessment: {
        speechDetected: true,
        transcript: "Saturday works.",
        nextTurn: "Where shall we meet?",
      },
    },
    ...patch,
  };
}
it("derives follow-up and history only from the learner's owned saved turn", async () => {
  const findFirst = vi.fn(async (_query: unknown) => row());
  const result = await dialogueContext(
    { practiceAttempt: { findFirst } } as any,
    "learner",
    task as any,
    input as any,
  );
  expect(findFirst.mock.calls[0][0]).toMatchObject({
    where: { id: "turn-a", userId: "learner", lookupId: "lookup-a" },
  });
  expect(result).toEqual({
    sourceText: "Where shall we meet?",
    dialogue: {
      turn: 2,
      history: [{ partner: "When can we meet?", response: "Saturday works." }],
    },
  });
});
it("rejects missing, mismatched, silent and completed chains", async () => {
  for (const saved of [
    null,
    row({ payload: {} }),
    row({ task: { ...row().task, itemId: "other" } }),
    row({
      task: { ...row().task, speakingAssessment: { speechDetected: false } },
    }),
    row({ task: { ...row().task, dialogue: { turn: 4, history: [] } } }),
  ]) {
    await expect(
      dialogueContext(
        { practiceAttempt: { findFirst: async () => saved } } as any,
        "learner",
        task as any,
        input as any,
      ),
    ).rejects.toBeDefined();
  }
});
