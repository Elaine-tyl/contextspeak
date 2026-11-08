import { expect, test, type Page } from "@playwright/test";
import { resolve } from "node:path";
async function voices(page: Page) {
  await page.addInitScript(() => {
    const state = ((window as any).speechFixture = {
      texts: [] as string[],
      names: [] as string[],
      cancels: 0,
      remoteOnly: false,
    });
    Object.defineProperty(window, "SpeechSynthesisUtterance", {
      value: class {
        constructor(public text: string) {}
      },
    });
    const synth = new EventTarget() as any;
    synth.getVoices = () => [
      { name: "Remote", lang: "en-US", localService: false },
      ...(!state.remoteOnly
        ? [{ name: "Local", lang: "en-US", localService: true }]
        : []),
    ];
    synth.speak = (utterance: any) => {
      state.texts.push(utterance.text);
      state.names.push(utterance.voice.name);
      utterance.onstart?.();
    };
    synth.cancel = () => state.cancels++;
    Object.defineProperty(window, "speechSynthesis", { value: synth });
  });
}
test("selection_speaker_uses_detected_language_and_stops_when_closed", async ({
  page,
}) => {
  await voices(page);
  await page.goto("http://127.0.0.1:3000/sample.html");
  await page.evaluate(() => {
    (window as any).browser = {
      runtime: {
        id: "speaker-test",
        sendMessage: async (message: any) =>
          message.type === "translation-options"
            ? {
                ok: true,
                protocolVersion: 4,
                provider: "gemini",
                languages: [{ code: "en", name: "English", targets: ["en"] }],
                preferences: {
                  sourceLanguage: "auto",
                  targetLanguage: "en",
                  autoTranslate: true,
                },
              }
            : {
                ok: true,
                status: "succeeded",
                meaning: "Free to take part.",
                detectedSourceLanguage: "en",
              },
      },
    };
  });
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
  const speaker = page.getByRole("button", {
    name: "Listen to selected text",
    exact: true,
  });
  await expect(speaker).toBeEnabled();
  await page.evaluate(() => {
    (window as any).speechFixture.remoteOnly = true;
    speechSynthesis.dispatchEvent(new Event("voiceschanged"));
  });
  await expect(speaker).toBeDisabled();
  await page.evaluate(() => {
    (window as any).speechFixture.remoteOnly = false;
    speechSynthesis.dispatchEvent(new Event("voiceschanged"));
  });
  await expect(speaker).toBeEnabled();
  await speaker.click();
  expect(
    await page.evaluate(() => (window as any).speechFixture.names),
  ).toEqual(["Local"]);
  expect(
    await page.evaluate(() => (window as any).speechFixture.texts),
  ).toEqual(["available"]);
  await page
    .getByRole("button", { name: "Close translation", exact: true })
    .click();
  expect(
    await page.evaluate(() => (window as any).speechFixture.cancels),
  ).toBeGreaterThan(0);
});
test("vocabulary_title_has_a_local_speaker_and_session_expiry_stops_playback", async ({
  page,
}) => {
  await voices(page);
  let signedIn = true;
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    if (!signedIn) {
      await route.fulfill({ status: 401, json: {} });
      return;
    }
    const data = route.request().url().includes("/auth/session")
      ? { csrfToken: "fixture", identity: { subject: "speaker-a" } }
      : {
          item: {
            id: "item-1",
            text: "academic",
            sourceLanguage: "auto",
            targetLanguage: "zh-Hans",
          },
          lookup: {
            id: "lookup-1",
            text: "academic",
            meaning: "学术的",
            detectedSourceLanguage: "en",
            translationStatus: "succeeded",
          },
        };
    await route.fulfill({ json: data });
  });
  await page.goto("http://127.0.0.1:3000/library/item/item-1");
  const speaker = page.getByRole("button", {
    name: "Listen to selected text",
    exact: true,
  });
  await expect(speaker).toBeEnabled();
  await page.evaluate(() => {
    (window as any).speechFixture.remoteOnly = true;
    speechSynthesis.dispatchEvent(new Event("voiceschanged"));
  });
  await expect(speaker).toBeDisabled();
  await page.evaluate(() => {
    (window as any).speechFixture.remoteOnly = false;
    speechSynthesis.dispatchEvent(new Event("voiceschanged"));
  });
  await expect(speaker).toBeEnabled();
  await speaker.click();
  expect(
    await page.evaluate(() => (window as any).speechFixture.names),
  ).toEqual(["Local"]);
  expect(
    await page.evaluate(() => (window as any).speechFixture.texts),
  ).toEqual(["academic"]);
  signedIn = false;
  await expect(speaker).toHaveCount(0);
  expect(
    await page.evaluate(() => (window as any).speechFixture.cancels),
  ).toBeGreaterThan(0);
});
