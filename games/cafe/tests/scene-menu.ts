import { expect, type Page } from "@playwright/test";

export async function enterScene(
  page: Page,
  scene: number,
  role = 0,
  difficulty = 0,
) {
  // Godot's controls are drawn inside the 1280×800 regression canvas.
  const canvas = page.locator("#canvas");
  await canvas.click({ position: { x: 298, y: 54 } });
  await expect(page.getByRole("status")).toContainText("practice difficulty");
  await choose(page, 235, scene);
  if (scene !== 0) await choose(page, 323, role);
  await choose(page, 411, difficulty);
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
}

async function choose(page: Page, y: number, index: number) {
  await page.locator("#canvas").click({ position: { x: 640, y } });
  // A mouse-opened Godot popup starts without a focused row.
  for (let row = 0; row <= index; row++) await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
}
