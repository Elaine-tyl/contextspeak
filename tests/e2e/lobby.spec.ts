import { expect, test } from "@playwright/test";
import { gameModes } from "@contextspeak/contracts";

test.beforeEach(async ({ page }) => {
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "lobby-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: [],
        progress: {
          listening: { level: 8, passed: [] },
          speaking: { level: 3, passed: [] },
        },
        modeProgress: { dictation: { level: 8 }, "gap-fill": { level: 4 } },
      },
    }),
  );
});

test("3D lobby renders a real world and preserves reached levels and game selection", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:3000/games");
  await expect(
    page.getByRole("img", { name: "3D game world", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".game-lobby")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  const dictation = page.getByRole("button", {
    name: "Dictation",
    exact: true,
  });
  await expect(dictation).toContainText("Level 8 of 10");
  await expect(
    page.getByRole("button", { name: "Missing words", exact: true }),
  ).toContainText("Level 4 of 10");
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-3d-lobby.png"),
    fullPage: true,
  });
  await page.locator(".game-lobby").screenshot({
    path: testInfo.outputPath("contextspeak-3d-game-interface.png"),
  });
  await page.getByRole("button", { name: "3D effects", exact: true }).click();
  const canvas = page.getByRole("img", { name: "3D game world", exact: true });
  const still = await canvas.screenshot({
    path: testInfo.outputPath("lobby-still.png"),
  });
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        let frames = 0;
        function tick() {
          if (++frames >= 8) resolve();
          else requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
      }),
  );
  expect((await canvas.screenshot()).equals(still)).toBe(true);
  const view = (await canvas.boundingBox())!;
  await page.mouse.move(view.x + view.width * 0.95, view.y + view.height * 0.9);
  await page.mouse.down();
  await page.mouse.move(view.x + view.width * 0.8, view.y + view.height * 0.8, {
    steps: 8,
  });
  await page.mouse.up();
  await expect(page.locator(".game-lobby")).toBeVisible();
  expect((await canvas.screenshot()).equals(still)).toBe(false);
  await page.getByRole("button", { name: "Reset view", exact: true }).click();
  // The canvas capture includes its HTML labels; let their hover fade finish too.
  await page
    .locator(".lobby-marker")
    .evaluateAll((markers) =>
      Promise.all(
        markers
          .flatMap((marker) => marker.getAnimations())
          .map((animation) => animation.finished.catch(() => {})),
      ),
    );
  const resetImage = await canvas.screenshot({
    path: testInfo.outputPath("lobby-reset.png"),
  });
  expect(resetImage.equals(still)).toBe(true);
  await dictation.focus();
  await page.keyboard.press("Enter");
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await expect(page.locator(".game-lobby")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  await dictation.scrollIntoViewIfNeeded();
  const island = (await dictation.boundingBox())!;
  await page.mouse.click(island.x + island.width / 2, island.y - 40, {
    button: "right",
  });
  await expect(page.locator(".game-lobby")).toBeVisible();
  await page.keyboard.press("Escape");
  await page.mouse.click(island.x + island.width / 2, island.y - 40);
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await page
    .getByRole("button", { name: "Speaking games", exact: true })
    .click();
  await expect(page.locator(".game-lobby")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  await expect
    .poll(() =>
      page
        .getByRole("img", { name: "3D game world", exact: true })
        .evaluate((element) =>
          (element as HTMLCanvasElement).getContext("webgl2")?.isContextLost(),
        ),
    )
    .toBe(false);
  await page.getByRole("button", { name: "List view", exact: true }).click();
  await page.getByRole("button", { name: "3D view", exact: true }).click();
  await expect(page.locator(".game-lobby")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  await expect
    .poll(() =>
      page
        .getByRole("img", { name: "3D game world", exact: true })
        .evaluate((element) =>
          (element as HTMLCanvasElement).getContext("webgl2")?.isContextLost(),
        ),
    )
    .toBe(false);
  await expect(
    page.getByRole("button", { name: "Speaking questions", exact: true }),
  ).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page
    .getByRole("button", { name: "Speaking questions", exact: true })
    .scrollIntoViewIfNeeded();
  await expect(
    page.getByRole("button", { name: "Speaking questions", exact: true }),
  ).toBeInViewport();
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-3d-lobby-mobile.png"),
    fullPage: true,
  });
});

test("a cancelled pointer gesture does not open a game", async ({ page }) => {
  await page.goto("http://127.0.0.1:3000/games");
  await expect(page.locator(".game-lobby")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  const marker = page.getByRole("button", { name: "Dictation", exact: true });
  await marker.scrollIntoViewIfNeeded();
  const bounds = (await marker.boundingBox())!;
  await page.mouse.move(bounds.x + bounds.width / 2, bounds.y - 40);
  await page.mouse.down();
  await page
    .getByRole("img", { name: "3D game world", exact: true })
    .dispatchEvent("pointercancel", {
      pointerId: 1,
      isPrimary: true,
      pointerType: "mouse",
    });
  await page.mouse.up();
  await expect(page.locator(".game-lobby")).toBeVisible();
});

test.describe("touch lobby", () => {
  test.use({
    viewport: { width: 390, height: 844 },
    isMobile: true,
    hasTouch: true,
  });
  test("a phone can tap an island label without losing any game modes", async ({
    page,
  }) => {
    await page.goto("http://127.0.0.1:3000/games");
    await expect(page.locator(".game-lobby")).toHaveAttribute(
      "data-renderer",
      "webgl",
    );
    await expect(page.locator(".lobby-marker")).toHaveCount(
      gameModes.filter((mode) => mode.track === "listening").length,
    );
    const canvas = page.getByRole("img", {
      name: "3D game world",
      exact: true,
    });
    expect(
      await canvas.evaluate((element) => getComputedStyle(element).touchAction),
    ).toBe("pan-y");
    const marker = page.getByRole("button", {
      name: "Missing words",
      exact: true,
    });
    await marker.tap();
    await expect(
      page.getByRole("button", { name: "Start level", exact: true }),
    ).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  });
});

test("games remain selectable when WebGL is unavailable", async ({ page }) => {
  await page.addInitScript(() => {
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (
      this: HTMLCanvasElement,
      kind: string,
      ...args: unknown[]
    ) {
      if (kind.startsWith("webgl")) return null;
      return Reflect.apply(getContext, this, [kind, ...args]);
    } as typeof getContext;
  });
  await page.goto("http://127.0.0.1:3000/games");
  await expect(page.locator(".game-lobby")).toHaveAttribute(
    "data-renderer",
    "fallback",
  );
  await page
    .getByRole("button", { name: "Missing words", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeVisible();
});
