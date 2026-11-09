import { expect, it } from "vitest";
import { alignWords, correctionRanges } from "@contextspeak/contracts";
it("aligns repeated words by position and labels omissions and insertions", () => {
  expect(alignWords("I can can help you", "I can help now")).toEqual([
    { actual: "I", expected: "I", status: "correct" },
    { actual: "", expected: "can", status: "missing" },
    { actual: "can", expected: "can", status: "correct" },
    { actual: "help", expected: "help", status: "correct" },
    { actual: "now", expected: "you", status: "replaced" },
  ]);
  expect(alignWords("Hello!", "hello there")).toEqual([
    { actual: "hello", expected: "Hello", status: "correct" },
    { actual: "there", expected: "", status: "extra" },
  ]);
});
it("only highlights exact unique nonoverlapping transcript evidence", () => {
  const correction = (original: string) => ({
    original,
    corrected: "am available",
    reason: "Use am.",
  });
  expect(
    correctionRanges("I available Saturday.", [correction("I available")]),
  ).toMatchObject([{ start: 0, end: 11 }]);
  expect(
    correctionRanges("go go", [correction("go"), correction("invented")]),
  ).toEqual([]);
  expect(
    correctionRanges("I available Saturday.", [
      correction("I available"),
      correction("available Saturday"),
    ]),
  ).toHaveLength(1);
});

it("rejects partial-word and unchanged speaking corrections", () => {
  const correction = (original: string, corrected = "better") => ({
    original,
    corrected,
    reason: "Check wording.",
  });
  expect(
    correctionRanges("The shop is open.", [
      correction("he"),
      correction("pen"),
    ]),
  ).toEqual([]);
  expect(correctionRanges("I'm ready.", [correction("I")])).toEqual([]);
  expect(
    correctionRanges("I am ready.", [correction("ready", "ready")]),
  ).toEqual([]);
  expect(
    correctionRanges("I available, thanks.", [
      correction("I available", "I am available"),
    ]),
  ).toHaveLength(1);
});
