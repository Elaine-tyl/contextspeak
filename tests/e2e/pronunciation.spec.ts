import { expect, test } from "@playwright/test";
import { resolve } from "node:path";
import { learningFixture } from "../fixtures/learning";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
test.setTimeout(20000);
test("word_phrase_and_example_speakers_use_AI_audio_and_stop_on_session_expiry", async ({
  page,
}, testInfo) => {
  const wave = await new FixtureSpeechProvider().generate();
  const requested: unknown[] = [];
  let signedIn = true;
  await page.addInitScript(() => {
    const state = ((window as any).speechFixture = { stops: 0, started: 0 });
    Object.defineProperty(window, "Audio", {
      value: class {
        onended: any;
        onerror: any;
        src = "";
        pause() {
          state.stops++;
        }
        removeAttribute() {}
        load() {}
        async play() {
          state.started++;
        }
      },
    });
  });
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    if (!signedIn) {
      await route.fulfill({ status: 401, json: {} });
      return;
    }
    const path = new URL(route.request().url()).pathname;
    let data: unknown;
    if (path === "/auth/session")
      data = { csrfToken: "fixture", identity: { subject: "speaker-a" } };
    else if (path === "/v1/speech") {
      requested.push(route.request().postDataJSON());
      data = wave;
    } else
      data = {
        item: {
          id: "item-1",
          text: "available",
          sourceLanguage: "auto",
          targetLanguage: "zh-Hans",
        },
        lookup: {
          id: "lookup-1",
          text: "available",
          meaning: "有空的",
          detectedSourceLanguage: "en",
          translationStatus: "succeeded",
          learningDetails: learningFixture,
        },
      };
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library/item/item-1");
  const example = page.getByRole("button", {
    name: "Listen to example",
    exact: true,
  });
  await expect(example).toHaveCount(1);
  await page
    .getByTestId("learning-notes")
    .screenshot({ path: testInfo.outputPath("ai-example-speakers.png") });
  await example.click();
  await expect.poll(() => requested.length).toBe(1);
  expect(requested[0]).toEqual({ text: "Are you available?", language: "en" });
  await expect
    .poll(() => page.evaluate(() => (window as any).speechFixture.started))
    .toBe(1);
  await page
    .getByRole("button", { name: "Listen to phrase", exact: true })
    .click();
  await expect.poll(() => requested.length).toBe(2);
  expect(requested[1]).toEqual({ text: "available time", language: "en" });
  await expect
    .poll(() => page.evaluate(() => (window as any).speechFixture.started))
    .toBe(2);
  await page
    .getByRole("button", { name: "Listen to selected text", exact: true })
    .click();
  await expect.poll(() => requested.length).toBe(3);
  expect(requested[2]).toEqual({ text: "available", language: "en" });
  await expect
    .poll(() => page.evaluate(() => (window as any).speechFixture.started))
    .toBe(3);
  const before = await page.evaluate(() => (window as any).speechFixture.stops);
  signedIn = false;
  await expect(example).toHaveCount(0);
  expect(
    await page.evaluate(() => (window as any).speechFixture.stops),
  ).toBeGreaterThan(before);
});
test("selection_examples_request_detected_language_AI_speech_and_cancel_on_close", async ({
  page,
}, testInfo) => {
  await page.goto("http://127.0.0.1:3000/sample.html");
  await page.evaluate((learning) => {
    const state = ((window as any).speechFixture = {
      requested: [] as any[],
      stops: 0,
      finish: undefined as any,
    });
    (window as any).browser = {
      runtime: {
        id: "speaker-test",
        sendMessage: async (message: any) => {
          if (message.type === "speech-start") {
            state.requested.push(message);
            return new Promise((resolve) => {
              state.finish = () => resolve({ ok: true });
            });
          }
          if (message.type === "speech-stop") {
            state.stops++;
            return { ok: true };
          }
          if (message.type === "translation-options")
            return {
              ok: true,
              protocolVersion: 4,
              provider: "gemini",
              languages: [{ code: "en", name: "English", targets: ["en"] }],
              preferences: {
                sourceLanguage: "auto",
                targetLanguage: "en",
                autoTranslate: true,
              },
            };
          return {
            ok: true,
            status: "succeeded",
            meaning: "Free to take part.",
            detectedSourceLanguage: "en",
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
  await page.locator("#available").evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element);
    const selection = getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
    element.dispatchEvent(new MouseEvent("mouseup", { bubbles: true }));
  });
  await page.getByText("Show explanation", { exact: true }).click();
  await page
    .locator("[data-contextspeak=panel]")
    .screenshot({ path: testInfo.outputPath("ai-card-speakers.png") });
  await page
    .getByRole("button", { name: "Listen to example", exact: true })
    .click();
  expect(
    await page.evaluate(() => (window as any).speechFixture.requested[0]),
  ).toMatchObject({ text: "Are you available?", language: "en" });
  await page
    .getByRole("button", { name: "Close translation", exact: true })
    .click();
  expect(
    await page.evaluate(() => (window as any).speechFixture.stops),
  ).toBeGreaterThan(0);
  await page.evaluate(() => (window as any).speechFixture.finish());
  await expect(page.locator("[data-contextspeak=panel]")).toHaveCount(0);
  await page.locator("#available").evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element);
    const selection = getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
    element.dispatchEvent(new MouseEvent("mouseup", { bubbles: true }));
  });
  await page.getByText("Show explanation", { exact: true }).click();
  await page
    .getByRole("button", { name: "Listen to example", exact: true })
    .click();
  const stops = await page.evaluate(() => (window as any).speechFixture.stops);
  await page.evaluate(() => window.dispatchEvent(new Event("pagehide")));
  await expect(page.locator("[data-contextspeak=panel]")).toHaveCount(0);
  expect(
    await page.evaluate(() => (window as any).speechFixture.stops),
  ).toBeGreaterThan(stops);
});
