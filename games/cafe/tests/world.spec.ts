import { expect, test } from "@playwright/test";
import { normalizeWave } from "../../../packages/world/protocol";

test("walk to the NPC, correct the budget, finish a conversation and restore guest progress", async ({
  page,
}, testInfo) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      /SCRIPT ERROR|Parse Error/.test(message.text())
    )
      errors.push(message.text());
  });
  // This regression tests gameplay without depending on installed speech models.
  await page.route("**/speech", (route) =>
    route.fulfill({
      status: 503,
      json: { error: "Voice fixture unavailable" },
    }),
  );
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  const canvas = page.locator("#canvas");
  await canvas.click();
  await canvas.press("Space");
  await expect(status).toContainText("Press E to talk");
  await canvas.press("e");
  await expect(status).toContainText("What can I get for you");
  async function reply(text: string) {
    await page.keyboard.type(text);
    await page.keyboard.press("Enter");
  }
  await reply("Two lattes, please.");
  await expect(status).toContainText("over your twelve-dollar budget");
  await reply("Two coffees, please.");
  await expect(status).toContainText("hot or iced");
  await reply("Iced, please.");
  await expect(status).toContainText("take away");
  await reply("To go, please.");
  await expect(status).toContainText("$8.00");
  await reply("Yes, please.");
  await expect(status).toContainText("Order complete");
  await page.screenshot({ path: testInfo.outputPath("cafe-completed.png") });
  await page.reload();
  await expect(status).toContainText("2 drinks earned");
  expect(errors).toEqual([]);
});

test("stopping a synthetic microphone reply checks it automatically", async ({
  page,
}) => {
  let recording: Buffer | null = null;
  await page.route("**/speech", (route) =>
    route.fulfill({
      status: 503,
      json: { error: "Voice fixture unavailable" },
    }),
  );
  await page.route("**/transcribe", async (route) => {
    recording = route.request().postDataBuffer();
    await route.fulfill({ json: { text: "Two coffees, please." } });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  const canvas = page.locator("#canvas");
  await canvas.click();
  await canvas.press("Space");
  await expect(status).toContainText("Press E to talk");
  await canvas.press("e");
  await expect(status).toContainText("What can I get for you");
  // Focus moves from the reply field to the recording control.
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Recording reply");
  await page.waitForTimeout(2000); // Capture actual frames from Chromium's fake microphone.
  await page.keyboard.press("Enter");
  await expect(status).toContainText("hot or iced");
  expect(recording).not.toBeNull();
  expect(normalizeWave(recording!).length).toBeGreaterThan(3200);
});

test("a failed recording check preserves the task and the typed fallback", async ({
  page,
}) => {
  await page.route("**/speech", (route) =>
    route.fulfill({
      status: 503,
      json: { error: "Voice fixture unavailable" },
    }),
  );
  await page.route("**/transcribe", (route) =>
    route.fulfill({
      status: 503,
      json: { error: "Recognition busy. Try again or type your reply." },
    }),
  );
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  const canvas = page.locator("#canvas");
  await canvas.click();
  await canvas.press("Space");
  await expect(status).toContainText("Press E to talk");
  await canvas.press("e");
  await expect(status).toContainText("What can I get for you");
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Recording reply");
  await page.waitForTimeout(500);
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Recognition busy");
  await page.keyboard.type("Two coffees, please.");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("hot or iced");
});
