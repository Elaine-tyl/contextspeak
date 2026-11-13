import { expect, test } from "@playwright/test";
import { installAudioFixtures } from "../helpers/practice-audio";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
import type {
  GameLevelRun,
  GameReceipt,
  GameRound,
  LearningProgress,
  LookupRecord,
} from "@contextspeak/contracts";

test("depth styling keeps dashboard and library navigation usable", async ({
  page,
}) => {
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        identity: { issuer: "fixture", subject: "motion-learner" },
        csrfToken: "motion-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  const progress: LearningProgress = {
    updatedAt: "2026-11-12T00:00:00Z",
    since: "2026-10-12T00:00:00Z",
    sampleLimit: 100,
    vocabulary: {
      saved: 8,
      active: 3,
      paused: 0,
      due: 2,
      reviews: 14,
      recentRatings: { again: 2, hard: 3, good: 7, easy: 2 },
      difficult: [],
    },
    listening: { scored: 8, average: 74, unscorable: 0, focus: null },
    speaking: { scored: 5, average: 61, unscorable: 0, focus: null },
    games: [
      { kind: "dictation", label: "Dictation", track: "listening", level: 3 },
    ],
  };
  const lookup: LookupRecord = {
    id: "motion-lookup",
    eventId: "motion-event",
    candidateId: "motion-candidate",
    text: "available",
    context: "Is a table available?",
    sourceUrl: "https://example.com/reading",
    sourceLanguage: "en",
    targetLanguage: "zh-Hans",
    translationStatus: "succeeded",
    meaning: "有空的，可用的",
    learningDetails: {
      kind: "word",
      definition: "Ready to use or free to meet.",
      partOfSpeech: "adjective",
      notes: null,
      collocations: [],
      examples: [
        { source: "Is a table available?", translation: "有空桌吗？" },
      ],
      version: 1,
      model: "fixture",
      modelDigest: null,
      promptVersion: 1,
    },
    errorCode: null,
    createdAt: "2026-11-12T00:00:00Z",
  };
  await page.route("**/v1/progress", (route) =>
    route.fulfill({ json: progress }),
  );
  await page.route("**/v1/lookups*", (route) =>
    route.fulfill({ json: { items: [lookup], nextCursor: null } }),
  );
  await page.route("**/v1/learning-items*", (route) =>
    route.fulfill({ json: { items: [], nextCursor: null } }),
  );
  await page.route("**/v1/settings", (route) =>
    route.fulfill({
      json: { enabled: false, excludedOrigins: [] },
    }),
  );
  await page.goto("http://127.0.0.1:3000/progress");
  await expect(
    page.getByRole("region", { name: "Listening progress", exact: true }),
  ).toContainText("74");
  await expect(
    page.getByRole("region", { name: "Your next steps" }),
  ).toBeVisible();
  await expect(page.locator(".learning-sculpture")).toBeVisible();
  await page.screenshot({
    path: "/private/tmp/contextspeak-depth-progress.png",
    fullPage: true,
  });
  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Library", exact: true })
    .click();
  const word = page.getByRole("link", { name: "available", exact: true });
  await expect(word).toBeVisible();
  await word.focus();
  await expect(word).toBeFocused();
  await expect(word).toHaveAttribute(
    "href",
    /\/library\/item\/motion-candidate\?lookup=motion-lookup/,
  );
  await page.screenshot({
    path: "/private/tmp/contextspeak-depth-library.png",
    fullPage: true,
  });
  await expect(page.getByTestId("lookup-card").locator("details")).toHaveCount(
    0,
  );
  await expect(
    page
      .getByTestId("lookup-card")
      .getByText("Is a table available?", { exact: true }),
  ).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "3D effects", exact: true }).click();
  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Progress", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "3D effects", exact: true }),
  ).toHaveAttribute("aria-pressed", "false");
  await expect(
    page.getByRole("link", { name: "Review now", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "/private/tmp/contextspeak-depth-mobile.png",
    fullPage: true,
  });
});

