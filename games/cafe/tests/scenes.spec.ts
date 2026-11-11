import { expect, test, type Page } from "@playwright/test";
import scenes from "../data/roleplay.json" with { type: "json" };
import { voiceFixture } from "./voice-fixture";
import layouts from "../data/scene_routes.json" with { type: "json" };
import { normalizeWave } from "../../../packages/world/protocol";
import { enterScene } from "./scene-menu";

const SAVE = "contextspeak-scenes-v1";
const shopping = scenes[0].roles[0];
const shoppingSave = `${scenes[0].id}/${shopping.id}`;

async function reply(page: Page, text: string) {
  await page.keyboard.type(text);
  await page.keyboard.press("Enter");
}

async function walkAndTalk(
  page: Page,
  turn: { prompt: string; npc: { name: string } },
  correction = false,
) {
  const status = page.getByRole("status");
  // The helper uses real movement along collision-checked corridors.
  await page.locator("#canvas").click({ position: { x: 640, y: 105 } });
  await page.keyboard.press("Space");
  await expect(status).toContainText(`Press E to talk to ${turn.npc.name}`, {
    timeout: 25000,
  });
  await page.keyboard.press("e");
  await expect(status).toContainText(
    correction ? "Please repeat:" : turn.prompt,
  );
  await expect(status).toContainText("Your turn");
}

async function exploredAfterGoal(page: Page, count: number) {
  await expect(page.getByRole("status")).toContainText(
    `Conversation goals: ${count}/3`,
  );
  await expect(page.getByRole("status")).toContainText("Explore ·");
}

test.beforeEach(async ({ page }) => {
  await page.route("**/session", (route) =>
    route.fulfill({ json: { token: "scene-test" } }),
  );
  await page.route("**/speech", (route) =>
    route.fulfill({ contentType: "audio/wav", body: voiceFixture() }),
  );
});

