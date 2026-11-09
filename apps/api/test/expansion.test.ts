import { expect, it } from "vitest";
import { filterExpansion } from "../src/expansion/expansion.provider";
const word = (term: string, example = `This is a ${term}.`) => ({
  term,
  difference: "Different use",
  example: { source: example, translation: "Example translation" },
});
it("filters identical, duplicate, unrelated-spelling and substring-only examples", () => {
  const result = filterExpansion(
    {
      model: "fixture",
      synonyms: [word("shape"), word("FORM")],
      lookalikes: [word("farm", "A farmer works."), word("from")],
    },
    "form",
  );
  expect(result.synonyms.map((value) => value.term)).toEqual(["shape"]);
  expect(result.lookalikes.map((value) => value.term)).toEqual(["from"]);
});
