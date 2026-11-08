import { test, expect } from "@playwright/test";
import { resolve } from "node:path";
import { learningFixture } from "../fixtures/learning";

test("automatic_card_is_compact_expandable_dismissible_and_reports_saved_state", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:3000/sample.html");
  await page.evaluate((learning) => {
    let mode = "saved";
    (window as any).setLookupMode = (value: string) => {
      mode = value;
    };
    (window as any).browser = {
      runtime: {
        id: "ui-test",
        sendMessage: async (message: any) => {
          if (message.type === "translation-options")
            return {
              ok: true,
              protocolVersion: 4,
              provider:
                mode === "plain-saved" ? "libretranslate-local" : "gemini",
              languages: [{ code: "en", name: "English", targets: ["en"] }],
              preferences: {
                sourceLanguage: "en",
                targetLanguage: "en",
                autoTranslate: true,
              },
            };
          if (message.type === "lookup-result" && mode === "quota")
            return {
              ok: false,
              status: "failed",
              saved: "library",
              error: "Gemini quota reached. Your lookup is saved.",
            };
          if (message.type === "translate" && mode === "delayed")
            return new Promise((resolve) => {
              (window as any).releaseTranslation = () =>
                resolve({
                  ok: true,
                  status: "queued",
                  eventId: message.input.eventId,
                });
            });
          if (message.type === "translate")
            return mode === "saved" ||
              mode === "quota" ||
              mode === "plain-saved"
              ? { ok: true, status: "queued", eventId: message.input.eventId }
              : {
                  ok: true,
                  status: "succeeded",
                  meaning: "Free to take part.",
                  learningDetails: learning,
                };
          return {
            ok: true,
            status: "succeeded",
            meaning: "Free to take part.",
            learningDetails: learning,
          };
        },
      },
    };
  }, learningFixture);
  await page.addScriptTag({
    path: resolve(
      "apps/extension/.output/chrome-mv3/content-scripts/content.js",
    ),
  });
  async function select() {
    await page.locator("#available").evaluate((element) => {
      const range = document.createRange();
      range.selectNodeContents(element);
      window.getSelection()!.removeAllRanges();
      window.getSelection()!.addRange(range);
    });
    await page.locator("#available").dispatchEvent("mouseup");
  }
  await select();
  await expect(page.getByRole("status")).toHaveText("Free to take part.");
  await expect(
    page.getByText("Saved to library", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeHidden();
  await expect(
    page.getByRole("button", { name: "Translate selection", exact: true }),
  ).toBeHidden();
  const region = page.getByRole("region", {
    name: "ContextSpeak translation",
    exact: true,
  });
  expect((await region.boundingBox())!.height).toBeLessThan(360);
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-compact-card.png"),
  });
  await page.getByText("Show explanation", { exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-expanded-card.png"),
  });
  await page
    .getByRole("button", { name: "Close translation", exact: true })
    .click();
  await expect(region).toHaveCount(0);
  await page.waitForTimeout(600);
  await expect(region).toHaveCount(0);
  await page.evaluate(() => (window as any).setLookupMode("transient"));
  await select();
  await expect(
    page.getByText("Not saved · collection off or site excluded", {
      exact: true,
    }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(region).toHaveCount(0);
  await page.evaluate(() => (window as any).setLookupMode("quota"));
  await select();
  await expect(page.getByRole("status")).toContainText("Gemini quota reached");
  await expect(
    page.getByText("Saved to library", { exact: true }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await page.evaluate(() => (window as any).setLookupMode("delayed"));
  await select();
  await expect(page.getByRole("status")).toHaveText("Translating…");
  await page
    .getByRole("button", { name: "Close translation", exact: true })
    .click();
  await page.evaluate(() => (window as any).releaseTranslation());
  await page.waitForTimeout(600);
  await expect(region).toHaveCount(0);
  await page.evaluate(() => (window as any).setLookupMode("plain-saved"));
  await select();
  await expect(
    page.getByText("Saved to library", { exact: true }),
  ).toBeVisible();
  await page.evaluate(() => (window as any).setLookupMode("transient"));
  await page
    .getByRole("button", { name: "Translate selection", exact: true })
    .click();
  await expect(
    page.getByText("Not saved · collection off or site excluded", {
      exact: true,
    }),
  ).toBeVisible();
});

test("library_filters_reset_pagination_survive_polling_and_show_no_results", async ({
  page,
}, testInfo) => {
  const paths: string[] = [];
  let releaseOld: (() => void) | undefined;
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const url = new URL(route.request().url());
    if (
      url.pathname === "/v1/lookups" &&
      url.searchParams.get("q") === "obsolete"
    ) {
      await new Promise<void>((resolve) => {
        releaseOld = resolve;
      });
      await route.fulfill({
        status: 503,
        json: { message: "Obsolete search failed" },
      });
      return;
    }
    let data: any = {};
    if (url.pathname === "/auth/session")
      data = { csrfToken: "test", identity: { subject: "learner" } };
    if (url.pathname === "/v1/settings")
      data = { enabled: true, excludedOrigins: [] };
    if (url.pathname === "/v1/lookups") {
      paths.push(url.search);
      const matches =
        (!url.searchParams.get("q") ||
          "available".includes(url.searchParams.get("q")!)) &&
        (!url.searchParams.get("targetLanguage") ||
          url.searchParams.get("targetLanguage") === "en");
      data = {
        items: matches
          ? [
              {
                id: url.searchParams.get("cursor") ? "older" : "first",
                text: "available",
                context: "Are you available for a quick call?",
                learningDetails: learningFixture,
                meaning: "Free",
                sourceLanguage: "en",
                targetLanguage: "en",
                translationStatus: "succeeded",
                createdAt: "2026-11-08T00:00:00Z",
              },
            ]
          : [],
        nextCursor: matches && !url.searchParams.get("cursor") ? "older" : null,
      };
    }
    if (url.pathname === "/v1/learning-items")
      data = { items: [], nextCursor: null };
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library");
  await expect(page.getByTestId("lookup-card")).toHaveCount(1);
  const card = page.getByTestId("lookup-card");
  await expect(card.getByText("Free", { exact: true })).toBeVisible();
  await expect(
    card.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeHidden();
  expect((await card.boundingBox())!.height).toBeLessThan(300);
  await card.getByText("Show details", { exact: true }).click();
  await expect(
    card.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeVisible();
  await expect(
    card.getByText("“Are you available for a quick call?”", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(
    card.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeVisible();
  await card.locator("summary").focus();
  await page.keyboard.press("Enter");
  await expect(
    card.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeHidden();
  await card.screenshot({
    path: testInfo.outputPath("compact-library-card.png"),
  });
  await page.getByRole("button", { name: "Load more", exact: true }).click();
  await expect(page.getByTestId("lookup-card")).toHaveCount(2);
  await page
    .getByRole("searchbox", { name: "Search library", exact: true })
    .fill("obsolete");
  await expect.poll(() => !!releaseOld).toBe(true);
  await page
    .getByRole("searchbox", { name: "Search library", exact: true })
    .fill("available");
  await expect(page.getByTestId("lookup-card")).toHaveCount(1);
  releaseOld!();
  await expect(
    page.getByText("Obsolete search failed", { exact: true }),
  ).toBeHidden();
  await page
    .getByLabel("Target language filter", { exact: true })
    .selectOption("fr");
  await expect(
    page.getByText("No matching vocabulary.", { exact: true }),
  ).toBeVisible();
  await page.locator(".library-section").screenshot({
    path: testInfo.outputPath("contextspeak-library-filters.png"),
  });
  await page.waitForTimeout(3200);
  expect(paths.at(-1)).toContain("q=available");
  expect(paths.at(-1)).toContain("targetLanguage=fr");
  expect(paths.at(-1)).not.toContain("cursor=");
  await page
    .getByRole("button", { name: "Clear filters", exact: true })
    .click();
  await expect(page.getByTestId("lookup-card")).toHaveCount(1);
});
