import { openWorld } from "./helpers";
import { expect, test, type Page, type Route } from "@playwright/test";
import { FixtureSpeechProvider } from "../../../apps/api/src/speech/gemini-speech.provider";
import { normalizeWave } from "../../../packages/world/protocol";

async function command(page: Page, action: string, text = "") {
  await page.evaluate(
    ([action, text]) => {
      const bridge = (window as any).ContextSpeakUnity;
      bridge.instance.SendMessage(
        bridge.owner,
        "OnBrowserCommand",
        JSON.stringify({ action, text }),
      );
    },
    [action, text],
  );
}

test("forest_slice_uses_its_own_controls_and_preserves_world_navigation", async ({
  page,
}) => {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=life");
  await expect(page.locator("#loading")).toHaveText("Ready", {
    timeout: 120000,
  });
  await expect(page.locator("#unity-status")).toContainText('"mode":"life"');
  await page
    .getByRole("button", { name: "Forest café (new)", exact: true })
    .click();
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"');
  await expect(page.locator("#mission")).toBeHidden();
  await expect(page).toHaveTitle("ContextSpeak · Forest Café");
  await expect(
    page.getByRole("link", { name: "ContextSpeak", exact: true }),
  ).toBeVisible();
  await expect(page.locator("#unity-canvas")).toBeVisible();
  await page.locator("#unity-canvas").press("m");
  await expect(page.locator("#unity-status")).toContainText('"panel":"map"');
  await page.locator("#unity-canvas").press("Escape");
  await expect(page.locator("#unity-status")).toContainText('"panel":""');
});

test("forest_slice_persists_settings_across_a_full_browser_reload", async ({
  page,
}) => {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"', {
    timeout: 120000,
  });
  // Use the same validated command bridge as the native settings controls.
  const position = JSON.parse(
    (await page.locator("#unity-status").textContent())!,
  );
  await command(page, "character", "Boy");
  await expect(page.locator("#unity-status")).toContainText(
    '"character":"Boy"',
  );
  await command(page, "character", "99");
  await expect(page.locator("#unity-status")).toContainText(
    '"character":"Boy"',
  );
  const switched = JSON.parse(
    (await page.locator("#unity-status").textContent())!,
  );
  expect(Math.abs(switched.x - position.x)).toBeLessThan(0.1);
  expect(Math.abs(switched.z - position.z)).toBeLessThan(0.1);
  await command(page, "band", "Band7");
  await command(page, "save");
  await expect(page.locator("#unity-status")).toContainText('"band":"Band7"');
  await expect(page.locator("#unity-status")).toContainText(
    '"saveState":"saved"',
  );
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"', {
    timeout: 120000,
  });
  await expect(page.locator("#unity-status")).toContainText('"band":"Band7"');
  await expect(page.locator("#unity-status")).toContainText(
    '"character":"Boy"',
  );
  await command(page, "character", "Girl");
  await expect(page.locator("#unity-status")).toContainText(
    '"character":"Girl"',
  );
});

