import { expect, test, type Page } from "@playwright/test";
test.afterEach(async ({ page }) => {
  await page.unrouteAll({ behavior: "wait" });
});
async function prepare(page: Page) {
  await page.route("**/v1/progress", async (route) => {
    const response = await route.fetch();
    const data = await response.json();
    const priorities = Array.from({ length: 8 }, (_, index) => ({
      id: `pilot-${index}`,
      lookupId: `lookup-${index}`,
      word: index === 0 ? "available" : "A longer saved expression " + index,
      targetLanguage: index === 1 ? "vi" : "zh-Hans",
      encounters: 3,
      score: 28,
      signals: { goal: 1, encounters: 0.5, frequency: null, gap: null, due: 0 },
      goalClues: ["help"],
      matchedGoals: ["asking-for-help"],
      frequency: null,
      practiceEvidence: 0,
      due: false,
    }));
    await route.fulfill({
      json: {
        ...data,
        personalization: {
          ...data.personalization,
          priorities,
          analysis: { status: "needs-context", newContexts: 3, minimum: 10 },
          profile: {
            ...data.personalization.profile,
            goals: ["introductions", "asking-for-help"],
          },
        },
      },
    });
  });
  await page.route("**/v1/practice/pilot-*?*", async (route) => {
    const scenario = new URL(route.request().url()).searchParams.get(
      "scenario",
    );
    await route.fulfill({
      json: {
        itemId: "pilot-0",
        lookupId: "lookup-0",
        version: 1,
        expression: "available",
        sourceText: "Are you available for a call?",
        translation: "你有空接电话吗？",
        language: "en",
        recent: [],
        speakingQuestion: "When are you available to help a friend?",
        scenario: scenario
          ? {
              id: scenario,
              title: scenario,
              goal: "Use available to answer",
              role: "friend",
            }
          : undefined,
      },
    });
  });
  await page.goto("http://127.0.0.1:3000/progress");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner B", exact: true }).click();
}
test("compact priorities explain evidence and Speaking opens the right skill and scenario", async ({
  page,
}) => {
  await prepare(page);
  const priorities = page.getByRole("region", {
    name: "Vocabulary priorities",
  });
  await expect(priorities.locator(".priority-row")).toHaveCount(5);
  await priorities
    .getByRole("button", { name: "Show all 8 priorities" })
    .click();
  await expect(priorities.locator(".priority-row")).toHaveCount(8);
  await priorities
    .getByRole("button", { name: "Show fewer priorities" })
    .click();
  await expect(priorities.locator(".priority-row")).toHaveCount(5);
  await priorities.getByText("Why this word?", { exact: true }).first().click();
  await expect(
    priorities
      .getByText("unknown; no frequency estimate", { exact: false })
      .first(),
  ).toBeVisible();
  await expect(page.getByText("3 of 10 new contexts ready")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Analyze saved contexts", exact: true }),
  ).toBeDisabled();
  await page.getByRole("link", { name: "Start speaking", exact: true }).click();
  await expect(page).toHaveURL(/mode=speaking/);
  await expect(page.getByLabel("Conversation scenario")).toHaveValue(
    "asking-for-help",
  );
  await expect(
    page.getByRole("button", { name: "Speaking", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await expect(
    page.getByRole("button", { name: "Speaking", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(page).toHaveURL(/mode=speaking/);
  await page.reload();
  await expect(
    page.getByRole("button", { name: "Speaking", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page
    .getByRole("link", { name: "Back to progress", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Learning progress", exact: true }),
  ).toBeVisible();
});
test("goal drafts survive a different saved goal update until the learner reloads them", async ({
  page,
}) => {
  await prepare(page);
  await page.setViewportSize({ width: 375, height: 812 });
  const panel = page.getByRole("region", { name: "Personal learning plan" });
  await panel
    .getByRole("checkbox", { name: "Making plans", exact: true })
    .check();
  await expect(
    panel.getByText("Unsaved goal changes", { exact: true }),
  ).toBeVisible();
  await page.unrouteAll({ behavior: "wait" });
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(
    panel.getByRole("button", { name: "Reload saved goals" }),
  ).toBeVisible();
  await expect(
    panel.getByRole("checkbox", { name: "Making plans", exact: true }),
  ).toBeChecked();
  await expect(
    panel.getByRole("button", { name: "Save goals", exact: true }),
  ).toBeDisabled();
  await panel.getByRole("button", { name: "Reload saved goals" }).click();
  await expect(
    panel.getByRole("checkbox", { name: "Making plans", exact: true }),
  ).not.toBeChecked();
  await expect
    .poll(() =>
      page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    )
    .toBe(true);
  const button = await panel
    .getByRole("button", { name: "Save goals", exact: true })
    .boundingBox();
  expect(button!.height).toBeGreaterThanOrEqual(44);
});
test("failed goal saves preserve the learner draft and visible priorities", async ({
  page,
}) => {
  await prepare(page);
  let attempts = 0;
  await page.route("**/v1/personalization/goals", async (route) => {
    attempts++;
    await route.fulfill({ status: 503, json: {} });
  });
  const panel = page.getByRole("region", { name: "Personal learning plan" });
  await panel
    .getByRole("checkbox", { name: "Making plans", exact: true })
    .check();
  await panel.getByRole("button", { name: "Save goals", exact: true }).click();
  await expect(panel.getByRole("alert")).toContainText("Could not confirm");
  await expect(
    panel.getByRole("checkbox", { name: "Making plans", exact: true }),
  ).toBeChecked();
  await expect(
    panel.getByRole("button", { name: "Save goals", exact: true }),
  ).toBeEnabled();
  await expect(
    page
      .getByRole("region", { name: "Vocabulary priorities" })
      .locator(".priority-row"),
  ).toHaveCount(5);
  expect(attempts).toBe(1);
});
