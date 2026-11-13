import { expect, it } from "vitest";
import { gapWordLengths } from "../app/games/gapLetters";

it("sizes letter boxes for each missing word using the spoken sentence", () => {
  expect(
    gapWordLengths(
      "Could you confirm the ingredients and check for allergens?",
      ["Could you confirm the ", " and check for ", "?"],
    ),
  ).toEqual([11, 9]);
});

it("handles adjacent gaps and a missing word at either edge", () => {
  expect(gapWordLengths("dietary requirements", ["", " ", ""])).toEqual([
    7, 12,
  ]);
  expect(gapWordLengths("We need clarification.", ["We need ", "."])).toEqual([
    13,
  ]);
});