test("forest_slice_records_corrects_orders_and_delivers_without_double_charging", async ({
  page,
}, info) => {
  let utterance = "";
  let delayedReview: Route | undefined;
  let reviews = 0;
  let npcSpeech = 0;
  const errors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(message.text());
  });
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/speech", (route) => {
    npcSpeech++;
    return route.fulfill({
      contentType: "audio/wav",
      body: Buffer.from(audio.audio, "base64"),
    });
  });
  await page.route("**/slice/review", (route) => {
    if (++reviews === 1) {
      delayedReview = route;
      return;
    }
    return route.fulfill({
      json: {
        correction: "",
        explanation: "",
        lexicalTip: "",
        acknowledgement: "Clear meaning.",
        relevant: true,
      },
    });
  });
  await page.route("**/transcribe", (route) => {
    expect(
      normalizeWave(route.request().postDataBuffer()!).length,
    ).toBeGreaterThan(3244);
    return route.fulfill({ json: { text: utterance } });
  });
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  const snapshot = () => page.locator("#unity-status");
  const state = async () =>
    JSON.parse((await snapshot().textContent()) || "{}");
  await expect(snapshot()).toContainText('"mode":"slice"', { timeout: 120000 });
  await expect(snapshot()).toContainText('"recording":"idle"');
  await command(page, "band", "Band6");
  await page.locator("#unity-canvas").focus();
  await page.keyboard.down("Control");
  await page.keyboard.down("w");
  await expect
    .poll(async () => (await state()).z, { intervals: [50] })
    .toBeLessThan(3.5);
  // Turn in the open aisle, before reaching the bakery counter's collision boundary.
  await page.keyboard.up("w");
  await page.keyboard.down("a");
  await expect
    .poll(async () => (await state()).x, { intervals: [50] })
    .toBeGreaterThan(3.0);
  await page.keyboard.up("a");
  await page.keyboard.down("w");
  await expect
    .poll(async () => (await state()).z, { intervals: [50] })
    .toBeLessThan(-2.4);
  await page.keyboard.up("w");
  await page.keyboard.up("Control");
  // Use the same counter travel offered on the player's map after exercising walking controls.
  await command(page, "visit", "counter");
  await expect.poll(async () => (await state()).x).toBeCloseTo(2.1, 1);
  await expect.poll(async () => (await state()).z).toBeCloseTo(-2.5, 1);
  await page.locator("#unity-canvas").press("f", { delay: 200 });
  await expect(snapshot()).toContainText('"speaker":"Rowan"');
  async function speak(text: string) {
    utterance = text;
    await command(page, "record");
    await expect(snapshot()).toContainText('"recording":"recording"');
    await page.waitForTimeout(300); // capture actual browser audio frames before stopping
    await command(page, "record");
    await expect(snapshot()).toContainText(JSON.stringify(text));
    await expect(snapshot()).toContainText('"busy":false');
  }
  await speak("Can I has a croissant?");
  await expect(snapshot()).toContainText('"gate":"RequireRetry"');
  expect((await state()).servings).toBe(0);
  await command(page, "send", "Can I have a croissant?");
  await command(page, "continue");
  await expect(snapshot()).toContainText('"gate":"RequireRetry"');
  expect((await state()).servings).toBe(0);
  await speak("Can I have a croissant?");
  expect((await state()).servings).toBe(1);
  await expect.poll(() => Boolean(delayedReview)).toBe(true);
  await delayedReview!.fulfill({
    json: {
      relevant: true,
      correction: "Old feedback from a previous reply.",
      explanation: "Stale",
      lexicalTip: "",
      acknowledgement: "OK",
    },
  });
  await speak("That's all, thanks");
  await expect(snapshot()).toContainText('"stage":"Confirming"');
  await speak("Yes, that's everything, thanks");
  await expect(snapshot()).toContainText('"stage":"Paying"');
  await speak("By card, please");
  expect((await state()).receipts).toBe(1);
  expect((await state()).card).toBe(5550);
  await page.screenshot({ path: info.outputPath("forest-cafe-order.png") });
  await expect(snapshot()).toContainText('"order":"Delivered"', {
    timeout: 60000,
  });
  expect((await state()).receipts).toBe(1);
  expect(npcSpeech).toBeGreaterThanOrEqual(6);
  await expect(snapshot()).not.toContainText("Old feedback");
  expect(
    errors.filter((message) =>
      /(?:ArgumentNull|NullReference)Exception/.test(message),
    ),
  ).toEqual([]);
});

