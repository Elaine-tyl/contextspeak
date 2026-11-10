import { expect, it } from "vitest";
import {
  frequencyFor,
  frequencySource,
} from "../src/personalization/frequency";
import { rankPriority, goalClues } from "../src/personalization/priority";
it("keeps subtitle provenance and unknown words explicit", () => {
  expect(frequencyFor("the")?.rank).toBeLessThan(10);
  expect(frequencyFor("never-a-real-entry-xyz")).toBeNull();
  expect(frequencySource.license).toBe("CC-BY-SA-4.0");
});
it("combines inspectable priority signals without treating unknown frequency or gaps as failure", () => {
  const base = {
    encounters: 1,
    goal: null,
    frequency: null,
    gap: null,
    due: false,
  };
  expect(rankPriority({ ...base, due: true }).score).toBeGreaterThan(
    rankPriority(base).score,
  );
  expect(rankPriority({ ...base, goal: 1 }).score).toBeGreaterThan(
    rankPriority(base).score,
  );
  expect(rankPriority(base).signals.frequency).toBeNull();
  expect(rankPriority(base).signals.gap).toBeNull();
  expect(rankPriority({ ...base, encounters: 10 }).score).toBeGreaterThan(
    rankPriority(base).score,
  );
  expect(goalClues(["making-plans"], "Let's meet this weekend.")).toContain(
    "meet",
  );
  expect(goalClues(["making-plans"], "The planet is large.")).not.toContain(
    "plan",
  );
});
it("does not boost missing frequency above an otherwise identical known word", () => {
  const input = { encounters: 3, goal: null, gap: null, due: false };
  const unknown = rankPriority({ ...input, frequency: null });
  const known = rankPriority({
    ...input,
    frequency: frequencyFor("available")!,
  });
  expect(unknown.score).toBeLessThanOrEqual(known.score);
  expect(unknown.signals.frequency).toBeNull();
});
