import { expect, test, type Page } from "@playwright/test";
import challenges from "../data/real_life.json" with { type: "json" };
import { enterScene } from "./scene-menu";
import { voiceFixture } from "./voice-fixture";
import { normalizeWave } from "../../../packages/world/protocol";

const SAVE = "contextspeak-scenes-v1";
const cafe = challenges.find((scene) => scene.id === "cafe")!.roles[0];
// Short alternative replies keep the slow software-rendered typing path bounded.
// The full authored examples and semantic review are covered by the world tests.
const opening = "Small soy flat white, for here. I like the taste.";
const laterReplies = [
  [
    "My receipt says twelve, not fourteen. Please check my order.",
    "I'll wait four minutes. I prefer my original drink.",
  ],
  [
    "Let's walk to the park. There's time before your bus.",
    "Stay here if it rains. Check at four-thirty.",
  ],
];

async function reply(page: Page, text: string) {
  await page.keyboard.type(text);
  await page.keyboard.press("Enter");
}

async function approach(page: Page, name: string, prompt: string) {
  const status = page.getByRole("status");
  await page.locator("#canvas").click({ position: { x: 640, y: 105 } });
  await page.keyboard.press("Space");
  await expect(status).toContainText(`Press E to talk to ${name}`, {
    timeout: 25000,
  });
  await page.keyboard.press("e");
  await expect(status).toContainText(prompt);
  await expect(status).toContainText("Your turn");
}

test.beforeEach(async ({ page }) => {
  await page.route("**/session", (route) =>
    route.fulfill({ json: { token: "challenge-browser" } }),
  );
  await page.route("**/speech", (route) =>
    route.fulfill({ contentType: "audio/wav", body: voiceFixture() }),
  );
});

test("cafe follow-ups, repeat, recorded reply and checkpoints preserve the original journey", async ({
  page,
}, testInfo) => {
  test.setTimeout(180000);
  const requests: any[] = [];
  let audio: Buffer | null = null;
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (/SCRIPT ERROR|SHADER ERROR|Parse Error/.test(message.text()))
      errors.push(message.text());
  });
  await page.route("**/roleplay", (route) => {
    requests.push(route.request().postDataJSON());
    return route.fulfill({
      json: {
        accepted: true,
        correction: "",
        feedback: "Clear and practical.",
        acknowledgement: "That works.",
      },
    });
  });
  await page.route("**/transcribe", (route) => {
    audio = route.request().postDataBuffer();
    return route.fulfill({ json: { text: cafe.turns[0].follow_up.sample } });
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 0, 0, 1);
  await approach(page, "Maya", cafe.turns[0].prompt);
  await reply(page, opening);
  await expect(status).toContainText(cafe.turns[0].follow_up.prompt);
  await expect(status).toContainText("Conversation goals: 0/3 · Follow-up");
  await expect(status).toContainText("Your turn");
  await page.screenshot({
    path: testInfo.outputPath("cafe-challenge-follow-up.png"),
  });
  await reply(page, "Sorry, could you say that again, please?");
  await expect(status).toContainText("Listen again");
  await expect(status).toContainText("Your turn");
  expect(requests).toHaveLength(1);

  await page.reload();
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 0, 0, 1);
  await approach(page, "Maya", cafe.turns[0].follow_up.prompt);
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Recording reply");
  await page.waitForTimeout(1500); // Actual frames from the synthetic microphone.
  await page.keyboard.press("Enter");
  await expect(status).toContainText("Conversation goals: 1/3");
  await expect(status).toContainText("Explore ·");
  expect(normalizeWave(audio!).length).toBeGreaterThan(3200);
  expect(requests[1]).toMatchObject({
    difficulty: "challenge",
    turn: 0,
    exchange: 1,
    history: [opening],
  });

  for (let turn = 1; turn < 3; turn++) {
    const goal = cafe.turns[turn];
    await approach(page, goal.npc.name, goal.prompt);
    await reply(page, laterReplies[turn - 1][0]);
    await expect(status).toContainText(goal.follow_up.prompt);
    await expect(status).toContainText(
      `Conversation goals: ${turn}/3 · Follow-up`,
    );
    await expect(status).toContainText("Your turn");
    await reply(page, laterReplies[turn - 1][1]);
    await expect(status).toContainText(`Conversation goals: ${turn + 1}/3`);
    await expect(status).toContainText("Explore ·");
  }
  await expect(status).toContainText("Mission complete");
  await page.screenshot({
    path: testInfo.outputPath("cafe-challenge-complete.png"),
  });
  const saved = await page.evaluate(
    (key) => JSON.parse(localStorage.getItem(key)!),
    SAVE,
  );
  expect(saved.sessions["cafe/customer/challenge"].answers).toHaveLength(3);
  expect(
    saved.sessions["cafe/customer/challenge"].answers.every(
      (answer: any) => !answer.assisted && answer.follow_up,
    ),
  ).toBe(true);
  expect(
    await page.evaluate(() => localStorage.getItem("contextspeak-cafe-v1")),
  ).toBeNull();
  expect(errors).toEqual([]);
});

test("switching back to Everyday discards a late challenge check and restores legacy progress", async ({
  page,
}) => {
  test.setTimeout(90000);
  let release: (() => void) | undefined;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  let submitted = false;
  await page.route("**/roleplay", async (route) => {
    submitted = true;
    await waiting;
    await route
      .fulfill({
        json: {
          accepted: true,
          correction: "",
          feedback: "Late feedback.",
          acknowledgement: "Late acknowledgement.",
        },
      })
      .catch(() => {});
  });
  await page.goto("http://127.0.0.1:4320");
  const status = page.getByRole("status");
  await expect(status).toContainText("Task goals: 0/4");
  const legacy = {
    version: 1,
    scene: "shopping",
    role: "shopper",
    answers: [{ text: "A red jacket, please.", assisted: false }],
    correction: "",
    feedback: "Your earlier progress.",
    acknowledgement: "Great.",
  };
  await page.evaluate(
    ({ key, state }) =>
      localStorage.setItem(
        key,
        JSON.stringify({ version: 1, sessions: { "shopping/shopper": state } }),
      ),
    { key: SAVE, state: legacy },
  );
  await page.reload();
  await expect(status).toContainText("Task goals: 0/4");
  await enterScene(page, 1, 0, 1);
  const shopping = challenges[0].roles[0];
  await approach(page, shopping.turns[0].npc.name, shopping.turns[0].prompt);
  await reply(page, shopping.turns[0].sample);
  await expect.poll(() => submitted).toBe(true);
  await enterScene(page, 1, 0, 0);
  release!();
  await expect(status).toContainText("Conversation goals: 1/3");
  await expect(status).toContainText("1 independent");
  await expect(status).not.toContainText("Follow-up");
  await expect(status).not.toContainText("Late");
  expect(
    await page.evaluate(
      (key) =>
        JSON.parse(localStorage.getItem(key)!).sessions["shopping/shopper"],
      SAVE,
    ),
  ).toEqual(legacy);
});
