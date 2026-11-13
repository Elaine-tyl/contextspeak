import { expect, test, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { openWorld } from "./helpers";

const catalog = JSON.parse(
  readFileSync("games/unity/Assets/_Game/Art/WorldAssets/catalog.json", "utf8"),
) as {
  id: string;
  usage: string;
  zone: string;
  position: number[];
}[];
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
  return JSON.parse((await page.locator("#unity-status").textContent())!);
}

test("all_sources_have_live_scenery_and_streaming_survives_map_travel", async ({
  page,
}) => {
  test.setTimeout(240000);
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  await expect(page.locator("#unity-status")).toContainText(
    `"sourceCount":${catalog.length}`,
  );
  expect((await state(page)).sceneryCount).toBe(
    catalog.filter((item) => item.usage === "world").length,
  );
  const zones = [
    ...new Set(
      catalog.filter((item) => item.usage === "world").map((item) => item.zone),
    ),
  ];
  for (const zone of zones) {
    const item = catalog.find(
      (item) => item.usage === "world" && item.zone === zone,
    )!;
    await command(page, "visit", item.id);
    await expect
      .poll(async () => Math.abs((await state(page)).x - item.position[0]))
      .toBeLessThan(0.5);
    await expect
      .poll(async () => (await state(page)).loadedScenery)
      .toContain(item.id);
    await expect
      .poll(async () => (await state(page)).interaction)
      .toBe(item.id);
    await page.locator("#unity-canvas").press("e");
    await expect.poll(async () => (await state(page)).inspected).toBe(item.id);
    const snapshot = await state(page);
    expect(snapshot.visibleScenery).toBeLessThanOrEqual(20);
    expect(snapshot.loadingScenery).toBeLessThanOrEqual(2);
    expect(snapshot.sceneryTriangles).toBeLessThanOrEqual(400000);
  }
  await command(page, "visit", "counter");
  // Allow the existing damped camera to settle after the last map teleport before visual proof.
  await page.waitForTimeout(1000);
  await page.screenshot({ path: "docs/images/environment-fbx-cafe.jpg" });
  await command(page, "talk");
  await expect(page.locator("#unity-status")).toContainText('"speaker":"Rowan');
});

test("supplied_player_motion_plays_and_vault_is_opt_in", async ({ page }) => {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"');
  await command(page, "character", "Boy");
  await page.locator("#unity-canvas").focus();
  await page.keyboard.down("Shift");
  await page.keyboard.down("w");
  try {
    await expect
      .poll(async () => (await state(page)).animation)
      .toBe("Daydream-Run");
  } finally {
    await page.keyboard.up("w");
    await page.keyboard.up("Shift");
  }
  await expect
    .poll(async () => (await state(page)).animation)
    .toBe("Daydream-Idle");
  await command(page, "motion-preview", "Academy|Vault");
  expect((await state(page)).character).toBe("Boy");
  await expect
    .poll(async () => (await state(page)).animation)
    .toBe("Academy-Vault");
  await expect
    .poll(async () => (await state(page)).animationTime)
    .toBeGreaterThan(0.05);
  await expect
    .poll(async () => (await state(page)).animation, { timeout: 20000 })
    .toBe("Daydream-Idle");
});

for (const [look, model] of [
  ["Girl", "Academy"],
  ["Boy", "Daydream"],
]) {
  test(`${look}_walks_normally_and_only_runs_when_requested`, async ({
    page,
  }) => {
    await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
    await command(page, "character", look);
    await page.locator("#unity-canvas").focus();
    await page.keyboard.down("w");
    try {
      await expect
        .poll(async () => (await state(page)).animation)
        .toBe(`${model}-Walk`);
      await page.keyboard.down("Control");
      await page.waitForTimeout(500);
      expect((await state(page)).animation).toBe(`${model}-Walk`);
      await page.keyboard.up("Control");
      await page.keyboard.down("Shift");
      await expect
        .poll(async () => (await state(page)).animation)
        .toBe(`${model}-Run`);
      await page.keyboard.up("Shift");
      await expect
        .poll(async () => (await state(page)).animation)
        .toBe(`${model}-Walk`);
    } finally {
      await page.keyboard.up("Shift");
      await page.keyboard.up("Control");
      await page.keyboard.up("w");
    }
    await expect
      .poll(async () => (await state(page)).animation)
      .toBe(`${model}-Idle`);
    await page.keyboard.press("c");
    await page.waitForTimeout(400);
    expect((await state(page)).animation).toBe(`${model}-Idle`);
    await page.keyboard.down("w");
    await expect
      .poll(async () => (await state(page)).animation)
      .toBe(`${model}-Walk`);
    await page.keyboard.up("w");
    await expect
      .poll(async () => (await state(page)).animation)
      .toBe(`${model}-Idle`);
    await page.keyboard.press("c");

    const timing = await page.evaluate(async () => {
      const frames: number[] = [];
      let previous = performance.now();
      for (let frame = 0; frame < 30; frame++) {
        await new Promise(requestAnimationFrame);
        const now = performance.now();
        frames.push(now - previous);
        previous = now;
      }
      frames.sort((a, b) => a - b);
      return { medianMs: frames[15], p95Ms: frames[28] };
    });
    console.log(`${look} browser frame timings: ${JSON.stringify(timing)}`);
  });
}
