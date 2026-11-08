import { expect, it } from "vitest";
import { learningDetailsSchema } from "@contextspeak/contracts";
import { learningFixture } from "../../../tests/fixtures/learning";
import { prepareLearning } from "../src/library/ai-learning";
it("prioritizes_the_contextual_sense_in_the_primary_translation", () => {
  const prepared = prepareLearning(
    {
      text: "Whether",
      context:
        "Whether the review is short or extended, similar features apply.",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
    },
    true,
  );
  expect(prepared.prompt).toContain("meaning in the supplied context");
  expect(prepared.prompt).toContain(
    "Do not lead with a different dictionary sense",
  );
  expect(prepared.data.context).toContain("short or extended");
});

it("keeps_saved_and_future_prompt_metadata_compatible_with_learning_cards", () => {
  for (const promptVersion of [1, 2, 3])
    expect(
      learningDetailsSchema.safeParse({ ...learningFixture, promptVersion })
        .success,
    ).toBe(true);
  expect(
    learningDetailsSchema.safeParse({ ...learningFixture, promptVersion: 0 })
      .success,
  ).toBe(false);
});
