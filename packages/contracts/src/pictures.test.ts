import { readFileSync } from "node:fs";
import { expect, it } from "vitest";
import { pictureIds, pictureLevels, pictureLibrary } from "./pictures";
import { pictureJson } from "../../../tools/arcade/export-pictures";

it("keeps the Godot picture library identical to the shared one", () => {
  const godot = readFileSync(
    new URL("../../../games/arcade/pictures.json", import.meta.url),
    "utf8",
  );
  expect(godot, "Run pnpm arcade:pictures").toBe(pictureJson());
});

it("draws every picture as a small, script-free SVG", () => {
  for (const id of pictureIds) {
    const { svg, label } = pictureLibrary[id];
    expect(label.length).toBeGreaterThan(0);
    expect(
      svg.startsWith(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 120">',
      ),
    ).toBe(true);
    expect(svg).not.toMatch(/<script|on\w+=|href=/i);
    expect(svg.length).toBeLessThan(2500);
  }
});

it("gives each level its own set: 4, then 6, then 3 × 3", () => {
  for (let level = 1; level <= 10; level++) {
    const set = pictureLevels[level];
    expect(set.pictures).toHaveLength(level <= 3 ? 4 : level <= 6 ? 6 : 9);
    expect(new Set(set.pictures).size).toBe(set.pictures.length);
  }
});
