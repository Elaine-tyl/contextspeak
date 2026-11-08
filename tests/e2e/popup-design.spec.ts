import { test, expect, chromium } from "@playwright/test";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

test("language_picker_search_keyboard_and_saved_preferences", async ({}, testInfo) => {
  const directory = await mkdtemp(join(tmpdir(), "cs-popup-design-"));
  const extension = resolve("apps/extension/.output/chrome-mv3");
  const browser = await chromium.launchPersistentContext(directory, {
    channel: "chromium",
    viewport: { width: 390, height: 600 },
    headless: true,
    args: [
      `--disable-extensions-except=${extension}`,
      `--load-extension=${extension}`,
    ],
  });
  try {
    const worker =
      browser.serviceWorkers()[0] ??
      (await browser.waitForEvent("serviceworker"));
    const page = await browser.newPage();
    await page.addInitScript(() => {
      const chrome = (globalThis as any).chrome;
      chrome.runtime.sendMessage = (message: any, ...args: any[]) => {
        let preferences = JSON.parse(
          localStorage.getItem("testPreferences") ??
            '{"sourceLanguage":"auto","targetLanguage":"zh-Hans","autoTranslate":false}',
        );
        if (message.type === "translation-preferences") {
          preferences = message.value;
          localStorage.setItem("testPreferences", JSON.stringify(preferences));
        }
        const result =
          message.type === "status"
            ? {
                ok: true,
                protocolVersion: 4,
                connection: {
                  identity: "design-test",
                  enabled: true,
                  excludedOrigins: [],
                  translationPreferences: preferences,
                },
                records: [],
              }
            : message.type === "translation-options"
              ? {
                  ok: true,
                  protocolVersion: 4,
                  provider: "libretranslate-local",
                  preferences,
                  languages: [
                    { code: "en", name: "English", targets: ["en", "zh-Hans"] },
                    {
                      code: "zh-Hans",
                      name: "Chinese",
                      targets: ["en", "zh-Hans"],
                    },
                  ],
                }
              : { ok: true };
        const callback = args.find((arg) => typeof arg === "function");
        if (callback) {
          queueMicrotask(() => callback(result));
          return;
        }
        return Promise.resolve(result);
      };
    });
    await page.goto(
      `chrome-extension://${new URL(worker.url()).host}/popup.html`,
    );
    const target = page.getByRole("combobox", {
      name: "Target language",
      exact: true,
    });
    await expect(target).toHaveAttribute("data-value", "zh-Hans");
    await target.click();
    const search = page.getByRole("searchbox", {
      name: "Search Target language",
    });
    await search.fill("Spanish");
    await expect(page.getByText("No matching languages")).toBeVisible();
    await search.fill("eng");
    await expect(page.getByRole("option")).toHaveCount(1);
    await search.press("ArrowDown");
    await search.press("Enter");
    await expect(target).toHaveAttribute("data-value", "en");
    await expect(target).toBeFocused();
    await target.press("ArrowDown");
    await page.getByRole("searchbox").press("Escape");
    await expect(target).toBeFocused();
    await page
      .getByLabel("Translate automatically after selecting text", {
        exact: true,
      })
      .check();
    await page
      .getByRole("button", { name: "Save language settings", exact: true })
      .click();
    await expect(page.getByRole("status")).toHaveText("Updated.");
    await page.reload();
    await expect(target).toHaveAttribute("data-value", "en");
    await expect(
      page.getByLabel("Translate automatically after selecting text", {
        exact: true,
      }),
    ).toBeChecked();
    await target.click();
    await page.getByRole("searchbox").press("ArrowUp");
    await page.getByRole("searchbox").press("Enter");
    await expect(target).toHaveText("Chinese");
    await target.click();
    await page
      .getByRole("heading", { name: "Translation languages", exact: true })
      .click();
    await expect(page.getByRole("listbox")).toHaveCount(0);
    await target.click();
    await page.getByRole("searchbox").press("Tab");
    await expect(page.getByRole("listbox")).toHaveCount(0);
    await expect(
      page.getByLabel("Translate automatically after selecting text", {
        exact: true,
      }),
    ).toBeFocused();
    await page.screenshot({
      path: testInfo.outputPath("contextspeak-popup-redesign.png"),
      fullPage: true,
    });
    await target.click();
    await page.screenshot({
      path: testInfo.outputPath("contextspeak-popup-language-menu.png"),
      fullPage: true,
    });
  } finally {
    await browser.close();
    await rm(directory, { recursive: true, force: true });
  }
});
