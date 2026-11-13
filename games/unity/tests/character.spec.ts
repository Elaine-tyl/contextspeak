import { expect, test, type Page } from "@playwright/test";
import { openWorld } from "./helpers";

async function command(page: Page, action: string, text = "") {
  await page.evaluate(
    ([action, text]) => {
      const bridge = (window as any).ContextSpeakUnity;
      bridge.instance.SendMessage(
        bridge.owner,
        "OnBrowserCommand",
        JSON.stringify({ action, text }),
      );
    },
    [action, text],
  );
}

test("an_outgoing_district_snapshot_cannot_hide_character_creation", async ({
  page,
}) => {
  await page.goto("http://127.0.0.1:4320/unity/?world=slice");
  const form = page.getByRole("form", { name: "Create your character" });
  await expect(form).toBeVisible({ timeout: 120000 });
  // Additive scenes overlap briefly: the old world can publish one last frame.
  await page.evaluate(() =>
    (window as any).ContextSpeakUnity.state('{"mode":"slice"}'),
  );
  await expect(form).toBeVisible();
  await page.getByRole("button", { name: "Male", exact: true }).click();
  await expect(page.getByLabel("Character name", { exact: true })).toHaveValue(
    "Lucas",
  );
});

test("first_launch_creates_a_real_character_before_entering_the_world", async ({
  page,
}) => {
  await page.goto("http://127.0.0.1:4320/unity/?world=slice");
  const form = page.getByRole("form", { name: "Create your character" });
  await expect(form).toBeVisible({ timeout: 20000 });
  await page.getByRole("button", { name: "Male", exact: true }).click();
  await expect(page.getByLabel("Character name", { exact: true })).toHaveValue(
    "Lucas",
  );
  const girlColor = await page
    .getByRole("button", { name: "Female", exact: true })
    .evaluate((button) => getComputedStyle(button).backgroundColor);
  const boyColor = await page
    .getByRole("button", { name: "Male", exact: true })
    .evaluate((button) => getComputedStyle(button).backgroundColor);
  expect(girlColor).not.toBe(boyColor);
  await page.getByRole("button", { name: "Turn preview right" }).click();
  await expect(page.locator("#unity-status")).toContainText('"rotation":45');
  await page.locator("#character-height").fill("1.03");
  await page.locator("#character-build").fill("0.98");
  await page.getByLabel("Character name", { exact: true }).fill("Elaine");
  await page.getByLabel("Starting practice band").selectOption("7");
  await page
    .getByRole("button", { name: "Save & enter world", exact: true })
    .click();
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"', {
    timeout: 120000,
  });
  await expect(page.locator("#unity-status")).toContainText(
    '"character":"Boy"',
  );
  await expect(page.locator("#unity-status")).toContainText(
    '"characterName":"Elaine"',
  );
  await expect(page.locator("#unity-status")).toContainText('"band":"Band7"');
});

test("wardrobe_cancel_and_save_preserve_the_world_and_current_difficulty", async ({
  page,
}, info) => {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  const state = page.locator("#unity-status");
  await command(page, "band", "Band6");
  await command(page, "visit", "wardrobe");
  const before = JSON.parse((await state.textContent())!);
  await page.locator("#unity-canvas").press("e");
  const form = page.getByRole("form", { name: "Create your character" });
  await expect(form).toBeVisible();
  await page.getByLabel("Character name", { exact: true }).fill("Unsaved");
  await page.getByRole("button", { name: "Male", exact: true }).click();
  await page.getByRole("button", { name: "Return without changes" }).click();
  await expect(state).toContainText('"mode":"slice"');
  await expect(state).toContainText('"characterName":"Emma"');
  await expect(state).toContainText('"character":"Girl"');
  await expect(state).toContainText('"band":"Band6"');
  await command(page, "wardrobe");
  await expect(form).toBeVisible();
  await page.getByLabel("Character name", { exact: true }).fill("Jamie");
  await page.getByRole("button", { name: "Male", exact: true }).click();
  await page.getByLabel("Starting practice band").selectOption("9");
  await page
    .getByRole("button", { name: "Save & enter world", exact: true })
    .click();
  await expect(state).toContainText('"mode":"slice"');
  await expect(state).toContainText('"characterName":"Jamie"');
  await expect(state).toContainText('"band":"Band6"');
  await page.reload();
  await expect(state).toContainText('"mode":"slice"', { timeout: 120000 });
  await expect(form).toBeHidden();
  await expect(state).toContainText('"characterName":"Jamie"');
  const after = JSON.parse((await state.textContent())!);
  expect(after.character).toBe("Boy");
  expect(after.cash).toBe(before.cash);
  expect(after.card).toBe(before.card);
  expect(after.receipts).toBe(before.receipts);
  expect(Math.abs(after.x - before.x)).toBeLessThan(0.15);
  expect(Math.abs(after.z - before.z)).toBeLessThan(0.15);
  await command(page, "visit", "fitting-room");
  await page.locator("#unity-canvas").press("e");
  await expect(form).toBeVisible();
  await page.screenshot({ path: info.outputPath("character-creation.png") });
});

test("character_creation_fits_a_phone_viewport", async ({ page }, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("http://127.0.0.1:4320/unity/?world=slice");
  const form = page.getByRole("form", { name: "Create your character" });
  await expect(form).toBeVisible({ timeout: 120000 });
  await page.getByRole("button", { name: "Female", exact: true }).click();
  await page.getByLabel("Character name", { exact: true }).fill("Maya");
  await page
    .getByRole("button", { name: "Save & enter world", exact: true })
    .scrollIntoViewIfNeeded();
  await page.screenshot({ path: info.outputPath("character-mobile.png") });
  await page
    .getByRole("button", { name: "Save & enter world", exact: true })
    .click();
  await expect(page.locator("#unity-status")).toContainText(
    '"characterName":"Maya"',
  );
});
