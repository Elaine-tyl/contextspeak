import { expect, it } from "vitest";
import { nearSelection, clampPosition } from "../src/panel-position";
const viewport = { width: 1000, height: 700 };
const size = { width: 360, height: 220 };
it("positions_beside_the_selection_without_covering_it", () => {
  const result = nearSelection(
    { left: 200, right: 280, top: 200, bottom: 220 },
    size,
    viewport,
  );
  expect(result.x).toBe(292);
  expect(result.y).toBe(200);
});
it("uses_the_left_side_near_the_right_edge", () => {
  const result = nearSelection(
    { left: 850, right: 950, top: 200, bottom: 220 },
    size,
    viewport,
  );
  expect(result).toEqual({ x: 478, y: 200 });
});
it("clamps_dragging_and_keeps_the_header_reachable", () => {
  expect(clampPosition({ x: -500, y: 900 }, size, viewport)).toEqual({
    x: 8,
    y: 472,
  });
});
it("keeps_a_card_visible_on_a_small_viewport", () => {
  const result = nearSelection(
    { left: 100, right: 200, top: 650, bottom: 670 },
    { width: 300, height: 220 },
    { width: 320, height: 700 },
  );
  expect(result.x).toBeGreaterThanOrEqual(8);
  expect(result.x + 300).toBeLessThanOrEqual(312);
  expect(result.y + 220).toBeLessThanOrEqual(692);
});
