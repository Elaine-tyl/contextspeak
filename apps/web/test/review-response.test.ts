import { expect, it } from "vitest";
import { reviewQueueSchema } from "@contextspeak/contracts";
it("rejects malformed review responses instead of treating them as an empty queue", () => {
  for (const value of [
    {},
    { items: null, dueCount: 0, totalCount: 0 },
    { items: [{}], dueCount: 1, totalCount: 1 },
    { items: [], dueCount: -1, totalCount: 0 },
    { items: [], dueCount: 0, totalCount: "0" },
  ])
    expect(reviewQueueSchema.safeParse(value).success).toBe(false);
});
it("accepts an empty queue and validates complete review cards", () => {
  expect(
    reviewQueueSchema.parse({
      items: [],
      dueCount: 0,
      totalCount: 0,
      nextDueAt: null,
    }).items,
  ).toEqual([]);
  const card = {
    id: "opaque-id",
    version: 0,
    prompt: "available",
    answer: "有空的",
    context: null,
    sourceLanguage: "en",
    targetLanguage: "zh-Hans",
    dueAt: new Date().toISOString(),
  };
  expect(
    reviewQueueSchema.parse({
      items: [card],
      dueCount: 1,
      totalCount: 1,
      nextDueAt: null,
    }).items[0],
  ).toEqual(card);
});