test("life_scenes_adapt_to_3_and_9_and_hold_an_incorrect_recording", async ({
  page,
}, info) => {
  let answer = "";
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/speech", (route) =>
    route.fulfill({
      contentType: "audio/wav",
      body: Buffer.from(audio.audio, "base64"),
    }),
  );
  await page.route("**/slice/review", (route) => {
    const request = route.request().postDataJSON();
    expect(request.goal).toBeTruthy();
    if (request.text === "I want tea.")
      expect(request.npcLine).toBe("What would you like to drink?");
    const relevant = request.text !== "My spaceship is purple.";
    return route.fulfill({
      json: {
        correction:
          request.text === "I am want tea."
            ? "I want tea."
            : request.text === "I am study English."
              ? "I study English."
              : "",
        explanation: "",
        lexicalTip: "",
        acknowledgement: "Clear.",
        relevant,
        assessedGoal: true,
        goalMet: relevant && request.text !== "Apples.",
      },
    });
  });
  await page.route("**/transcribe", (route) =>
    route.fulfill({ json: { text: answer } }),
  );
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  const snapshot = page.locator("#unity-status");
  await expect(snapshot).toContainText('"mode":"slice"', { timeout: 120000 });
  await expect(snapshot).toContainText('"recording":"idle"');
  await command(page, "band", "Band3");
  await command(page, "visit", "market");
  await command(page, "talk");
  await expect(snapshot).toContainText('"speaker":"Rose"');
  await expect(snapshot).toContainText("What fruit would you like?");
  async function speak(text: string) {
    answer = text;
    await command(page, "record");
    await expect(snapshot).toContainText('"recording":"recording"');
    if (text === "They are fresh.") {
      await command(page, "retry-lesson", "market");
      await expect(snapshot).toContainText('"lessonStep":1');
    }
    await page.waitForTimeout(300);
    await command(page, "record");
    await expect(snapshot).toContainText(JSON.stringify(text));
    await expect(snapshot).toContainText('"busy":false');
  }
  await speak("My spaceship is purple.");
  await expect(snapshot).toContainText('"lessonStep":0');
  await speak("Some apples, please.");
  await expect(snapshot).toContainText('"lessonStep":1');
  await speak("They are fresh.");
  await expect(snapshot).toContainText('"lessonStep":2');
  await speak("I like carrots too.");
  await expect(snapshot).toContainText('"lessonStep":3');
  await page.screenshot({ path: info.outputPath("market-conversation.png") });
  await command(page, "close");
  await command(page, "visit", "timetable");
  await page.locator("#unity-canvas").press("e");
  await expect(snapshot).toContainText('"panel":"school"');
  await command(page, "visit", "school");
  await command(page, "talk");
  await expect(snapshot).toContainText('"speaker":"Ms Taylor"');
  await speak("I am study English.");
  await expect(snapshot).toContainText('"lessonStep":0');
  await expect(snapshot).toContainText('"gate":"RequireRetry"');
  await speak("I study English.");
  await expect(snapshot).toContainText('"lessonStep":1');
  await speak("Could you explain it again?");
  await expect(snapshot).toContainText('"lessonStep":2');
  await speak("I enjoy working with classmates.");
  await expect(snapshot).toContainText('"lessonStep":3');
  await page.screenshot({ path: info.outputPath("school-conversation.png") });
  await command(page, "close");
  await command(page, "band", "Band9");
  await command(page, "visit", "market");
  await command(page, "talk");
  await expect(snapshot).toContainText("supporting local growers");
  await expect(snapshot).toContainText("counterexample");
  await speak("Apples.");
  await expect(snapshot).toContainText('"lessonStep":0');
  await speak(
    "Local food may be fresher, but imported produce can be cheaper because the climate affects supply.",
  );
  await expect(snapshot).toContainText('"lessonStep":1');
  await command(page, "close");
  await command(page, "band", "Band100");
  await expect(snapshot).toContainText('"band":"Band9"');
  for (const place of [
    "home",
    "cafe",
    "fishing",
    "restaurant",
    "japan",
    "park",
    "mall",
  ]) {
    await command(page, "visit", place);
    await command(page, "talk");
    await expect(snapshot).toContainText(`"lesson":"${place}"`);
    await command(page, "close");
    await page.screenshot({ path: info.outputPath(`${place}.png`) });
  }
  await command(page, "band", "Band3");
  await command(page, "visit", "cafe");
  await command(page, "talk");
  await speak("I am want tea.");
  await expect(snapshot).toContainText('"lessonStep":0');
  await expect(snapshot).toContainText('"gate":"RequireRetry"');
  answer = "I want tea.";
  await command(page, "practice");
  await expect(snapshot).toContainText('"recording":"recording"');
  await page.waitForTimeout(300);
  await command(page, "record");
  await expect(snapshot).toContainText('"busy":false');
  await expect(snapshot).toContainText('"practiceTarget":""');
  await expect(snapshot).toContainText('"lessonStep":1');
  await speak("Hot, please.");
  await expect(snapshot).toContainText('"lessonStep":2');
  await command(page, "close");
  await command(page, "visit", "park:SkateboardBoy");
  await command(page, "talk");
  await expect(snapshot).toContainText('"speaker":"Kai"');
  await expect(snapshot).toContainText('"lessonStep":0');
  await speak("I like the carousel.");
  await expect(snapshot).toContainText('"lessonStep":1');
  await page.screenshot({ path: info.outputPath("kai-resident.png") });
  await command(page, "close");
  await command(page, "visit", "park:SkaterBoy");
  await command(page, "talk");
  await expect(snapshot).toContainText('"speaker":"Jay"');
  await expect(snapshot).toContainText('"lessonStep":0');
  await command(page, "close");
  await command(page, "band", "Band9");
  await command(page, "save");
  await expect(snapshot).toContainText('"saveState":"saved"');
  await page.reload();
  await expect(snapshot).toContainText('"band":"Band9"', { timeout: 120000 });
  await command(page, "band", "Band3");
  await command(page, "visit", "market");
  await command(page, "talk");
  await expect(snapshot).toContainText('"lessonStep":3');
  await command(page, "close");
  await command(page, "visit", "park:SkateboardBoy");
  await command(page, "talk");
  await expect(snapshot).toContainText('"speaker":"Kai"');
  await expect(snapshot).toContainText('"lessonStep":1');
});