test("3D effects reset cleanly and respect saved and system motion preferences", async ({
  page,
}) => {
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "motion-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/progress", (route) =>
    route.fulfill({ status: 503, json: {} }),
  );
  await page.goto("http://127.0.0.1:3000/progress");
  const toggle = page.getByRole("button", { name: "3D effects", exact: true });
  const card = page.locator(".learning-sculpture");
  await expect(toggle).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator(".learning-sculpture")).toBeVisible();
  await card.hover({ position: { x: 25, y: 30 } });
  // Playwright may scroll the card into view; move again after that scroll reset.
  let bounds = (await card.boundingBox())!;
  await page.mouse.move(bounds.x + 40, bounds.y + 45);
  await expect(card).toHaveAttribute("data-tilting", "true");
  await expect
    .poll(() => card.evaluate((element) => getComputedStyle(element).transform))
    .toContain("matrix3d");
  await page.mouse.move(5, 5);
  await expect(card).not.toHaveAttribute("data-tilting");
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-pressed", "false");
  await page.reload();
  await expect(toggle).toHaveAttribute("aria-pressed", "false");
  await card.hover();
  await expect(card).not.toHaveAttribute("data-tilting");
  await toggle.click();
  await card.hover({ position: { x: 25, y: 30 } });
  bounds = (await card.boundingBox())!;
  await page.mouse.move(bounds.x + 40, bounds.y + 45);
  await expect(card).toHaveAttribute("data-tilting", "true");
  await page.emulateMedia({ reducedMotion: "reduce" });
  await expect(toggle).toBeDisabled();
  await expect(toggle).toHaveAttribute("aria-pressed", "false");
  await expect(card).not.toHaveAttribute("data-tilting");
  expect(
    await page
      .locator(".sculpture-orbit")
      .evaluate((element) => getComputedStyle(element).animationName),
  ).toBe("none");
  await page.emulateMedia({ reducedMotion: "no-preference" });
  await expect(toggle).toBeEnabled();
  await expect(toggle).toHaveAttribute("aria-pressed", "true");
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await expect(toggle).toBeVisible();
});

