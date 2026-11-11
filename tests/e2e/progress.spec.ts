import { expect, test } from "@playwright/test";
test("progress_is_navigable_refresh_safe_owned_and_links_to_a_game_mode", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:3000/progress");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner A", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Learning progress", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Vocabulary review progress" }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Unlocked game levels" }),
  ).toContainText("of 10 unlocked");
  await expect(
    page.getByRole("region", { name: "Speaking progress" }),
  ).toContainText("scored answers");
  await page.route("http://127.0.0.1:4318/v1/progress", (route) =>
    route.fulfill({ status: 503, json: {} }),
  );
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "Previously loaded results are kept",
  );
  await expect(
    page.getByRole("region", { name: "Vocabulary review progress" }),
  ).toBeVisible();
  await page.unroute("http://127.0.0.1:4318/v1/progress");
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: testInfo.outputPath("learning-progress.png"),
    fullPage: true,
  });
  await page
    .getByRole("link", { name: "Practise dictation", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Dictation", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Progress", exact: true })
    .click();
  await expect(
    page.getByRole("region", { name: "Vocabulary review progress" }),
  ).toBeVisible();
  await page.route("http://127.0.0.1:4318/auth/session", (route) =>
    route.fulfill({ status: 401, json: {} }),
  );
  await expect(
    page.getByRole("link", { name: "Sign in with Google", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Vocabulary review progress" }),
  ).toHaveCount(0);
});
test("dashboard_initial_errors_do_not_fabricate_zero_statistics", async ({
  page,
}) => {
  await page.goto("http://127.0.0.1:3000/progress");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.route("http://127.0.0.1:4318/v1/progress", (route) =>
    route.fulfill({ status: 503, json: {} }),
  );
  await page.getByRole("link", { name: "Test learner B", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "Could not refresh progress",
  );
  await expect(
    page.getByRole("region", { name: "Vocabulary review progress" }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Refresh", exact: true }),
  ).toBeEnabled();
});

test("progress_layout_is_usable_on_a_phone_width", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await page.goto("http://127.0.0.1:3000/progress");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner A", exact: true }).click();
  await expect(
    page.getByRole("region", { name: "Vocabulary review progress" }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth + 1,
    ),
  ).toBe(true);
  const button = await page
    .getByRole("link", { name: "Review due vocabulary", exact: true })
    .boundingBox();
  const heading = await page
    .getByRole("heading", {
      name: "How your last 30 days of reviews went",
      exact: true,
    })
    .boundingBox();
  expect(button!.y + button!.height).toBeLessThanOrEqual(heading!.y);
});
