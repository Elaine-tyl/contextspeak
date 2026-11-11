import { expect, test } from "@playwright/test";
test("enroll_reveal_rate_and_pause_reviews_with_retry_safe_submission", async ({
  page,
}, testInfo) => {
  let enrolled = false;
  let rated = false;
  let attempts = 0;
  const submissions: unknown[] = [];
  const dueAt = new Date(Date.now() + 86400000).toISOString();
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let data: unknown = {};
    if (path === "/auth/session")
      data = { csrfToken: "fixture", identity: { subject: "learner" } };
    if (path === "/v1/settings") data = { enabled: true, excludedOrigins: [] };
    if (path === "/v1/lookups") data = { items: [], nextCursor: null };
    if (path === "/v1/learning-items")
      data = {
        items: [
          {
            id: "card-1",
            text: "available",
            status: "candidate",
            senseStatus: "unresolved",
            encounterCount: 1,
            sourceLanguage: "en",
            targetLanguage: "zh-Hans",
            reviewDueAt: enrolled ? dueAt : null,
          },
        ],
        nextCursor: null,
      };
    if (path.endsWith("/enroll")) {
      enrolled = true;
      data = { id: "card-1", version: 0, dueAt: new Date().toISOString() };
    }
    if (path === "/v1/reviews")
      data = {
        items:
          enrolled && !rated
            ? [
                {
                  id: "card-1",
                  version: 0,
                  prompt: "available",
                  answer: "有空的",
                  context: "Are you available?",
                  sourceLanguage: "en",
                  targetLanguage: "zh-Hans",
                  dueAt: new Date().toISOString(),
                },
              ]
            : [],
        dueCount: enrolled && !rated ? 1 : 0,
        totalCount: enrolled ? 1 : 0,
        nextDueAt: rated ? dueAt : null,
      };
    if (path.endsWith("/pause")) {
      enrolled = false;
      data = { paused: true };
    }
    if (path.endsWith("/rate")) {
      submissions.push(route.request().postDataJSON());
      if (++attempts === 1) {
        await route.fulfill({ status: 503, json: {} });
        return;
      }
      rated = true;
      data = { id: "card-1", version: 1, dueAt };
    }
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library");
  await expect(page.getByText("Connected", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Word list", exact: true }).click();
  await page
    .getByRole("button", { name: "Start reviewing", exact: true })
    .click();
  const queue = page.getByRole("region", {
    name: "Vocabulary review",
    exact: true,
  });
  await expect(
    queue.getByRole("heading", { name: "available", exact: true }),
  ).toBeVisible();
  await expect(queue.getByText("有空的", { exact: true })).toBeHidden();
  await expect(
    queue.getByRole("button", { name: "Good", exact: true }),
  ).toHaveCount(0);
  await queue.getByRole("button", { name: "Show answer", exact: true }).click();
  await expect(queue.getByText("有空的", { exact: true })).toBeVisible();
  await queue.screenshot({
    path: testInfo.outputPath("review-recall-card.png"),
  });
  await queue.getByRole("button", { name: "Good", exact: true }).click();
  await expect(queue.getByRole("alert")).toContainText("not confirmed");
  await expect(
    queue.getByRole("button", { name: "Easy", exact: true }),
  ).toBeDisabled();
  await queue.getByRole("button", { name: "Good", exact: true }).click();
  await expect(
    queue.getByRole("heading", { name: "You’re up to date.", exact: true }),
  ).toBeVisible();
  expect(submissions).toHaveLength(2);
  expect(submissions[0]).toEqual(submissions[1]);
  await expect(queue.getByText(/^Next review:/)).toBeVisible();
  await page
    .getByRole("navigation", { name: "Main" })
    .getByRole("link", { name: "Library", exact: true })
    .click();
  await page.getByRole("button", { name: "Word list", exact: true }).click();
  await page
    .getByRole("button", { name: "Pause reviews", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Start reviewing", exact: true }),
  ).toBeVisible();
});

test("saved_vocabulary_can_be_reviewed_through_the_real_API", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:3000/library");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner A", exact: true }).click();
  await expect(page.getByText("Connected", { exact: true })).toBeVisible();
  const session = await (
    await page.request.get("http://127.0.0.1:4318/auth/session")
  ).json();
  const headers = {
    Origin: "http://127.0.0.1:3000",
    "X-CSRF-Token": session.csrfToken,
  };
  await page.request.put("http://127.0.0.1:4318/v1/settings", {
    headers,
    data: { enabled: true, excludedOrigins: [] },
  });
  const capture = await page.request.post("http://127.0.0.1:4318/v1/lookups", {
    headers,
    data: {
      eventId: crypto.randomUUID(),
      text: "confident",
      context: "Vocabulary review integration",
      sourceLanguage: "en",
      targetLanguage: "en",
    },
  });
  expect(capture.status()).toBe(201);
  const receipt = await capture.json();
  await expect
    .poll(
      async () =>
        (
          await (
            await page.request.get(
              `http://127.0.0.1:4318/v1/lookups/${receipt.lookupId}/translation`,
            )
          ).json()
        ).translationStatus,
    )
    .toBe("succeeded");
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await page.getByRole("button", { name: "Word list", exact: true }).click();
  const candidate = page.getByTestId("candidate-card").filter({
    has: page.getByRole("heading", { name: "confident", exact: true }),
  });
  await candidate.getByRole("link", { name: "confident", exact: true }).click();
  await expect(page).toHaveURL(/\/library\/item\//);
  await expect(
    page.getByRole("heading", { name: "confident", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Start reviewing", exact: true })
    .click();
  await page
    .getByRole("link", { name: "Open review queue", exact: true })
    .click();
  const queue = page.getByRole("region", {
    name: "Vocabulary review",
    exact: true,
  });
  await expect(
    queue.getByRole("heading", { name: "confident", exact: true }),
  ).toBeVisible();
  await queue.getByRole("button", { name: "Show answer", exact: true }).click();
  await expect(
    queue.getByText("Sure of your ability to do something.", { exact: true }),
  ).toBeVisible();
  await queue.screenshot({
    path: testInfo.outputPath("vocabulary-review.png"),
  });
  await queue.getByRole("button", { name: "Good", exact: true }).click();
  await expect(
    queue.getByRole("heading", { name: "You’re up to date.", exact: true }),
  ).toBeVisible();
  const saved = await (
    await page.request.get("http://127.0.0.1:4318/v1/reviews")
  ).json();
  expect(saved).toMatchObject({ dueCount: 0, totalCount: 1 });
  expect(new Date(saved.nextDueAt).getTime()).toBeGreaterThan(Date.now());
});
