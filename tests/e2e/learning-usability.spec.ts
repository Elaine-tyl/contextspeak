import { expect, test } from "@playwright/test";
test("library_search_filters_and_return_navigation_survive_refresh", async ({
  page,
}) => {
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    const item = {
      id: "item-1",
      text: "available",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      status: "candidate",
      encounterCount: 1,
    };
    const lookup = {
      id: "lookup-1",
      candidateId: "item-1",
      text: "available",
      meaning: "有空的",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: "succeeded",
      createdAt: "2026-11-08T00:00:00Z",
    };
    let data: unknown = {};
    if (path === "/auth/session")
      data = {
        csrfToken: "fixture",
        identity: { subject: "learner" },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      };
    if (path === "/v1/settings") data = { enabled: true, excludedOrigins: [] };
    if (path === "/v1/reviews")
      data = { items: [], dueCount: 0, totalCount: 0, nextDueAt: null };
    if (path === "/v1/lookups") data = { items: [lookup], nextCursor: null };
    if (path === "/v1/learning-items")
      data = { items: [item], nextCursor: null };
    if (path === "/v1/learning-items/item-1") data = { item, lookup };
    await route.fulfill({ json: data });
  });
  await page.goto(
    "http://127.0.0.1:3000/library?view=candidates&q=available&targetLanguage=zh-Hans",
  );
  await expect(page.getByLabel("Search library")).toHaveValue("available");
  await expect(page.getByLabel("Target language filter")).toHaveValue(
    "zh-Hans",
  );
  await expect(
    page.getByRole("heading", { name: "Word list", exact: true }),
  ).toBeVisible();
  await page.getByRole("link", { name: "available", exact: true }).click();
  await expect(page.getByText("Translated", { exact: true })).toBeVisible();
  await page
    .getByRole("link", { name: "Back to library", exact: true })
    .click();
  await expect(page.getByLabel("Search library")).toHaveValue("available");
  await expect(
    page.getByRole("heading", { name: "Word list", exact: true }),
  ).toBeVisible();
  await page.reload();
  await expect(page.getByLabel("Search library")).toHaveValue("available");
  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Review", exact: true })
    .click();
  await expect(page).toHaveURL(/view=reviews/);
  await expect(
    page.getByRole("heading", { name: "No reviews yet.", exact: true }),
  ).toBeVisible();
  await page.goBack();
  await expect(
    page.getByRole("heading", { name: "Word list", exact: true }),
  ).toBeVisible();
  await expect(page.getByLabel("Search library")).toHaveValue("available");
});

for (const failRestore of [false, true]) {
  test(`returning_to_paginated_history_restores_loaded_items_and_scroll_${failRestore ? "after_failure" : "normally"}`, async ({
    page,
  }) => {
    let returning = false;
    let failed = false;
    let restoreRequests = 0;
    const items = Array.from({ length: 40 }, (_, index) => ({
      id: `lookup-${index}`,
      candidateId: `item-${index}`,
      text: `word${index}`,
      meaning: "A saved meaning",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      translationStatus: "succeeded",
      createdAt: "2026-11-08T00:00:00Z",
    }));
    await page.route("http://127.0.0.1:4318/**", async (route) => {
      const url = new URL(route.request().url());
      let data: unknown = {};
      if (url.pathname === "/auth/session")
        data = { csrfToken: "fixture", identity: { subject: "learner" } };
      if (url.pathname === "/v1/settings") data = { enabled: true };
      if (
        url.pathname === "/v1/lookups" &&
        url.searchParams.has("cursor") &&
        returning
      ) {
        restoreRequests++;
        if (failRestore && !failed) {
          failed = true;
          await route.fulfill({ status: 503, json: {} });
          return;
        }
      }
      if (url.pathname === "/v1/lookups")
        data = url.searchParams.has("cursor")
          ? { items: items.slice(20), nextCursor: null }
          : { items: items.slice(0, 20), nextCursor: "second-page" };
      if (url.pathname === "/v1/learning-items")
        data = { items: [], nextCursor: null };
      if (url.pathname === "/v1/learning-items/item-39")
        data = {
          item: {
            id: "item-39",
            text: "word39",
            sourceLanguage: "en",
            targetLanguage: "zh-Hans",
          },
          lookup: items[39],
        };
      await route.fulfill({ json: data });
    });
    await page.goto("http://127.0.0.1:3000/library");
    await page.getByRole("button", { name: "Load more", exact: true }).click();
    await expect(page.getByTestId("lookup-card")).toHaveCount(40);
    await page.getByRole("link", { name: "word39", exact: true }).click();
    returning = true;
    await page
      .getByRole("link", { name: "Back to library", exact: true })
      .click();
    if (failRestore) {
      await expect(
        page.getByText("Request failed (503). Try again.", { exact: true }),
      ).toBeVisible();
      await page.waitForTimeout(3500);
      expect(restoreRequests).toBe(1);
      await page
        .getByRole("button", { name: "Load more", exact: true })
        .click();
    }
    await expect(page.getByTestId("lookup-card")).toHaveCount(40);
    await expect
      .poll(() => page.evaluate(() => window.scrollY))
      .toBeGreaterThan(500);
  });
}
