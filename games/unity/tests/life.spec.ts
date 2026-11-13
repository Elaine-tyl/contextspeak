import { openWorld } from "./helpers";
import { expect, test, type Page } from "@playwright/test";
import { FixtureSpeechProvider } from "../../../apps/api/src/speech/gemini-speech.provider";
import { lifeInput, reviewLife } from "../../../packages/world/life";
import { normalizeWave } from "../../../packages/world/protocol";

async function openTown(page: Page) {
  let utterance = "";
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/speech", (route) =>
    route.fulfill({
      contentType: "audio/wav",
      body: Buffer.from(audio.audio, "base64"),
    }),
  );
  await page.route("**/transcribe", (route) => {
    expect(
      normalizeWave(route.request().postDataBuffer()!).length,
    ).toBeGreaterThan(3244);
    return route.fulfill({ json: { text: utterance } });
  });
  await page.route("**/life/review", (route) => {
    const input = lifeInput.parse(route.request().postDataJSON());
    const repair =
      input.text === "I wants three apples." ? "I want three apples." : "";
    return route.fulfill({
      json: reviewLife(input, {
        accepted: true,
        correction: repair,
        feedback: repair ? "Use want with I." : "Clear meaning and wording.",
        acknowledgement: "Sounds good.",
        choice: input.step === "cafe_order" ? "latte" : "",
        ...(input.step.startsWith("friend_")
          ? {
              checks: [
                { met: true, quote: input.text },
                { met: true, quote: input.text },
              ],
            }
          : {}),
      }),
    });
  });
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=life");
  await expect(page.locator("#unity-status")).toContainText('"mode":"life"', {
    timeout: 120000,
  });
  return async (text: string, waitForReview = true) => {
    utterance = text;
    await page
      .getByRole("button", { name: "Record reply", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Stop recording", exact: true }),
    ).toBeVisible();
    await page.waitForTimeout(250);
    await page
      .getByRole("button", { name: "Stop recording", exact: true })
      .click();
    await expect(page.locator("#transcript")).toContainText(text);
    if (!waitForReview) return;
    await expect(
      page.getByRole("button", { name: "Record reply", exact: true }),
    ).toBeVisible();
    await expect(page.locator("#unity-status")).toContainText('"busy":false');
  };
}
const step = (page: Page, value: number) =>
  expect
    .poll(
      async () =>
        JSON.parse((await page.locator("#unity-status").textContent()) || "{}")
          .step,
    )
    .toBe(value);
async function walkAndTalk(page: Page, name: string) {
  await page
    .getByRole("button", { name: "Walk to objective", exact: true })
    .click();
  await expect(page.locator("#interact")).toContainText(`Talk to ${name}`, {
    timeout: 45000,
  });
  await page.locator("#interact").click();
  await expect(page.locator("#speaker")).toContainText(name);
}

test("life_town_loads_and_keeps_typed_rehearsal_separate_from_spoken_progress", async ({
  page,
}, info) => {
  const speak = await openTown(page);
  await walkAndTalk(page, "Mom");
  const card = await page.locator("#dialogue").boundingBox();
  expect(card!.width).toBeLessThanOrEqual(640);
  expect(card!.height).toBeLessThanOrEqual(405);
  await expect(page.locator("#life-menu")).toContainText("3 apples");
  await page
    .getByLabel("Typed rehearsal", { exact: true })
    .fill("Sure, I'll pick those up.");
  await page
    .getByRole("button", { name: "Check typed rehearsal", exact: true })
    .click();
  await expect(page.locator("#feedback")).toContainText("Typed rehearsal only");
  await step(page, 0);
  await speak("Sure, I'll pick those up.");
  await step(page, 1);
  await page.screenshot({
    path: info.outputPath("life-home.png"),
    fullPage: true,
  });
  await page
    .getByRole("button", { name: "Walk to objective", exact: true })
    .click();
  const before = JSON.parse(
    (await page.locator("#unity-status").textContent())!,
  ).z;
  await page.locator("#unity-canvas").focus();
  await page.keyboard.down("w");
  await expect
    .poll(
      async () =>
        JSON.parse((await page.locator("#unity-status").textContent())!).z,
      { timeout: 2000 },
    )
    .toBeLessThan(before - 0.4);
  await page.keyboard.up("w");
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText('"mode":"life"', {
    timeout: 120000,
  });
  await step(page, 1);
  await page.locator("#stage").selectOption("friend");
  await expect(page.locator("#objective")).toContainText("PRACTICE");
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText('"mode":"life"', {
    timeout: 120000,
  });
  await step(page, 1);
  await expect(page.locator("#objective")).not.toContainText("PRACTICE");
});

