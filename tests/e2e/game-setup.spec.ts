import { expect, test, type Page } from "@playwright/test";
import type { GameRound } from "@contextspeak/contracts";

async function setupCatalog(page: Page, recommended = 5) {
  const rounds: GameRound[] = [1, 5, 8].flatMap((level) =>
    ["Everyday", "Travel"].map((topic) => ({
      id: `picture-${level}-${topic}`,
      kind: "picture" as const,
      track: "listening" as const,
      level,
      topic,
      title: "Find the right picture",
      instruction: "Listen, then choose a picture.",
      audioText: "A cup of coffee.",
      choices: [{ id: "coffee", label: "Coffee", picture: "coffee" as const }],
    })),
  );
  const catalog = {
    rounds,
    progress: {
      listening: { level: 10, passed: [] },
      speaking: { level: 1, passed: [] },
    },
    modeProgress: { picture: { level: 8 } },
    recommendations: {
      listening: {
        level: recommended,
        reason: "Three independent results support a harder task.",
        focus: "taskResponse",
      },
    },
  };
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "level-map-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/games", (route) => route.fulfill({ json: catalog }));
  return rounds;
}

test("game entry uses its own level map and starts the chosen theme without changing unlock rules", async ({
  page,
}, testInfo) => {
  const rounds = await setupCatalog(page);
  await page.goto("http://127.0.0.1:3000/games");
  await page
    .getByRole("button", { name: "Picture choice", exact: true })
    .click();
  const entry = page.getByTestId("game-setup");
  await expect(entry).toHaveAttribute("data-kind", "picture");
  await expect(entry.locator(".studio-visual")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  await expect(
    page.getByRole("button", { name: "Level 1", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.getByRole("button", { name: "Level 9 · Locked", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Level 10 · Locked", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Use recommended level", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 5", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByLabel("Theme", { exact: true }).selectOption("Travel");
  await entry.screenshot({
    path: testInfo.outputPath("contextspeak-picture-level-map.png"),
  });
  let submissions = 0;
  await page.route("**/v1/games/levels", async (route) => {
    submissions++;
    const payload = route.request().postDataJSON();
    expect(payload).toMatchObject({
      kind: "picture",
      level: 5,
      topic: "Travel",
    });
    expect(payload).not.toHaveProperty("source");
    await route.fulfill({
      json: {
        id: "33333333-3333-4333-8333-333333333333",
        kind: "picture",
        level: 5,
        topic: "Travel",
        source: null,
        rounds: rounds.filter(
          (round) => round.level === 5 && round.topic === "Travel",
        ),
        receipts: [],
        completed: false,
        score: null,
        passed: false,
        nextLevel: null,
      },
    });
  });
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await expect(page.getByTestId("game-studio")).toHaveAttribute(
    "data-kind",
    "picture",
  );
  await expect(
    page.getByRole("button", { name: "Play challenge", exact: true }),
  ).toBeEnabled();
  expect(submissions).toBe(1);
});

test("phone level map caps recommendations and handles empty filtered content", async ({
  page,
}, testInfo) => {
  await setupCatalog(page, 10);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("http://127.0.0.1:3000/games?mode=picture");
  const entry = page.getByTestId("game-setup");
  await expect(entry).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Practice recommendation" }),
  ).toContainText("Level 8");
  await page
    .getByRole("button", { name: "Use recommended level", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 8", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "Level 3", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeDisabled();
  await expect(entry).toContainText("No matching questions");
  await page.getByRole("button", { name: "Level 5", exact: true }).click();
  await page.getByLabel("Theme", { exact: true }).selectOption("Travel");
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("ContextSpeak");
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeEnabled();
  await expect(entry.locator(".studio-visual")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  const levels = page.getByRole("group", {
    name: "Game difficulty",
    exact: true,
  });
  for (const button of await levels.getByRole("button").all()) {
    const bounds = (await button.boundingBox())!;
    expect(bounds.width).toBeGreaterThanOrEqual(44);
    expect(bounds.height).toBeGreaterThanOrEqual(44);
  }
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  const entryBounds = (await entry.boundingBox())!;
  const motionBounds = (await page
    .getByRole("button", { name: "3D effects", exact: true })
    .boundingBox())!;
  expect(motionBounds.y).toBeGreaterThanOrEqual(
    entryBounds.y + entryBounds.height,
  );
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-level-map-mobile.png"),
    fullPage: true,
  });
});

test("an older server catalog explains missing games and Refresh recovers new questions", async ({
  page,
}) => {
  await setupCatalog(page);
  await page.goto("http://127.0.0.1:3000/games?mode=reading-question");
  const entry = page.getByTestId("game-setup");
  await expect(entry).toContainText(
    "This game's questions are not loaded on the server",
  );
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeDisabled();
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: [
          {
            id: "reading-recovered",
            kind: "reading-question",
            track: "reading",
            level: 1,
            title: "A new story",
            instruction: "Read, then answer.",
            passage: "Maya waited for her friend.",
            audioText: "",
            choices: [{ id: "friend", label: "Her friend" }],
          },
        ],
        progress: {
          listening: { level: 1, passed: [] },
          speaking: { level: 1, passed: [] },
        },
        modeProgress: { "reading-question": { level: 1 } },
      },
    }),
  );
  await page
    .getByRole("button", { name: "Refresh progress", exact: true })
    .click();
  await expect(entry).toContainText("1 question · Level 1");
  await expect(entry).not.toContainText("not loaded on the server");
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeEnabled();
});

test("empty source filters recover without changing level, and changing level clears stale filters", async ({
  page,
}) => {
  const rounds = await setupCatalog(page);
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: [
          ...rounds,
          {
            ...rounds[0],
            id: "licensed-picture-8",
            level: 8,
            source: {
              id: "example",
              provider: "VOA Learning English",
              title: "Example",
              author: "Voice of America",
              license: "Public-domain-VOA",
              licenseUrl: "https://learningenglish.voanews.com/p/6861.html",
              url: "https://learningenglish.voanews.com/",
            },
          },
        ],
        progress: {
          listening: { level: 8, passed: [] },
          speaking: { level: 1, passed: [] },
        },
        modeProgress: { picture: { level: 8 } },
      },
    }),
  );
  await page.goto("http://127.0.0.1:3000/games?mode=picture");
  await page.getByRole("button", { name: "Level 1", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("VOA Learning English");
  await expect(page.getByTestId("game-setup")).toContainText(
    "No matching questions",
  );
  await page
    .getByRole("button", { name: "Clear filters", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeEnabled();
  await page.getByRole("button", { name: "Level 8", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("VOA Learning English");
  await page.getByRole("button", { name: "Level 5", exact: true }).click();
  await expect(page.getByLabel("Content source", { exact: true })).toHaveValue(
    "all",
  );
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeEnabled();
});
