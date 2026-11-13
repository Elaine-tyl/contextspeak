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
async function state(page: Page) {
  const snapshot = await page.locator("#unity-status").textContent();
  return snapshot ? JSON.parse(snapshot) : {};
}

test("imported buildings support entrance, cutaway, NPC conversation and exit", async ({
  page,
}) => {
  test.setTimeout(240000);
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  await expect.poll(async () => (await state(page)).buildingCount).toBe(34);
  for (const scene of [
    "home",
    "market",
    "school",
    "restaurant",
    "japan",
    "mall",
    "counter",
  ]) {
    await command(page, "visit", `building:${scene}`);
    await expect.poll(async () => (await state(page)).insideBuilding).toBe("");
    await expect
      .poll(async () => (await state(page)).interaction)
      .toBe(`building:${scene}`);
    expect((await state(page)).exteriorBuildings).toBe(7);
    if (scene === "home") {
      await page.waitForTimeout(1500);
      await page.screenshot({ path: "docs/images/building-exterior.jpg" });
    }
    await page.locator("#unity-canvas").press("e");
    await expect
      .poll(async () => (await state(page)).insideBuilding)
      .toBe(scene);
    expect((await state(page)).exteriorBuildings).toBe(6);
    if (scene === "home") {
      await page.waitForTimeout(1500);
      await page.screenshot({ path: "docs/images/building-interior.jpg" });
    }
    await command(page, "talk");
    await expect.poll(async () => (await state(page)).dialogue).toBe(true);
    if (scene !== "counter") expect((await state(page)).lesson).toBe(scene);
    await command(page, "visit", `exit:${scene}`);
    await expect
      .poll(async () => (await state(page)).interaction)
      .toBe(`exit:${scene}`);
    await page.locator("#unity-canvas").press("e");
    await expect.poll(async () => (await state(page)).insideBuilding).toBe("");
    expect((await state(page)).dialogue).toBe(false);
  }
  await command(page, "visit", "home");
  await expect
    .poll(async () => (await state(page)).insideBuilding)
    .toBe("home");
  await command(page, "save");
  await expect.poll(async () => (await state(page)).saveState).toBe("saved");
  await page.reload();
  await expect
    .poll(async () => (await state(page)).insideBuilding, { timeout: 120000 })
    .toBe("home");
  await expect(
    page.getByRole("form", { name: "Create your character" }),
  ).toHaveCount(0);
});
