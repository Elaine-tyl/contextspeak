import { expect, test } from "@playwright/test";
import { voiceFixture } from "./voice-fixture";
import { normalizeWave } from "../../../packages/world/protocol";

test("2D café keeps voiced recording, ordering, friend chat and its own save", async ({
  page,
}, testInfo) => {
  test.setTimeout(120000);
  const errors: string[] = [];
  page.on("console", (message) => {
    if (/SCRIPT ERROR|Parse Error/.test(message.text()))
      errors.push(message.text());
  });
  page.on("pageerror", (error) => errors.push(error.message));
  const original = JSON.stringify({
    version: 1,
    stage: "temperature",
    items: { coffee: 2 },
    temperature: "",
    service: "",
    complete: false,
  });
  await page.addInitScript((saved) => {
    if (!localStorage.getItem("contextspeak-cafe-v1"))
      localStorage.setItem("contextspeak-cafe-v1", saved);
  }, original);
  await page.route("**/speech", (route) =>
    route.fulfill({ contentType: "audio/wav", body: voiceFixture() }),
  );
  await page.route("**/interpret", (route) =>
    route.fulfill({
      status: 503,
      json: { error: "Use the existing offline parser." },
    }),
  );
  let recording: Buffer | null = null;
  await page.route("**/transcribe", (route) => {
    recording = route.request().postDataBuffer();
    return route.fulfill({ json: { text: "Two coffees, please." } });
  });
  await page.route("**/conversation", (route) =>
    route.fulfill({
      json: {
        accepted: true,
        acknowledgement: "That sounds fun.",
        feedback: "Clear and natural.",
      },
    }),
  );
  await page.goto("http://127.0.0.1:4320/?view=2d");
  const status = page.getByRole("status");
  await expect(status).toContainText("2D café");
  await expect(status).toContainText("Task goals: 0/4");
  await page.screenshot({ path: testInfo.outputPath("2d-room.png") });
  const canvas = page.locator("#canvas");
  await canvas.click();
  await canvas.press("Space");
  await expect(status).toContainText("Press E to talk");
  await canvas.press("e");
  await expect(status).toContainText("Your turn");
  await page.screenshot({ path: testInfo.outputPath("2d-counter.png") });
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Recording reply");
  await page.waitForTimeout(1200); // Capture real frames from Chromium's fake microphone.
  await page.keyboard.press("Enter");
  await expect(status).toContainText("hot or iced");
  expect(recording).not.toBeNull();
  expect(normalizeWave(recording!).length).toBeGreaterThan(3200);
  async function reply(text: string) {
    await expect(status).toContainText("Your turn");
    await page.keyboard.type(text);
    await page.keyboard.press("Enter");
  }
  await reply("Hot, please.");
  await expect(status).toContainText("take away");
  await reply("For here, please.");
  await expect(status).toContainText("Shall I place your order");
  await reply("Yes, please.");
  await expect(status).toContainText("What do you like to drink", {
    timeout: 25000,
  });
  await page.screenshot({ path: testInfo.outputPath("2d-friend.png") });
  for (const text of [
    "I like tea because it helps me relax.",
    "Let's go to the park.",
    "Would you like to meet at ten on Saturday?",
  ])
    await reply(text);
  await expect(status).toContainText("Mission complete");
  await page.reload();
  await expect(status).toContainText("Friend conversation: 3/3");
  expect(
    await page.evaluate(() => localStorage.getItem("contextspeak-cafe-v1")),
  ).toBe(original);
  expect(
    await page.evaluate(() => localStorage.getItem("contextspeak-cafe-2d-v1")),
  ).not.toBeNull();
  await page.setViewportSize({ width: 573, height: 793 });
  await expect(canvas).toHaveAttribute("width", "573");
  await page.screenshot({ path: testInfo.outputPath("2d-narrow.png") });
  await page.goto("http://127.0.0.1:4320/");
  await expect(status).toContainText("Task goals: 1/4");
  expect(errors).toEqual([]);
});
