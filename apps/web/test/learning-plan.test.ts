import { expect, it } from "vitest";
import { priorityHref, practiceEntry } from "../src/learning-plan";
import type { PriorityItem } from "@contextspeak/contracts";
it("opens the requested skill with the matching confirmed goal and a safe Dashboard return", () => {
  const item = {
    id: "owned-item",
    lookupId: "owned-lookup",
    matchedGoals: ["asking-for-help"],
  } as PriorityItem;
  const href = priorityHref(
    item,
    ["introductions", "asking-for-help"],
    "speaking",
  );
  const query = new URL(href, "http://contextspeak.invalid").searchParams;
  expect(query.get("mode")).toBe("speaking");
  expect(query.get("scenario")).toBe("asking-for-help");
  expect(query.get("returnTo")).toBe("/progress");
  expect(practiceEntry(undefined, "speaking")).toBe(
    "/library?view=practice&practiceMode=speaking",
  );
});
it("prefers a manageable short expression for quick-start practice without deleting longer lookups", () => {
  const long = {
    id: "long",
    lookupId: "long-lookup",
    word: "AI voice quota reached or busy. Try again shortly; cached audio remains available.",
    matchedGoals: [],
  };
  const short = {
    id: "short",
    lookupId: "short-lookup",
    word: "available",
    matchedGoals: [],
  };
  const plan = {
    priorities: [long, short],
    profile: { goals: [] },
  } as unknown as import("@contextspeak/contracts").Personalization;
  expect(practiceEntry(plan, "speaking")).toContain("/practice/short?");
  expect(
    practiceEntry(
      { ...plan, priorities: [long as unknown as PriorityItem] },
      "speaking",
    ),
  ).toBe("/library?view=practice&practiceMode=speaking");
  expect(plan.priorities).toHaveLength(2);
});
