import { expect, test, type Page } from "@playwright/test";
import type { GameRound, GameLevelRun } from "@contextspeak/contracts";
import { installAudioFixtures } from "../helpers/practice-audio";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";

const text =
  "Maya chose a table beside the window.\n\nShe waited to order and drank some water.";
const words = ["window", "order", "water"];
async function openRecall(page: Page, long = false) {
  await installAudioFixtures(page);
  const prefix = long
    ? "The visitors discussed their plans for the afternoon. ".repeat(14)
    : "";
  const passage = prefix + text;
  const round: GameRound = {
    id: "recall-browser-v2",
    kind: "reading-question",
    track: "reading",
    level: 1,
    title: "A seat by the window",
    instruction: "Listen and read, then fill the highlighted words.",
    recall: true,
    passage,
    audioText: passage,
    gapText: [
      prefix + "Maya chose a table beside the ",
      ".\n\nShe waited to ",
      " and drank some ",
      ".",
    ],
  };
  const run: GameLevelRun = {
    id: "55555555-5555-4555-8555-555555555555",
    kind: "reading-question",
    level: 1,
    topic: null,
    source: null,
    rounds: [round],
    receipts: [],
    completed: false,
    score: null,
    passed: false,
    nextLevel: null,
  };
  const session = {
    csrfToken: "recall-browser",
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  };
  await page.route("**/auth/session", (route) =>
    route.fulfill({ json: session }),
  );
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: [round],
        progress: {
          listening: { level: 1, passed: [] },
          speaking: { level: 1, passed: [] },
        },
      },
    }),
  );
  await page.route("**/v1/games/levels/*", (route) =>
    route.fulfill({ json: run }),
  );
  const audio = await new FixtureSpeechProvider().generate();
  let speechRequests = 0;
  let fail = false;
  await page.route("**/v1/speech", async (route) => {
    speechRequests++;
    const input = route.request().postDataJSON();
    expect(input.language).toBe("en");
    if (long) expect([...input.text].length).toBeLessThanOrEqual(600);
    else expect(input.text).toBe(text);
    await route.fulfill(fail ? { status: 429, json: {} } : { json: audio });
  });
  let submissions = 0;
  await page.route("**/v1/games/attempts", async (route) => {
    submissions++;
    const body = route.request().postDataJSON();
    expect(body.completedPlays).toBeGreaterThan(0);
    expect(body.gapAnswers).toEqual(words);
    expect(body).not.toHaveProperty("choice");
    await route.fulfill({
      json: {
        id: "recall-mark",
        roundId: round.id,
        score: 100,
        createdAt: new Date().toISOString(),
        feedback: {
          explanation: "All keywords correct.",
          gapFill: {
            score: 100,
            words: words.map((word) => ({
              answer: word,
              expected: word,
              correct: true,
            })),
          },
        },
      },
    });
  });
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  await page.evaluate(() => {
    (window as any).audioFixture.holdAudio = true;
  });
  return {
    speechRequests: () => speechRequests,
    submissions: () => submissions,
    fail: (value: boolean) => {
      fail = value;
    },
  };
}

test("Start reads the marked passage once, then replaces the keywords with answer boxes", async ({
  page,
}, info) => {
  const requests = await openRecall(page);
  await expect(page.locator(".recall-keyword")).toHaveCount(3);
  await expect(
    page.getByRole("textbox", { name: "Missing word 1", exact: true }),
  ).toHaveCount(0);
  expect(requests.speechRequests()).toBe(0);
  await page.getByRole("button", { name: "Start", exact: true }).click();
  await expect(
    page.getByText("AI audio in progress.", { exact: true }),
  ).toBeVisible();
  await expect(page.locator(".recall-keyword")).toHaveCount(3);
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeDisabled();
  expect(requests.speechRequests()).toBe(1);
  await page.screenshot({
    path: info.outputPath("reading-recall-highlighted.png"),
    fullPage: true,
  });
  await page.evaluate(() => (window as any).audioFixture.finishAudio());
  await expect(page.locator(".recall-keyword")).toHaveCount(0);
  for (let i = 0; i < words.length; i++)
    await page
      .getByRole("textbox", { name: `Missing word ${i + 1}`, exact: true })
      .fill(words[i]);
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeEnabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: info.outputPath("reading-recall-fill-mobile.png"),
    fullPage: true,
  });
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(page.getByRole("region", { name: "Game result" })).toContainText(
    "All keywords correct.",
  );
  expect(requests.submissions()).toBe(1);
});

test("failed audio keeps the preview; replay keeps typed answers hidden from the original text", async ({
  page,
}) => {
  const requests = await openRecall(page);
  requests.fail(true);
  await page.getByRole("button", { name: "Start", exact: true }).click();
  await expect(
    page.getByTestId("game-studio").getByRole("alert"),
  ).toContainText("AI voice quota");
  await expect(page.locator(".recall-keyword")).toHaveCount(3);
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeDisabled();
  requests.fail(false);
  await page.getByRole("button", { name: "Start", exact: true }).click();
  await expect(
    page.getByText("AI audio in progress.", { exact: true }),
  ).toBeVisible();
  await page.evaluate(() => (window as any).audioFixture.finishAudio());
  const first = page.getByRole("textbox", {
    name: "Missing word 1",
    exact: true,
  });
  await first.fill("window");
  await page.getByRole("button", { name: "Replay audio", exact: true }).click();
  await expect(
    page.getByText("AI audio in progress.", { exact: true }),
  ).toBeVisible();
  await expect(page.locator(".recall-keyword")).toHaveCount(0);
  await expect(first).toBeDisabled();
  await expect(first).toHaveValue("window");
  await page.evaluate(() => (window as any).audioFixture.finishAudio());
  await expect(first).toBeEnabled();
  await expect(first).toHaveValue("window");
  expect(requests.submissions()).toBe(0);
});

test("interrupted narration does not hide the highlighted words or enable an answer", async ({
  page,
}) => {
  const requests = await openRecall(page);
  await page.getByRole("button", { name: "Start", exact: true }).click();
  await expect(
    page.getByText("AI audio in progress.", { exact: true }),
  ).toBeVisible();
  await page.evaluate(() => {
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      value: "hidden",
    });
    document.dispatchEvent(new Event("visibilitychange"));
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      value: "visible",
    });
  });
  await expect(
    page.getByRole("button", { name: "Start", exact: true }),
  ).toBeEnabled();
  await expect(page.locator(".recall-keyword")).toHaveCount(3);
  await expect(
    page.getByRole("textbox", { name: "Missing word 1", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeDisabled();
  expect(requests.submissions()).toBe(0);
});

test("an advanced passage finishes every narration chunk before any keyword disappears", async ({
  page,
}) => {
  const requests = await openRecall(page, true);
  await page.getByRole("button", { name: "Start", exact: true }).click();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.spoken.length))
    .toBe(1);
  await page.evaluate(() => (window as any).audioFixture.finishAudio());
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.spoken.length))
    .toBe(2);
  await expect(page.locator(".recall-keyword")).toHaveCount(3);
  await expect(
    page.getByRole("textbox", { name: "Missing word 1", exact: true }),
  ).toHaveCount(0);
  await page.evaluate(() => (window as any).audioFixture.finishAudio());
  await expect(page.locator(".recall-keyword")).toHaveCount(0);
  await expect(
    page.getByRole("textbox", { name: "Missing word 1", exact: true }),
  ).toBeEnabled();
  expect(requests.speechRequests()).toBe(2);
});