test("walk freely between areas and greet a different NPC without skipping a goal", async ({
  page,
}, testInfo) => {
  const questions: string[] = [];
  await page.route("**/roleplay", (route) => {
    questions.push(route.request().postDataJSON().text);
    return route.fulfill({ status: 500 });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 1);
  await expect(status).toContainText("Explore · Entrance");
  await expect(status).not.toContainText("Your turn");
  await page.locator("#canvas").click({ position: { x: 640, y: 105 } });
  await page.keyboard.press("e");
  await expect(status).toContainText("Move closer to an NPC");

  // Walk through the same open corridors as a player, without the helper.
  for (const [key, duration] of [
    ["d", 1050],
    ["w", 475],
    ["d", 1580],
  ] as const) {
    await page.keyboard.down(key);
    await page.waitForTimeout(duration);
    await page.keyboard.up(key);
  }
  const zone = layouts.shopping.zones[2];
  const npc = shopping.turns[2].npc;
  await expect(status).toContainText(zone.label);
  await expect(status).toContainText(`Press E to talk to ${npc.name}`);
  await page.mouse.move(1279, 799);
  await page.screenshot({
    path: testInfo.outputPath("shopping-free-exploration.png"),
  });
  await page.keyboard.press("e");
  await expect(status).toContainText("Hey, good to see you!");
  await expect(status).toContainText("Find " + shopping.turns[0].npc.name);
  await expect(status).not.toContainText("Your turn");
  await expect(status).toContainText("Press Esc or × to keep exploring");
  await expect(status).toContainText("Conversation goals: 0/3");
  expect(questions).toEqual([]);
  await page.screenshot({
    path: testInfo.outputPath("shopping-npc-greeting.png"),
  });
  await page.keyboard.press("Escape");
  await walkAndTalk(page, shopping.turns[0]);
  await expect(status).toContainText("Conversation goals: 0/3");
});

test("correction needs a recorded readback, then completes and restores only its selected role", async ({
  page,
}, testInfo) => {
  test.setTimeout(150000); // Physical approaches to three separate NPCs.
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (/SCRIPT ERROR|SHADER ERROR|Parse Error/.test(message.text()))
      errors.push(message.text());
  });
  const calls: number[] = [];
  let captured: Buffer | null = null;
  await page.route("**/roleplay", (route) => {
    const input = route.request().postDataJSON();
    calls.push(input.turn);
    return route.fulfill({
      json: {
        accepted: input.text !== "banana",
        correction: input.text === "banana" ? shopping.turns[0].sample : "",
        feedback:
          input.text === "banana" ? shopping.turns[0].goal : "Clear answer.",
        acknowledgement:
          input.text === "banana" ? "Let's practise that line." : "Thank you.",
      },
    });
  });
  await page.route("**/transcribe", (route) => {
    captured = route.request().postDataBuffer();
    return route.fulfill({
      json: { text: shopping.turns[0].sample },
    });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 1);
  await walkAndTalk(page, shopping.turns[0]);
  await expect(status).toContainText(shopping.turns[0].prompt);
  await expect(status).toContainText("Your turn");
  await reply(page, "banana");
  await expect(status).toContainText("Please repeat:");
  await expect(status).toContainText("Conversation goals: 0/3");
  await reply(page, "I am looking");
  await expect(status).toContainText("Not quite the model sentence");
  expect(calls).toEqual([0]); // Readback uses the stored correction, not another model call.
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Recording reply");
  await page.waitForTimeout(1200); // Capture real frames from the fake browser microphone.
  await page.keyboard.press("Enter");
  await exploredAfterGoal(page, 1);
  expect(normalizeWave(captured!).length).toBeGreaterThan(3200);
  await expect(status).toContainText("1 assisted");
  await walkAndTalk(page, shopping.turns[1]);
  await reply(page, shopping.turns[1].sample);
  await exploredAfterGoal(page, 2);
  await walkAndTalk(page, shopping.turns[2]);
  await reply(page, shopping.turns[2].sample);
  await exploredAfterGoal(page, 3);
  await expect(status).toContainText("Mission complete");
  await expect(status).toContainText("2 independent · 1 assisted");
  expect(calls).toEqual([0, 1, 2]);
  const saved = await page.evaluate((key) => localStorage.getItem(key), SAVE);
  expect(JSON.parse(saved!).sessions[shoppingSave].answers).toHaveLength(3);
  await page.mouse.move(1279, 799);
  await page.screenshot({ path: testInfo.outputPath("shopping-complete.png") });
  await page.reload();
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 1);
  await expect(status).toContainText("Conversation goals: 3/3");
  await enterScene(page, 1, 1);
  await expect(status).toContainText(
    "Find " + scenes[0].roles[1].turns[0].npc.name,
  );
  await expect(status).toContainText("Conversation goals: 0/3");
  expect(errors).toEqual([]);
});

