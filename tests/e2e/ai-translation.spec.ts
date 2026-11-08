import { test, expect, chromium } from "@playwright/test";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

test("ai_automatically_generates_and_saves_the_same_learning_card", async () => {
  const profile = await mkdtemp(join(tmpdir(), "cs-real-translation-"));
  const extension = resolve("apps/extension/.output/chrome-mv3");
  const browser = await chromium.launchPersistentContext(profile, {
    channel: "chromium",
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
    const popup = await browser.newPage();
    await popup.goto(
      `chrome-extension://${new URL(worker.url()).host}/popup.html`,
    );
    const opened = browser.waitForEvent("page");
    await popup
      .getByRole("button", { name: "Pair extension", exact: true })
      .click();
    const approval = await opened;
    await approval.getByRole("link", { name: "Sign in with Google" }).click();
    await approval
      .getByRole("link", { name: "Test learner A", exact: true })
      .click();
    await approval.getByRole("button", { name: "Approve extension" }).click();
    await expect(popup.getByText("Connected", { exact: true })).toBeVisible();
    await popup
      .getByRole("combobox", { name: "Source language", exact: true })
      .click();
    await popup
      .getByRole("option", { name: "Detect automatically", exact: true })
      .click();
    await popup
      .getByRole("combobox", { name: "Target language", exact: true })
      .click();
    await popup.getByRole("option", { name: "Chinese", exact: true }).click();
    await expect(
      popup.getByLabel("Translate automatically after selecting text", {
        exact: true,
      }),
    ).toBeChecked();
    await popup.getByRole("button", { name: "Save language settings" }).click();
    await expect(popup.getByRole("status")).toHaveText("Updated.");
    await popup
      .getByLabel("Automatically collect selection-translation queries", {
        exact: true,
      })
      .check();
    await popup
      .getByRole("button", { name: "Save collection settings" })
      .click();
    await expect(popup.getByRole("status")).toHaveText("Updated.");
    const token = await worker.evaluate(
      async () =>
        (await (globalThis as any).chrome.storage.local.get("connection"))
          .connection.token,
    );
    const headers = { Authorization: `Bearer ${token}` };
    const sample = await browser.newPage();
    await sample.goto("http://127.0.0.1:3000/sample.html");
    async function select(id: string) {
      await sample.locator("#" + id).evaluate((element) => {
        const range = document.createRange();
        range.selectNodeContents(element);
        const selection = window.getSelection()!;
        selection.removeAllRanges();
        selection.addRange(range);
      });
      await sample.locator("#" + id).dispatchEvent("mouseup");
    }
    await select("sentence");
    // No button click: this must use the saved automatic-translation preference.
    await expect(sample.getByRole("status")).toContainText(/[\u3400-\u9fff]/);
    const first = (
      await (
        await browser.request.get("http://127.0.0.1:4318/v1/lookups", {
          headers,
        })
      ).json()
    ).items[0];
    expect(first).toMatchObject({
      sourceLanguage: "auto",
      targetLanguage: "zh-Hans",
      detectedSourceLanguage: "en",
      translationStatus: "succeeded",
    });
    const library = await browser.newPage();
    await library.goto("http://127.0.0.1:3000/library");
    await expect(library.getByTestId("lookup-card")).toContainText(
      first.meaning,
    );
    await expect(library.getByTestId("candidate-card")).toHaveCount(1);
    await expect(sample.getByRole("status")).toHaveText(first.meaning);
    await expect(
      sample.getByText("AI-generated learning notes", { exact: true }),
    ).toBeVisible();
    await expect(
      sample.getByRole("heading", { name: "Examples", exact: true }),
    ).toBeHidden();
    await sample.getByText("Show explanation", { exact: true }).click();
    await expect(
      sample.getByRole("heading", { name: "Examples", exact: true }),
    ).toBeVisible();
    await expect(
      sample.getByRole("button", { name: "Translate selection", exact: true }),
    ).toBeHidden();
    expect(first.learningDetails).toMatchObject({
      version: 1,
      kind: "sentence",
      partOfSpeech: null,
    });
    expect(first.learningDetails.examples).toHaveLength(1);
    const card = library.getByTestId("lookup-card");
    await expect(card.getByTestId("learning-notes")).toContainText(
      first.learningDetails.definition,
    );
    await expect(card).toContainText(first.learningDetails.examples[0].source);
    await expect(card).toContainText(
      first.learningDetails.examples[0].translation,
    );
    await expect(
      sample.locator("[data-learning-card] .example-translation"),
    ).toHaveText(first.learningDetails.examples[0].translation);
    await sample.screenshot({
      path: "/private/tmp/contextspeak-ai-card.png",
      fullPage: true,
    });
    await select("available");
    await expect(sample.getByRole("status")).toContainText(/[\u3400-\u9fff]/);
    const word = (
      await (
        await browser.request.get("http://127.0.0.1:4318/v1/lookups", {
          headers,
        })
      ).json()
    ).items[0];
    expect(word.text).toBe("available");
    expect(word.learningDetails).toMatchObject({ version: 1, kind: "word" });
    expect(word.learningDetails.examples).toHaveLength(2);
    expect(word.meaning.length).toBeLessThan(80);
    await expect(
      sample.getByRole("button", { name: "Translate selection", exact: true }),
    ).toBeHidden();
    await library.getByRole("button", { name: "Refresh", exact: true }).click();
    await expect(library.getByTestId("lookup-card")).toHaveCount(2);
    const wordCard = library
      .getByTestId("lookup-card")
      .filter({ hasText: "available" })
      .first();
    await expect(wordCard.getByTestId("learning-notes")).toContainText(
      word.learningDetails.examples[1].translation,
    );
    await sample.screenshot({
      path: "/private/tmp/contextspeak-ai-word-card.png",
      fullPage: true,
    });
    await library.screenshot({
      path: "/private/tmp/contextspeak-ai-library.png",
      fullPage: true,
    });
  } finally {
    await browser.close();
    await rm(profile, { recursive: true, force: true });
  }
});
