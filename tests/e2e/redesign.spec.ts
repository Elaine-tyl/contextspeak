import { expect, test, type Page } from "@playwright/test";

async function prepareLibrary(page: Page) {
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let data: unknown = {};
    if (path === "/auth/session")
      data = { csrfToken: "fixture", identity: { subject: "learner" } };
    if (path === "/v1/settings") data = { enabled: true, excludedOrigins: [] };
    if (path === "/v1/lookups" || path === "/v1/learning-items")
      data = { items: [], nextCursor: null };
    if (path === "/v1/reviews")
      data = { items: [], dueCount: 0, totalCount: 0, nextDueAt: null };
    await route.fulfill({ json: data });
  });
  await page.goto(
    "http://127.0.0.1:3000/library?q=available&targetLanguage=zh-Hans",
  );
}

test("main navigation preserves library filters and follows browser history", async ({
  page,
}) => {
  await prepareLibrary(page);
  const nav = page.getByRole("navigation", { name: "Main", exact: true });
  const library = nav.getByRole("link", { name: "Library", exact: true });
  const review = nav.getByRole("link", { name: "Review", exact: true });
  await expect(library).toHaveAttribute("aria-current", "page");
  await review.click();
  await expect(review).toHaveAttribute("aria-current", "page");
  await expect(library).not.toHaveAttribute("aria-current", "page");
  await expect(page).toHaveURL(/q=available/);
  await expect(page).toHaveURL(/targetLanguage=zh-Hans/);
  await expect(
    page.getByRole("heading", { name: "No reviews yet." }),
  ).toBeVisible();
  await page.goBack();
  await expect(library).toHaveAttribute("aria-current", "page");
  await expect(page.getByLabel("Search library")).toHaveValue("available");
  await nav.getByRole("link", { name: "Listen & speak", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Listen & speak", exact: true }),
  ).toBeVisible();
  await expect(page).toHaveURL(/view=practice/);
});

test("modified navigation clicks open another tab without switching this page", async ({
  page,
  context,
}) => {
  await prepareLibrary(page);
  const currentUrl = page.url();
  const opened = context.waitForEvent("page");
  await page
    .getByRole("navigation", { name: "Main", exact: true })
    .getByRole("link", { name: "Review", exact: true })
    .click({ modifiers: ["ControlOrMeta"] });
  const tab = await opened;
  await expect(tab).toHaveURL(/\/library\?view=reviews/);
  await expect(page).toHaveURL(currentUrl);
  await tab.close();
});

test("phone navigation keeps the brand and all sections reachable without page overflow", async ({
  page,
}) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await prepareLibrary(page);
  const nav = page.getByRole("navigation", { name: "Main", exact: true });
  await expect(
    nav.getByRole("link", { name: "ContextSpeak home" }),
  ).toBeVisible();
  await expect(nav.getByRole("link")).toHaveCount(7);
  await nav.getByRole("link", { name: "Account", exact: true }).click();
  await expect(page).toHaveURL(/\/account$/);
  await expect(
    nav.getByRole("link", { name: "Account", exact: true }),
  ).toHaveAttribute("aria-current", "page");
  await expect(
    page.getByRole("heading", { name: "Account", exact: true }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});

for (const width of [320, 375]) {
  test(`library and vocabulary controls fit a ${width}px phone`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 812 });
    await prepareLibrary(page);
    await expect(page.getByLabel("Search library")).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    await page.route("**/v1/learning-items/item-1", (route) =>
      route.fulfill({
        json: {
          item: {
            id: "item-1",
            text: "available",
            sourceLanguage: "en",
            targetLanguage: "zh-Hans",
            encounterCount: 1,
          },
          lookup: {
            id: "lookup-1",
            text: "available",
            meaning: "有空的",
            translationStatus: "succeeded",
          },
        },
      }),
    );
    await page.goto("http://127.0.0.1:3000/library/item/item-1");
    const practice = page.getByRole("link", {
      name: "Practice listening and speaking",
      exact: true,
    });
    await expect(practice).toBeVisible();
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
    const bounds = await practice.boundingBox();
    expect(bounds!.x).toBeGreaterThanOrEqual(0);
    expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
  });
}