test("compact_dialogue_keeps_long_feedback_and_controls_accessible_on_a_small_screen", async ({
  page,
}) => {
  await openTown(page);
  await walkAndTalk(page, "Mom");
  const feedback =
    "Your meaning is clear. Add a polite phrase and confirm the items before leaving for the market. ".repeat(
      2,
    );
  await page.route("**/life/review", (route) =>
    route.fulfill({
      json: {
        accepted: true,
        correction: "",
        feedback,
        acknowledgement: "Thanks!",
        choice: "",
      },
    }),
  );
  await page
    .getByLabel("Typed rehearsal", { exact: true })
    .fill("Sure, I can pick those up.");
  await page
    .getByRole("button", { name: "Check typed rehearsal", exact: true })
    .click();
  await expect(page.locator("#feedback")).toContainText(feedback.trim());
  await page.setViewportSize({ width: 390, height: 844 });
  const card = await page.locator("#dialogue").boundingBox();
  expect(card!.width).toBeLessThanOrEqual(366);
  expect(card!.height).toBeLessThanOrEqual(422);
  await page.locator("#record").scrollIntoViewIfNeeded();
  await expect(page.locator("#record")).toBeInViewport();
  await page
    .getByRole("button", { name: "Close conversation", exact: true })
    .click();
  await expect(page.locator("#dialogue")).toBeHidden();
});

test("life_repairs_require_a_new_valid_recording_before_the_next_question", async ({
  page,
}) => {
  const speak = await openTown(page);
  await page.locator("#stage").selectOption("market");
  await page.locator("#interact").click();
  await speak("I wants three apples.");
  await step(page, 1);
  await expect(page.locator("#repair-note")).toContainText(
    "I want three apples.",
  );
  await expect(
    page.getByRole("button", { name: "Hear correction", exact: true }),
  ).toBeVisible();
  await speak("Could I get three apples, please?");
  await step(page, 2);
  await expect(page.locator("#line")).toContainText("Anything else");
  await expect(page.locator("#repair-note")).toBeHidden();
});

test("life_complete_day_links_walking_menu_purchases_delivery_and_friend_chat", async ({
  page,
}, info) => {
  test.setTimeout(240000);
  const speak = await openTown(page);
  await walkAndTalk(page, "Mom");
  await speak("Sure, I'll pick those up.");
  await walkAndTalk(page, "Sam");
  await expect(page.locator("#life-menu")).toContainText("Apples · $1");
  await speak("Three apples, please.");
  await speak("Two carrots, please.");
  await expect(page.locator("#wallet")).toHaveText("$20.00");
  await speak("I'll pay by card, thanks.");
  await step(page, 4);
  await expect(page.locator("#wallet")).toHaveText("$13.00");
  await expect(page.locator("#bag")).toContainText("3 apples");
  await walkAndTalk(page, "Mom");
  await speak("Here are the apples and carrots for dinner.");
  await expect(page.locator("#bag")).toHaveText("Empty");
  await walkAndTalk(page, "Maya");
  await expect(page.locator("#life-menu")).toContainText("Latte · $6.50");
  await speak("One latte, please.");
  await speak("Iced, please.");
  await speak("For here, please.");
  await speak("Yes, but wait.");
  await step(page, 8);
  await expect(page.locator("#wallet")).toHaveText("$13.00");
  await speak("Yes, go ahead, please.");
  await step(page, 9);
  await expect(page.locator("#wallet")).toHaveText("$6.50");
  await expect(page.locator("#bag")).toContainText("latte");
  await walkAndTalk(page, "Alex");
  await speak("I like tea because it helps me relax.");
  await speak("How about going hiking this weekend?");
  await speak("Want to join me on Saturday at ten?");
  await step(page, 12);
  await expect(page.locator("#objective")).toContainText("First day complete");
  await page.screenshot({
    path: info.outputPath("life-first-day-complete.png"),
    fullPage: true,
  });
});

test("closing_a_pending_life_assessment_cannot_advance_the_task", async ({
  page,
}) => {
  let release: (() => Promise<void>) | undefined;
  const speak = await openTown(page);
  await walkAndTalk(page, "Mom");
  await page.route(
    "**/life/review",
    (route) =>
      new Promise<void>((resolve) => {
        release = async () => {
          await route.fulfill({
            json: {
              accepted: true,
              correction: "",
              feedback: "Clear.",
              acknowledgement: "Sure.",
              choice: "",
            },
          });
          resolve();
        };
      }),
  );
  await speak("Sure, I'll pick those up.", false);
  await expect.poll(() => !!release).toBe(true);
  await page
    .getByRole("button", { name: "Close conversation", exact: true })
    .click();
  await release!();
  await step(page, 0);
});
