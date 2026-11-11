import { copyFile } from "node:fs/promises";
import { expect, test } from "@playwright/test";
import { normalizeWave } from "../../../packages/world/protocol";

test("local recordings drive voiced ordering, seated conversation and saved completion", async ({
  page,
}, testInfo) => {
  test.skip(
    !process.env.CONTEXTSPEAK_WORLD_VOICE_TEST,
    "Requires local models and public synthetic microphone fixtures.",
  );
  test.setTimeout(360000);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  const canvas = page.locator("#canvas");
  await canvas.click();
  await canvas.press("Space");
  await expect(status).toContainText("Press E to talk");
  const voice = page.waitForResponse(
    (response) => response.url().endsWith("/speech"),
    { timeout: 90000 },
  );
  await canvas.press("e");
  await expect(status).toContainText("What can I get for you");
  const npcAudio = await voice;
  expect(npcAudio.status()).toBe(200);
  expect((await npcAudio.body()).subarray(0, 4).toString()).toBe("RIFF");

  async function record(
    name: string,
    words: RegExp,
    next: string,
    duration = 3500,
  ) {
    await expect(status).toContainText("Your turn", { timeout: 60000 });
    await copyFile(
      `.local/world-test-${name}.wav`,
      ".local/world-test-audio.wav",
    );
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    await expect(status).toContainText("Recording reply");
    await page.waitForTimeout(duration); // Capture the synthetic phrase after its microphone setup silence.
    const posting = page.waitForRequest((request) =>
      request.url().endsWith("/transcribe"),
    );
    const recognized = page.waitForResponse((response) =>
      response.url().endsWith("/transcribe"),
    );
    await page.keyboard.press("Enter");
    const wave = normalizeWave((await posting).postDataBuffer()!);
    expect((wave.length - 44) / 32000).toBeGreaterThan(3.2);
    const response = await recognized;
    expect(response.status()).toBe(200);
    expect((await response.json()).text).toMatch(words);
    await expect(status).toContainText(next, { timeout: 45000 });
    await page.screenshot({ path: testInfo.outputPath(`${name}-checked.png`) });
  }
  await record("order", /two coffees/i, "hot or iced");
  await record("temperature", /hot/i, "take away");
  await record("service", /to go/i, "$8.00");
  await page.keyboard.type("Is that correct?");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("3/4");
  await record("confirm", /yes/i, "Order complete");
  await expect(status).toContainText("What do you like to drink", {
    timeout: 60000,
  });
  await expect(status).toContainText("Listen to Alex", { timeout: 60000 });
  await record("friend", /like tea/i, "this weekend", 5000);
  async function reply(text: string, next: string) {
    await expect(status).toContainText("Your turn", { timeout: 60000 });
    await page.keyboard.type(text);
    await page.keyboard.press("Enter");
    await expect(status).toContainText(next, { timeout: 60000 });
  }
  await reply("I was now playing Batman this weekend.", "suggest a time");
  await reply(
    "Would you like to meet me at the park at ten on Saturday?",
    "Mission complete",
  );
  await expect(status).toContainText("Conversation complete", {
    timeout: 60000,
  });
  await page.screenshot({
    path: testInfo.outputPath("local-friend-complete.png"),
  });
  await page.reload();
  await expect(status).toContainText("Friend conversation: 3/3");
  expect(errors).toEqual([]);
});
