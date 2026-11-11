import { expect, test, type Page } from "@playwright/test";

const prompt = {
  id: "review-1",
  version: 0,
  prompt: "available",
  answer: "有空的",
  context: "Are you available?",
  sourceLanguage: "en",
  targetLanguage: "zh-Hans",
  dueAt: new Date().toISOString(),
};

async function prepare(page: Page, reviews: () => unknown) {
  await page.clock.install({ time: new Date() });
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/rate")) {
      await route.fulfill({ status: 503, json: {} });
      return;
    }
    let data: unknown = {};
    if (path === "/auth/session")
      data = { csrfToken: "fixture", identity: { subject: "learner" } };
    if (path === "/v1/settings") data = { enabled: true, excludedOrigins: [] };
    if (path === "/v1/lookups" || path === "/v1/learning-items")
      data = { items: [], nextCursor: null };
    if (path === "/v1/reviews") {
      data = reviews();
      if (data === null) {
        await route.fulfill({ status: 503, json: {} });
        return;
      }
    }
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library?view=reviews");
  return page.getByRole("region", { name: "Vocabulary review", exact: true });
}

test("due_cards_appear_automatically_without_interrupting_unconfirmed_recall", async ({
  page,
}) => {
  let reads = 0;
  let due = false;
  const nextDueAt = new Date(Date.now() + 60000).toISOString();
  const queue = await prepare(page, () => {
    reads++;
    return {
      items: due ? [prompt] : [],
      dueCount: due ? 1 : 0,
      totalCount: 1,
      nextDueAt: due ? null : nextDueAt,
    };
  });
  await expect(queue.getByText("You’re up to date.")).toBeVisible();
  due = true;
  await page.clock.fastForward(65000);
  await expect(
    queue.getByRole("heading", { name: "available", exact: true }),
  ).toBeVisible();
  await queue.getByRole("button", { name: "Show answer", exact: true }).click();
  await queue.getByRole("button", { name: "Good", exact: true }).click();
  await expect(queue.getByRole("alert")).toContainText("not confirmed");
  const before = reads;
  await page.clock.fastForward(65000);
  await page.evaluate(() =>
    document.dispatchEvent(new Event("visibilitychange")),
  );
  await expect(queue.getByText("有空的", { exact: true })).toBeVisible();
  await expect(queue.getByRole("alert")).toContainText("not confirmed");
  expect(reads).toBe(before);
});

test("automatic_review_refresh_stops_after_failure_and_allows_manual_recovery", async ({
  page,
}) => {
  let reads = 0;
  let recovered = false;
  let fail = false;
  const nextDueAt = new Date(Date.now() + 60000).toISOString();
  const queue = await prepare(page, () => {
    reads++;
    if (fail && !recovered) return null;
    return {
      items: recovered ? [prompt] : [],
      dueCount: recovered ? 1 : 0,
      totalCount: 1,
      nextDueAt: recovered ? null : nextDueAt,
    };
  });
  await expect(queue.getByText("You’re up to date.")).toBeVisible();
  const before = reads;
  fail = true;
  await page.clock.fastForward(65000);
  await expect(queue.getByRole("alert")).toContainText(
    "Could not load reviews",
  );
  await page.clock.fastForward(65000);
  await page.evaluate(() =>
    document.dispatchEvent(new Event("visibilitychange")),
  );
  expect(reads).toBe(before + 1);
  recovered = true;
  await queue
    .getByRole("button", { name: "Refresh queue", exact: true })
    .click();
  await expect(
    queue.getByRole("heading", { name: "available", exact: true }),
  ).toBeVisible();
});

test("returning_to_an_empty_queue_checks_for_cards_added_elsewhere", async ({
  page,
}) => {
  let added = false;
  const queue = await prepare(page, () => ({
    items: added ? [prompt] : [],
    dueCount: added ? 1 : 0,
    totalCount: added ? 1 : 0,
    nextDueAt: null,
  }));
  await expect(queue.getByText("No reviews yet.")).toBeVisible();
  added = true;
  await page.evaluate(() =>
    document.dispatchEvent(new Event("visibilitychange")),
  );
  await expect(
    queue.getByRole("heading", { name: "available", exact: true }),
  ).toBeVisible();
});
test("malformed_review_refresh_keeps_revealed_recall_and_navigation_usable", async ({
  page,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  let malformed = false;
  const queue = await prepare(page, () =>
    malformed
      ? {}
      : { items: [prompt], dueCount: 1, totalCount: 1, nextDueAt: null },
  );
  await queue.getByRole("button", { name: "Show answer", exact: true }).click();
  malformed = true;
  await queue
    .getByRole("button", { name: "Refresh queue", exact: true })
    .click();
  await expect(queue.getByRole("alert")).toContainText(
    "Could not load reviews",
  );
  await expect(queue.getByText("有空的", { exact: true })).toBeVisible();
  await expect(
    queue.getByRole("heading", { name: "No reviews yet.", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Library", exact: true })
    .click();
  await page.getByRole("button", { name: "Word list", exact: false }).click();
  await expect(
    page.getByRole("heading", { name: "Word list", exact: true }),
  ).toBeVisible();
  expect(errors).toEqual([]);
});
