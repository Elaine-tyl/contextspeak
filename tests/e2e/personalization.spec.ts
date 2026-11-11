import { expect, test } from "@playwright/test";
test("learners confirm goals, inspect priorities and reach account export", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:3000/progress");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner A", exact: true }).click();
  const panel = page.getByRole("region", { name: "Personal learning plan" });
  await expect(panel).toBeVisible();
  await panel.getByRole("button", { name: "Reset goals", exact: true }).click();
  await expect(panel.getByRole("status")).toHaveText("Goals saved.");
  await panel
    .getByRole("checkbox", { name: "Making plans", exact: true })
    .check();
  await panel.getByRole("button", { name: "Reset goals", exact: true }).click();
  await expect(
    panel.getByRole("checkbox", { name: "Making plans", exact: true }),
  ).not.toBeChecked();
  await panel
    .getByRole("checkbox", { name: "Making plans", exact: true })
    .check();
  await page.route("**/v1/progress", async (route) => {
    const response = await route.fetch();
    const data = await response.json();
    await route.fulfill({
      json: { ...data, updatedAt: "2035-01-01T00:00:00.000Z" },
    });
  });
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.getByText(/^Updated /)).toContainText("2035");
  await expect(
    panel.getByRole("checkbox", { name: "Making plans", exact: true }),
  ).toBeChecked();
  await page.unroute("**/v1/progress");
  await panel.getByRole("button", { name: "Save goals", exact: true }).click();
  await expect(panel.getByRole("status")).toHaveText("Goals saved.");
  await page.reload();
  await expect(
    panel.getByRole("checkbox", { name: "Making plans", exact: true }),
  ).toBeChecked();
  await expect(
    panel.getByRole("button", { name: "Analyze saved contexts", exact: true }),
  ).toBeDisabled();
  await expect(panel).toContainText("not a mastery score");
  await page.setViewportSize({ width: 375, height: 812 });
  await expect
    .poll(() =>
      page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    )
    .toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("personal-plan-mobile.png"),
    fullPage: true,
  });
  await page.getByRole("link", { name: "Account", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Account", exact: true }),
  ).toBeVisible();
  const download = page.waitForEvent("download");
  await page
    .getByRole("button", { name: "Download my data", exact: true })
    .click();
  expect((await download).suggestedFilename()).toBe("contextspeak-data.json");
});
test("scenario_analysis_waits_for_slow_provider_responses", async ({
  page,
}) => {
  await page.route("**/v1/progress", async (route) => {
    const response = await route.fetch();
    const data = await response.json();
    await route.fulfill({
      json: {
        ...data,
        personalization: {
          ...data.personalization,
          contextCount: 10,
          suggestionAvailable: true,
          analysis: { status: "ready", newContexts: 10, minimum: 10 },
        },
      },
    });
  });
  await page.route("**/v1/personalization/suggestions", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 6500));
    await route.fulfill({ status: 201, json: {} });
  });
  await page.goto("http://127.0.0.1:3000/progress");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner B", exact: true }).click();
  const panel = page.getByRole("region", { name: "Personal learning plan" });
  await panel
    .getByRole("button", { name: "Analyze saved contexts", exact: true })
    .click();
  await expect(panel.getByRole("status")).toHaveText(
    "Suggestions checked. Choose which goals to use.",
    { timeout: 15000 },
  );
});
