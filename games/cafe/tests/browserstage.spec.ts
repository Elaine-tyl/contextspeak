import { expect, test, type Page } from "@playwright/test";
import { voiceFixture } from "./voice-fixture";

const SAVE_KEY = "contextspeak-cafe-v1";
const savedJourney = JSON.stringify({
  version: 2,
  order: {
    version: 1,
    stage: "service",
    items: { tea: 2 },
    temperature: "iced",
    service: "",
    complete: false,
  },
  friend: { version: 1, phase: "counter", answers: [] },
});

async function choosePendingStage(page: Page, stage: number) {
  // Godot draws native controls on the canvas. The world test viewport is 1280×800.
  await page.locator("#canvas").click({ position: { x: 1040, y: 64 } });
  // Godot opens its popup on the render loop after the browser click completes.
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      }),
  );
  // A mouse-opened Godot PopupMenu starts without a focused row. The first
  // ArrowDown focuses row zero; Home does not establish that focus.
  for (let index = 0; index <= stage; index++) {
    await page.keyboard.press("ArrowDown");
  }
  await page.keyboard.press("Enter");
}

async function activatePendingStage(page: Page) {
  // The next focusable control after the picker is its explicit Select action.
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
}

async function selectStage(page: Page, stage: number) {
  await choosePendingStage(page, stage);
  await activatePendingStage(page);
}

test.beforeEach(async ({ page }) => {
  await page.route("**/session", (route) =>
    route.fulfill({ json: { token: "practice-stage-test" } }),
  );
  await page.route("**/speech", (route) =>
    route.fulfill({ contentType: "audio/wav", body: voiceFixture() }),
  );
});

test("explicit keyboard selection previews any turn and resumes the untouched saved journey", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await page.evaluate(({ key, value }) => localStorage.setItem(key, value), {
    key: SAVE_KEY,
    value: savedJourney,
  });
  await page.reload();
  await expect(status).toContainText("Task goals: 2/4");

  await choosePendingStage(page, 3);
  await expect(status).toContainText("Task goals: 2/4");
  await expect(status).not.toContainText("Test preview");
  await activatePendingStage(page);
  await expect(status).toContainText("Test preview · Weekend plans");
  await expect(status).toContainText("this weekend");
  await expect(status).toContainText("Your turn");
  await page.mouse.move(1279, 799);
  await page.screenshot({
    path: testInfo.outputPath("campus-stage-preview.png"),
  });

  await selectStage(page, 5);
  await expect(status).toContainText("Mission complete");
  await selectStage(page, 2);
  await expect(status).toContainText("What do you like to drink");
  await expect(status).toContainText("Friend conversation: 0/3");
  await selectStage(page, 4);
  await expect(status).toContainText("suggest a time");
  await selectStage(page, 1);
  await expect(status).toContainText("What can I get for you");
  await page.keyboard.type("Two coffees, please.");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("hot or iced");
  expect(
    await page.evaluate((key) => localStorage.getItem(key), SAVE_KEY),
  ).toBe(savedJourney);

  // The restart button remains in the existing mission HUD.
  await page.locator("#canvas").click({ position: { x: 135, y: 488 } });
  await expect(status).toContainText("Test preview · Counter ordering");
  await expect(status).toContainText("What can I get for you");
  await selectStage(page, 0);
  await expect(status).toContainText("Saved journey restored");
  await expect(status).toContainText("Task goals: 2/4");
  expect(
    await page.evaluate((key) => localStorage.getItem(key), SAVE_KEY),
  ).toBe(savedJourney);
  await page.reload();
  await expect(status).toContainText("Task goals: 2/4");
  await expect(status).not.toContainText("Test preview");
});

test("a late AI result cannot advance a newly selected practice turn", async ({
  page,
}) => {
  let release: (() => void) | undefined;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  let requested = false;
  await page.route("**/conversation", async (route) => {
    requested = true;
    await held;
    await route
      .fulfill({
        json: {
          accepted: true,
          acknowledgement: "Stale acknowledgement must be discarded.",
          feedback: "Stale correction must be discarded.",
        },
      })
      .catch(() => {}); // A stage change may already have aborted this request.
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await selectStage(page, 3);
  await expect(status).toContainText("this weekend");
  await page.keyboard.type("I would like to visit the park this weekend.");
  await page.keyboard.press("Enter");
  await expect.poll(() => requested).toBe(true);
  await selectStage(page, 2);
  release!();
  await expect(status).toContainText("Friend conversation: 0/3");
  await expect(status).toContainText("What do you like to drink");
  await expect(status).not.toContainText("Stale");
  expect(
    await page.evaluate((key) => localStorage.getItem(key), SAVE_KEY),
  ).toBe(null);
});
