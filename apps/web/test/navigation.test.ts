import { expect, it } from "vitest";
import {
  libraryReturn,
  withLibraryReturn,
  practiceReturn,
} from "../src/navigation";
it("only_allows_local_library_return_targets", () => {
  expect(libraryReturn("/library?view=practice&practiceQ=available")).toBe(
    "/library?view=practice&practiceQ=available",
  );
  for (const value of [
    "https://evil.example/library",
    "//evil.example/library",
    "/auth/login",
    "/library/../pair",
  ])
    expect(libraryReturn(value)).toBe("/library");
  expect(
    withLibraryReturn(
      "/practice/item?lookup=lookup",
      "/library?view=practice&q=hello",
    ),
  ).toBe(
    "/practice/item?lookup=lookup&returnTo=%2Flibrary%3Fview%3Dpractice%26q%3Dhello",
  );
  expect(withLibraryReturn("/practice/item", "/library")).toBe(
    "/practice/item",
  );
});

it("keeps_practice_filters_when_choosing_the_next_expression", () => {
  expect(
    practiceReturn(
      "/library?view=practice&practiceQ=available&practiceLanguage=vi&practiceScenario=making-plans",
    ),
  ).toBe(
    "/library?view=practice&practiceQ=available&practiceLanguage=vi&practiceScenario=making-plans",
  );
  expect(practiceReturn("https://evil.example/")).toBe(
    "/library?view=practice",
  );
});