test("missing words supports keyboard typing without revealing or submitting unfinished answers", async ({
  page,
}) => {
  await installAudioFixtures(page);
  const round: GameRound = {
    id: "typing-gaps",
    track: "listening",
    kind: "gap-fill",
    level: 1,
    title: "Listen for the missing words",
    instruction: "Play the sentence, then fill each blank with one word.",
    audioText: "Could you confirm the ingredients and check for allergens?",
    gapText: ["Could you confirm the ", " and check for ", "?"],
  };
  const run: GameLevelRun = {
    id: "22222222-2222-4222-8222-222222222222",
    kind: "gap-fill",
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
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "test-session",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/speech", (route) => route.fulfill({ json: audio }));
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
  let submissions = 0;
  await page.route("**/v1/games/attempts", async (route) => {
    submissions++;
    const answer = route.request().postDataJSON();
    expect(answer.gapAnswers).toEqual(["ingredients", "allergens"]);
    expect(answer.completedPlays).toBeGreaterThan(0);
    await route.fulfill({
      json: {
        id: "typing-receipt",
        roundId: round.id,
        score: 100,
        createdAt: new Date().toISOString(),
        feedback: {
          explanation: "Both words correct.",
          gapFill: {
            score: 100,
            words: [
              { answer: "ingredients", expected: "ingredients", correct: true },
              { answer: "allergens", expected: "allergens", correct: true },
            ],
          },
        },
      },
    });
  });
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  const first = page.getByRole("textbox", {
    name: "Missing word 1",
    exact: true,
  });
  const second = page.getByRole("textbox", {
    name: "Missing word 2",
    exact: true,
  });
  await expect(first).toBeDisabled();
  await expect(first).toHaveValue("");
  const firstCells = page.locator(".gap-word").nth(0).locator(".letter-cell");
  await expect(firstCells).toHaveCount(11);
  await expect(
    page.locator(".gap-word").nth(1).locator(".letter-cell"),
  ).toHaveCount(9);
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(first).toBeFocused();
  await first.press("Enter");
  await expect(first).toBeFocused();
  await first.pressSequentially("ingredient");
  await first.press("Backspace");
  await first.pressSequentially("ts");
  await expect(second).toBeFocused();
  await expect(firstCells).toHaveText([
    "i",
    "n",
    "g",
    "r",
    "e",
    "d",
    "i",
    "e",
    "n",
    "t",
    "s",
  ]);
  expect(submissions).toBe(0);
  await expect(second).toHaveValue("");
  await page.keyboard.insertText("allergens");
  await expect(second).toHaveValue("allergens");
  // Clicking a filled cell edits this letter instead of restarting the word.
  await page.locator(".gap-word").nth(1).locator(".letter-cell").nth(1).click();
  await page.keyboard.type("L");
  await expect(second).toHaveValue("aLlergens");
  await second.fill("allergens");
  // IME confirmation must not be mistaken for submitting the answer.
  await second.dispatchEvent("keydown", { key: "Enter", isComposing: true });
  await expect(second).toBeEnabled();
  expect(submissions).toBe(0);
  await page.setViewportSize({ width: 390, height: 844 });
  // Filled letters animate briefly; measure line layout after finite input effects settle.
  await firstCells.evaluateAll((cells) =>
    Promise.all(
      cells
        .flatMap((cell) => cell.getAnimations())
        .filter(
          (animation) => animation.effect?.getTiming().iterations !== Infinity,
        )
        .map((animation) => animation.finished.catch(() => {})),
    ),
  );
  expect(
    await firstCells.evaluateAll(
      (cells) =>
        new Set(cells.map((cell) => cell.getBoundingClientRect().top)).size,
    ),
  ).toBe(1);
  await page.screenshot({
    path: "/private/tmp/contextspeak-typing.png",
    fullPage: true,
  });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await second.press("Enter");
  await expect(
    page.getByRole("region", { name: "Missing words feedback" }),
  ).toContainText("100/100");
  expect(submissions).toBe(1);
});

test("browser games play, save feedback, resume and keep sound preferences without an engine", async ({
  page,
}) => {
  await installAudioFixtures(page);
  await page.addInitScript(() => {
    (window as any).gameToneCount = 0;
    const NativeContext = window.AudioContext;
    window.AudioContext = class extends NativeContext {
      createOscillator() {
        (window as any).gameToneCount++;
        return super.createOscillator();
      }
    };
  });
  const round: GameRound = {
    id: "web-dictation",
    track: "listening",
    kind: "dictation",
    level: 1,
    title: "A quick coffee",
    instruction: "Listen and write the sentence.",
    audioText: "I'd like a coffee, please.",
  };
  const run: GameLevelRun = {
    id: "11111111-1111-4111-8111-111111111111",
    kind: "dictation",
    level: 1,
    topic: null,
    source: null,
    rounds: [round, { ...round, id: "second" }],
    receipts: [],
    completed: false,
    score: null,
    passed: false,
    nextLevel: null,
  };
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "test-session",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/speech", (route) => route.fulfill({ json: audio }));
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
  await page.route("**/v1/games/levels", (route) =>
    route.fulfill({ json: run }),
  );
  await page.route("**/v1/games/levels/*", (route) =>
    route.fulfill({ json: run }),
  );
  await page.route("**/v1/games/attempts", async (route) => {
    const submission = route.request().postDataJSON();
    expect(submission.response).toBe("I'd like a coffee, please.");
    expect(submission.completedPlays).toBeGreaterThan(0);
    const receipt: GameReceipt = {
      id: "receipt-one",
      roundId: round.id,
      score: 100,
      createdAt: new Date().toISOString(),
      feedback: {
        explanation: "Clear and accurate.",
        correctAnswer: round.audioText,
      },
    };
    run.receipts = [receipt];
    await route.fulfill({ json: receipt });
  });
  await page.goto("http://127.0.0.1:3000/games");
  await expect(
    page.getByRole("heading", { name: "Games", exact: true }),
  ).toBeVisible();
  await expect(page.locator("iframe")).toHaveCount(0);
  await page.getByRole("button", { name: "Sound on", exact: true }).click();
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill(round.audioText);
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Nicely done!" }),
  ).toBeVisible();
  await expect(page.getByRole("region", { name: "Game result" })).toContainText(
    "100",
  );
  await page.screenshot({
    path: "/private/tmp/contextspeak-browser-feedback.png",
    fullPage: true,
  });
  await page
    .getByRole("button", { name: "Next question", exact: true })
    .click();
  await expect(
    page.getByRole("progressbar", { name: "Level completion" }),
  ).toHaveAttribute("aria-valuenow", "1");
  await page.reload();
  await expect(
    page.getByRole("button", { name: "Sound off", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("progressbar", { name: "Level completion" }),
  ).toHaveAttribute("aria-valuenow", "1");
  await expect(page.locator("iframe")).toHaveCount(0);
  await page.goto("http://127.0.0.1:3000/games/arcade?run=" + run.id);
  await expect(
    page.getByRole("button", { name: "Play challenge", exact: true }),
  ).toBeVisible();
  await expect(page.locator("iframe")).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: "/private/tmp/contextspeak-browser-game-mobile.png",
    fullPage: true,
  });
  await page.goto("http://127.0.0.1:3000/games");
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.screenshot({
    path: "/private/tmp/contextspeak-browser-playground.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "Sound off", exact: true }).click();
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await expect
    .poll(() => page.evaluate(() => (window as any).gameToneCount))
    .toBeGreaterThan(0);
});
