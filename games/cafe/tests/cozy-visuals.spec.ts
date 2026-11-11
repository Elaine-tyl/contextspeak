import { expect, test, type Page } from "@playwright/test";
import { writeFile } from "node:fs/promises";
import { voiceFixture } from "./voice-fixture";

async function selectStage(page: Page, stage: number) {
  await page.locator("#canvas").click({ position: { x: 1040, y: 64 } });
  for (let index = 0; index <= stage; index++)
    await page.keyboard.press("ArrowDown");
  await page.keyboard.press("Enter");
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
}

test("café presentation keeps stage previews, Escape and NPC controls working", async ({
  page,
}, testInfo) => {
  test.setTimeout(90000);
  const errors: string[] = [];
  const spoken: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (
      /SCRIPT ERROR|SHADER ERROR|Shader compilation|Parse Error/.test(
        message.text(),
      )
    )
      errors.push(message.text());
  });
  await page.route("**/session", (route) =>
    route.fulfill({ json: { token: "visual-regression" } }),
  );
  await page.route("**/speech", (route) => {
    spoken.push(route.request().postDataJSON().text);
    return route.fulfill({ contentType: "audio/wav", body: voiceFixture() });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await page.mouse.move(1279, 799);
  await page.screenshot({ path: testInfo.outputPath("cafe-overview.png") });

  // This measures browser presentation cadence, not a guarantee for other GPUs.
  const cadence = await page.evaluate(
    () =>
      new Promise<{ frames: number; fps: number }>((resolve) => {
        let first = 0;
        let frames = 0;
        function sample(now: number) {
          first ||= now;
          frames++;
          if (now - first >= 2000)
            resolve({ frames, fps: ((frames - 1) * 1000) / (now - first) });
          else requestAnimationFrame(sample);
        }
        requestAnimationFrame(sample);
      }),
  );
  await writeFile(
    testInfo.outputPath("presentation-cadence.json"),
    JSON.stringify(cadence),
  );
  expect(cadence.frames).toBeGreaterThan(1);

  await selectStage(page, 1);
  await expect(status).toContainText("Your turn");
  await expect(status).toContainText("What can I get for you");
  await page.screenshot({ path: testInfo.outputPath("cafe-counter.png") });
  const counterLines = spoken.length;
  // Escape must work from the focused reply field, not only from the close icon.
  await page.keyboard.press("Escape");
  await page.keyboard.press("Space");
  await page.keyboard.press("e");
  await expect.poll(() => spoken.length).toBeGreaterThan(counterLines);

  await selectStage(page, 2);
  await expect(status).toContainText("What do you like to drink");
  await expect(status).toContainText("Your turn");
  await page.screenshot({
    path: testInfo.outputPath("cafe-friend-dialogue.png"),
  });
  await selectStage(page, 0);
  await expect(status).toContainText("Saved journey restored");
  await expect(status).toContainText("Task goals: 0/4");
  expect(
    await page.evaluate(() => localStorage.getItem("contextspeak-cafe-v1")),
  ).toBeNull();
  expect(errors).toEqual([]);
});

test.describe("narrow high-density game view", () => {
  test.use({ viewport: { width: 573, height: 793 }, deviceScaleFactor: 2 });

  test("readable controls still open scenes and start nearby dialogue", async ({
    page,
  }, testInfo) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await page.route("**/session", (route) =>
      route.fulfill({ json: { token: "responsive-regression" } }),
    );
    await page.route("**/speech", (route) =>
      route.fulfill({ contentType: "audio/wav", body: voiceFixture() }),
    );
    await page.goto("http://127.0.0.1:4320");
    const status = page.getByRole("status");
    await expect(status).toContainText("Task goals: 0/4");
    await page.screenshot({
      path: testInfo.outputPath("narrow-exploration.png"),
    });
    // The narrow header wraps the existing stage controls onto its second row.
    await page.locator("#canvas").click({ position: { x: 240, y: 45 } });
    await expect(status).toContainText("Choose your scene");
    await page.screenshot({ path: testInfo.outputPath("narrow-scenes.png") });
    await page.keyboard.press("Escape");
    await page.locator("#canvas").click({ position: { x: 500, y: 700 } });
    await page.keyboard.press("Space");
    await expect(status).toContainText("Press E to talk");
    await page.keyboard.press("e");
    await expect(status).toContainText("What can I get for you");
    await expect(status).toContainText("Your turn");
    await page.screenshot({ path: testInfo.outputPath("narrow-counter.png") });
    expect(errors).toEqual([]);
  });
});