test("each role starts in exploration and nearby NPCs voice their own questions", async ({
  page,
}, testInfo) => {
  test.setTimeout(180000); // Five physical approaches and ten map/role loads.
  const spoken: string[] = [];
  await page.route("**/speech", (route) => {
    spoken.push(route.request().postDataJSON().text);
    return route.fulfill({ contentType: "audio/wav", body: voiceFixture() });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  for (let scene = 0; scene < scenes.length; scene++) {
    for (let role = 0; role < 2; role++) {
      await enterScene(page, scene + 1, role);
      const prompt = scenes[scene].roles[role].turns[0].prompt;
      await expect(status).toContainText("Explore ·");
      await expect(status).toContainText(
        "Find " + scenes[scene].roles[role].turns[0].npc.name,
      );
      await expect(status).not.toContainText("Your turn");
      if (role === 0) {
        await walkAndTalk(page, scenes[scene].roles[role].turns[0]);
        expect(spoken).toContain(prompt);
        await page.mouse.move(1279, 799);
        await page.screenshot({
          path: testInfo.outputPath(`${scenes[scene].id}.png`),
        });
      }
    }
  }
  expect(
    await page.evaluate(() => localStorage.getItem("contextspeak-cafe-v1")),
  ).toBe(null);
  await enterScene(page, 0);
  await expect(status).toContainText("Task goals: 0/4");
});

test("switching roles discards a late assessment", async ({ page }) => {
  let release: (() => void) | undefined;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  let requested = false;
  await page.route("**/roleplay", async (route) => {
    requested = true;
    await held;
    await route
      .fulfill({
        json: {
          accepted: true,
          correction: "",
          feedback: "Stale feedback.",
          acknowledgement: "Stale result.",
        },
      })
      .catch(() => {});
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 2);
  await walkAndTalk(page, scenes[1].roles[0].turns[0]);
  await expect(status).toContainText(scenes[1].roles[0].turns[0].prompt);
  await reply(page, "I will water the carrots.");
  await expect.poll(() => requested).toBe(true);
  await enterScene(page, 3, 1);
  release!();
  await expect(status).toContainText(
    "Find " + scenes[2].roles[1].turns[0].npc.name,
  );
  await expect(status).toContainText("Explore ·");
  await expect(status).toContainText("Conversation goals: 0/3");
  await expect(status).not.toContainText("Stale");
  expect(await page.evaluate((key) => localStorage.getItem(key), SAVE)).toBe(
    null,
  );
});

test("a saved correction survives reload and a busy evaluator keeps the next answer editable", async ({
  page,
}) => {
  test.setTimeout(150000);
  let calls = 0;
  await page.route("**/roleplay", (route) => {
    calls++;
    return calls === 1
      ? route.fulfill({
          json: {
            accepted: false,
            correction: shopping.turns[0].sample,
            feedback: shopping.turns[0].goal,
            acknowledgement: "Let's try that line.",
          },
        })
      : route.fulfill({
          status: 503,
          json: { error: "Local AI is busy. Your task is kept." },
        });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 1);
  await walkAndTalk(page, shopping.turns[0]);
  await expect(status).toContainText(shopping.turns[0].prompt);
  await reply(page, "banana");
  await expect(status).toContainText("Please repeat:");
  await page.reload();
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 1);
  await walkAndTalk(page, shopping.turns[0], true);
  await expect(status).toContainText("Please repeat:");
  await expect(status).toContainText("Conversation goals: 0/3");
  await reply(page, shopping.turns[0].sample);
  await exploredAfterGoal(page, 1);
  await walkAndTalk(page, shopping.turns[1]);
  expect(calls).toBe(1);
  await reply(page, shopping.turns[1].sample);
  await expect(status).toContainText("Local AI is busy");
  const saved = JSON.parse(
    (await page.evaluate((key) => localStorage.getItem(key), SAVE)) ?? "{}",
  );
  expect(saved.sessions[shoppingSave].answers).toHaveLength(1);
  // The failed answer is restored for editing; a fresh keystroke can change it.
  await page.keyboard.press("End");
  await page.keyboard.type(" please");
  await page.keyboard.press("Enter");
  await expect.poll(() => calls).toBe(3);
});

test("friends finish a park outing using casual replies and their own ride and snack choices", async ({
  page,
}, testInfo) => {
  test.setTimeout(150000);
  const parkIndex = scenes.findIndex((scene) => scene.id === "amusement_park");
  expect(parkIndex).toBeGreaterThan(-1);
  const park = scenes[parkIndex].roles[0];
  const answers = [
    "Carousel first!",
    "No worries, let's do something gentler.",
    "Let's have popcorn instead of ice cream.",
  ];
  const checked: string[] = [];
  const errors: string[] = [];
  page.on("console", (message) => {
    if (/SCRIPT ERROR|SHADER ERROR|Parse Error/.test(message.text()))
      errors.push(message.text());
  });
  page.on("pageerror", (error) => errors.push(error.message));
  await page.route("**/roleplay", (route) => {
    const input = route.request().postDataJSON();
    checked.push(input.text);
    return route.fulfill({
      json: {
        accepted: true,
        correction: "",
        feedback: "That gets your point across.",
        acknowledgement: "Sounds good!",
      },
    });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, parkIndex + 1);
  for (let turn = 0; turn < answers.length; turn++) {
    await walkAndTalk(page, park.turns[turn]);
    await reply(page, answers[turn]);
    await exploredAfterGoal(page, turn + 1);
  }
  await expect(status).toContainText("Mission complete");
  await expect(status).toContainText("3 independent · 0 assisted");
  const saved = JSON.parse(
    (await page.evaluate((key) => localStorage.getItem(key), SAVE)) ?? "{}",
  );
  expect(
    saved.sessions[`amusement_park/${park.id}`].answers.map(
      (answer: { text: string }) => answer.text,
    ),
  ).toEqual(answers);
  expect(checked).toEqual(answers);
  await page.mouse.move(1279, 799);
  await page.screenshot({
    path: testInfo.outputPath("park-friends-complete.png"),
  });
  expect(errors).toEqual([]);
});
