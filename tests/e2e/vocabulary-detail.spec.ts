import { expect, test } from "@playwright/test";
test("card_click_opens_full_details_and_buttons_keep_their_actions", async ({
  page,
}, testInfo) => {
  const text =
    "Could you tell me where the nearest train station is and how long it takes to walk there?";
  let signedIn = true;
  let enrollment: unknown;
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    if (path === "/auth/session" && !signedIn) {
      await route.fulfill({ status: 401, json: {} });
      return;
    }
    const item = {
      id: "item-1",
      text,
      status: "candidate",
      senseStatus: "unresolved",
      encounterCount: 1,
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      reviewDueAt: null,
    };
    const lookup = {
      id: "lookup-1",
      candidateId: "item-1",
      text,
      context: "A traveller asks for directions.",
      meaning: "请问最近的火车站在哪里？",
      translationStatus: "succeeded",
      sourceLanguage: "en",
      targetLanguage: "zh-Hans",
      createdAt: "2026-11-08T00:00:00Z",
    };
    let data: unknown = {};
    if (path === "/auth/session")
      data = { csrfToken: "fixture", identity: { subject: "learner" } };
    if (path === "/v1/settings") data = { enabled: true, excludedOrigins: [] };
    if (path === "/v1/lookups") data = { items: [lookup], nextCursor: null };
    if (path === "/v1/learning-items")
      data = { items: [item], nextCursor: null };
    if (path === "/v1/learning-items/item-1") data = { item, lookup };
    if (path.endsWith("/enroll")) {
      enrollment = route.request().postDataJSON();
      data = { id: "item-1", version: 0, dueAt: new Date().toISOString() };
    }
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library");
  const card = page.getByTestId("lookup-card");
  await expect(card).toHaveCount(1);
  expect(
    (await card.getByRole("heading").boundingBox())!.height,
  ).toBeLessThanOrEqual(22);
  await card.getByText("Show details", { exact: true }).click();
  await expect(card.locator("details")).toHaveAttribute("open", "");
  await expect(page).toHaveURL(/\/library$/);
  await card.getByText("Hide details", { exact: true }).click();
  await card.click({ position: { x: 5, y: 5 } });
  await expect(page).toHaveURL(/\/library\/item\/item-1\?lookup=lookup-1$/);
  await expect(
    page.getByRole("heading", { name: text, exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("请问最近的火车站在哪里？", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("A traveller asks for directions.", { exact: true }),
  ).toBeVisible();
  await page
    .locator(".vocabulary-detail")
    .screenshot({ path: testInfo.outputPath("vocabulary-detail.png") });
  await page
    .getByRole("button", { name: "Start reviewing", exact: true })
    .click();
  await expect(
    page.getByRole("link", { name: "Open review queue", exact: true }),
  ).toBeVisible();
  expect(enrollment).toEqual({ lookupId: "lookup-1" });
  signedIn = false;
  await expect(
    page.getByRole("heading", { name: text, exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("link", { name: "Sign in with Google", exact: true }),
  ).toBeVisible();
});
