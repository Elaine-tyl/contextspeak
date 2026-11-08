import { expect, test } from "@playwright/test";

test("library_practice_navigation_search_filter_and_selected_encounter", async ({
  page,
}) => {
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const url = new URL(route.request().url());
    let data: unknown = { items: [], nextCursor: null };
    if (url.pathname === "/auth/session")
      data = {
        csrfToken: "hub",
        identity: { issuer: "fixture", subject: "learner" },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      };
    if (url.pathname === "/v1/settings") data = { enabled: true };
    if (url.pathname === "/v1/practice")
      data = {
        items:
          url.searchParams.get("q") === "missing" ||
          url.searchParams.get("targetLanguage") === "vi"
            ? []
            : [
                {
                  id: "item-1",
                  lookupId: "successful-lookup",
                  text: "available",
                  sourceLanguage: "en",
                  targetLanguage: "zh-Hans",
                },
              ],
      };
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library");
  await page.getByRole("button", { name: "Listening & speaking" }).click();
  const link = page.getByRole("link", { name: "available", exact: true });
  await expect(link).toHaveAttribute(
    "href",
    "/practice/item-1?lookup=successful-lookup",
  );
  await page.getByLabel("Conversation scenario").selectOption("introductions");
  await expect(link).toHaveAttribute(
    "href",
    "/practice/item-1?lookup=successful-lookup&scenario=introductions",
  );
  await page
    .getByLabel("Conversation scenario")
    .selectOption("asking-for-help");
  await expect(link).toHaveAttribute(
    "href",
    "/practice/item-1?lookup=successful-lookup&scenario=asking-for-help",
  );
  await page.getByLabel("Search practice").fill("missing");
  await expect(
    page.getByText("No matching practice yet.", { exact: false }),
  ).toBeVisible();
  await page.getByLabel("Search practice").fill("");
  await expect(link).toBeVisible();
  await page.getByLabel("Translation language").selectOption("vi");
  await expect(link).toHaveCount(0);
  await expect(
    page.getByText("No matching practice yet.", { exact: false }),
  ).toBeVisible();
  await page.goto("http://127.0.0.1:3000/library?view=practice");
  await expect(
    page.getByRole("heading", { name: "Listening & speaking" }),
  ).toBeVisible();
  await expect(link).toBeVisible();
});
