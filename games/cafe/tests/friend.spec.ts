import { expect, test } from "@playwright/test";
import { voiceFixture } from "./voice-fixture";

test("voiced ordering leads to seated friend questions and saved conversation completion", async ({
  page,
}, testInfo) => {
  // Full WebGL journey plus two rendered captures on software-GPU CI.
  test.setTimeout(90000);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      /SCRIPT ERROR|SHADER ERROR|Shader compilation|Parse Error/.test(
        message.text(),
      )
    )
      errors.push(message.text());
  });
  await page.route("**/speech", (route) =>
    route.fulfill({ contentType: "audio/wav", body: voiceFixture() }),
  );
  const turns: number[] = [];
  await page.route("**/conversation", (route) => {
    const input = route.request().postDataJSON();
    turns.push(input.turn);
    return route.fulfill({
      json: {
        accepted: input.text !== "uh",
        acknowledgement:
          input.text === "uh"
            ? "Could you say a little more?"
            : "That sounds lovely.",
        feedback:
          input.text === "uh"
            ? "Name a drink and give a reason, for example: I like tea because it helps me relax."
            : "Give a reason and speak in a complete sentence.",
      },
    });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  const canvas = page.locator("#canvas");
  await expect(status).toContainText("Task goals: 0/4");
  await canvas.click();
  await canvas.press("Space");
  await expect(status).toContainText("Press E to talk");
  await canvas.press("e");
  await expect(status).toContainText("Listen to Maya");
  await expect(status).toContainText("What can I get for you");
  async function reply(text: string) {
    await page.keyboard.type(text);
    await page.keyboard.press("Enter");
  }
  await reply("Two hot coffees for here, please.");
  await expect(status).toContainText("Shall I place your order");
  await reply("Yes, please.");
  await expect(status).toContainText("What do you like to drink", {
    timeout: 20000,
  });
  await expect(status).toContainText("Listen to Alex");
  await page.mouse.move(1279, 799);
  await page.screenshot({ path: testInfo.outputPath("seated-with-alex.png") });
  await expect(status).toContainText("Your turn");
  await expect(status).not.toContainText("Order confirmed");
  await reply("uh");
  await expect(status).toContainText("Friend conversation: 0/3");
  await expect(status).not.toContainText("What do you like to drink");
  await expect(status).toContainText("Name a drink and give a reason");
  await reply("I like tea because it is relaxing.");
  await expect(status).toContainText("this weekend");
  await expect(status).toContainText("Your turn");
  await expect(status).toContainText(
    "Give a reason and speak in a complete sentence.",
  );
  await reply("I would like to go to the park.");
  await expect(status).toContainText("suggest a time");
  await reply("Would you like to meet at ten on Saturday?");
  await expect(status).toContainText("Mission complete");
  await expect(status).toContainText("Conversation complete");
  expect(turns).toEqual([0, 0, 1, 2]);
  await page.mouse.move(1279, 799);
  await page.screenshot({ path: testInfo.outputPath("friend-completed.png") });
  await page.reload();
  await expect(status).toContainText("Friend conversation: 3/3");
  await expect(status).toContainText("Mission complete");
  expect(errors).toEqual([]);
});
