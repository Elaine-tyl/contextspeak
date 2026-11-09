import { test, expect } from "@playwright/test";
import { resolve } from "node:path";
import { learningFixture } from "../fixtures/learning";
test("translation_renders_before_related_words_and_keeps_one_base_result", async ({
  page,
}) => {
  await page.goto("http://127.0.0.1:3000/sample.html");
  await page.evaluate((learning) => {
    const state = ((window as any).expansionFixture = {
      translations: 0,
      expansions: 0,
    });
    const base = {
      ok: true,
      status: "succeeded",
      meaning: "Free to take part.",
      detectedSourceLanguage: "en",
      learningDetails: learning,
    };
    (window as any).browser = {
      runtime: {
        id: "expansion-test",
        sendMessage: async (message: any) => {
          if (message.type === "translation-options")
            return {
              ok: true,
              protocolVersion: 4,
              provider: "gemini",
              languages: [{ code: "en", name: "English", targets: ["en"] }],
              preferences: {
                sourceLanguage: "en",
                targetLanguage: "en",
                autoTranslate: true,
              },
            };
          if (message.type === "translate") {
            state.translations++;
            return base;
          }
          if (message.type === "lookup-expand") {
            state.expansions++;
            return new Promise((resolve) => {
              (window as any).releaseExpansion = () =>
                resolve({
                  ...base,
                  learningDetails: {
                    ...learning,
                    expansion: {
                      model: "fixture-expansion",
                      synonyms: [
                        {
                          term: "free",
                          difference: "Can describe having time.",
                          example: {
                            source: "I am free today.",
                            translation: "I have time today.",
                          },
                        },
                      ],
                      lookalikes: [],
                    },
                  },
                });
            });
          }
          return { ok: true };
        },
      },
    };
  }, learningFixture);
  await page.addScriptTag({
    path: resolve(
      "apps/extension/.output/chrome-mv3/content-scripts/content.js",
    ),
  });
  await page.locator("#available").evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element);
    window.getSelection()!.removeAllRanges();
    window.getSelection()!.addRange(range);
  });
  await page.locator("#available").dispatchEvent("mouseup");
  await expect(page.getByRole("status")).toHaveText("Free to take part.");
  await expect
    .poll(() =>
      page.evaluate(() => (window as any).expansionFixture.expansions),
    )
    .toBe(1);
  await page.getByText("Show explanation", { exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Examples", exact: true }),
  ).toBeVisible();
  await page.evaluate(() => (window as any).releaseExpansion());
  await expect(
    page.getByRole("heading", { name: "Similar meanings", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Can describe having time.", { exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("status")).toHaveText("Free to take part.");
  expect(
    await page.evaluate(() => (window as any).expansionFixture.translations),
  ).toBe(1);
});