test("speaking_trail_plans_from_vocabulary_and_the_lodge_board_works", async ({
  page,
}, info) => {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"', {
    timeout: 120000,
  });
  await command(page, "band", "Band3");
  await command(page, "plan-route", "Ticket, QUEUE");
  await expect(page.locator("#unity-status")).toContainText(
    '"focusVocabulary":"ticket, queue"',
  );
  await expect(page.locator("#unity-status")).toContainText(
    '"trailNext":"park"',
  );
  await command(page, "visit", "lodge");
  await expect
    .poll(
      async () =>
        JSON.parse((await page.locator("#unity-status").textContent())!).x,
    )
    .toBeCloseTo(-19.55, 0);
  await page.locator("#unity-canvas").press("e");
  await expect(page.locator("#unity-status")).toContainText('"panel":"map"');
  await page.screenshot({ path: info.outputPath("speaking-trail.png") });
  await page.locator("#unity-canvas").press("Escape");
  await page.screenshot({ path: info.outputPath("speaking-lodge.png") });
  await command(page, "visit", "park");
  await page.locator("#unity-canvas").press("f");
  await expect(page.locator("#unity-status")).toContainText('"speaker":"Sky"');
  await expect(page.locator("#unity-status")).toContainText(
    "Which ride would you like to try?",
  );
  await command(page, "close");
  await command(page, "save");
  await expect(page.locator("#unity-status")).toContainText(
    '"saveState":"saved"',
  );
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText(
    '"focusVocabulary":"ticket, queue"',
    { timeout: 120000 },
  );
  await expect(page.locator("#unity-status")).toContainText(
    '"trailNext":"park"',
  );
  await command(page, "plan-route", "");
  await command(page, "practice-need", "Dining");
  await expect(page.locator("#unity-status")).toContainText(
    '"practiceNeed":"Dining"',
  );
  await expect(page.locator("#unity-status")).toContainText(
    '"trailNext":"cafe"',
  );
});

test("speaking_trail_replan_survives_an_immediate_reload", async ({ page }) => {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice");
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"', {
    timeout: 120000,
  });
  await command(page, "save");
  await expect(page.locator("#unity-status")).toContainText(
    '"saveState":"saved"',
  );
  await command(page, "plan-route", "latte");
  await expect(page.locator("#unity-status")).toContainText(
    '"focusVocabulary":"latte"',
  );
  await expect(page.locator("#unity-status")).toContainText(
    '"saveState":"saved"',
  );
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText(
    '"focusVocabulary":"latte"',
    { timeout: 120000 },
  );
});
