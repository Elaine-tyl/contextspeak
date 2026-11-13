import { expect, test, type Page } from "@playwright/test";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
import { gapFillChallenges } from "../../apps/api/src/games/gap-fill";
import { installAudioFixtures } from "../helpers/practice-audio";
async function signIn(
  page: Page,
  learner = "Test learner A",
  mockVoice = true,
) {
  await installAudioFixtures(page);
  if (mockVoice) {
    const audio = await new FixtureSpeechProvider().generate();
    await page.route("http://127.0.0.1:4318/v1/speech", (route) =>
      route.fulfill({ json: audio }),
    );
  }
  await page.goto("http://127.0.0.1:3000/games/web");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: learner, exact: true }).click();
  await expect(page).toHaveURL("http://127.0.0.1:3000/games/web");
  await expect(
    page.getByRole("button", { name: "Listening games", exact: true }),
  ).toBeVisible();
}
async function listen(page: Page) {
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
}
async function record(page: Page) {
  await listen(page);
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
}
async function choose(page: Page, name: string) {
  await page.getByRole("button", { name, exact: true }).click();
  await page.getByRole("button", { name: "Start level", exact: true }).click();
}
async function currentRun(page: Page) {
  await expect(page).toHaveURL(/run=[a-f0-9-]+/);
  const id = new URL(page.url()).searchParams.get("run");
  return (
    await page.request.get(`http://127.0.0.1:4318/v1/games/levels/${id}`)
  ).json();
}

test("new listening games submit tile and sound answers for server-owned feedback", async ({
  page,
}) => {
  await signIn(page);
  const modes = [
    { label: "Spelling bee", parts: ["c", "a", "t"] },
    { label: "Sentence builder", parts: ["I", "like", "green", "tea"] },
    { label: "Sound detective", answer: "sheep" },
    { label: "Quick fire", answer: "True" },
  ];
  for (const mode of modes) {
    await page.goto("http://127.0.0.1:3000/games/web");
    await choose(page, mode.label);
    if (mode.label === "Quick fire")
      await expect(
        page.getByRole("img", { name: "Cat", exact: true }),
      ).toBeVisible();
    await listen(page);
    if (mode.parts) {
      for (const part of mode.parts)
        await page
          .getByRole("button", { name: `Add ${part}`, exact: true })
          .click();
    } else {
      await page
        .getByRole("button", { name: mode.answer, exact: true })
        .click();
    }
    await page
      .getByRole("button", { name: "Check answer", exact: true })
      .click();
    await expect(
      page.getByRole("heading", {
        name: "Answer checked",
        exact: true,
      }),
    ).toBeVisible();
    const current = await currentRun(page);
    expect(current.receipts).toHaveLength(1);
    expect(current.receipts[0].score).toBe(100);
  }
});

async function finishDictation(page: Page, response?: string) {
  const run = await currentRun(page);
  for (let index = run.receipts.length; index < run.rounds.length; index++) {
    await listen(page);
    await page
      .getByLabel("Type what you hear")
      .fill(response ?? run.rounds[index].audioText);
    await page
      .getByRole("button", { name: "Check answer", exact: true })
      .click();
    if (index < run.rounds.length - 1) {
      await expect(
        page.getByRole("heading", { name: "Answer checked", exact: true }),
      ).toBeVisible();
      await page
        .getByRole("button", { name: "Next question", exact: true })
        .click();
    }
  }
  await expect(
    page.getByRole("region", { name: "Level result", exact: true }),
  ).toBeVisible();
}
test("modes_have_themes_ten_levels_scored_runs_retry_next_and_separate_unlocks", async ({
  page,
}) => {
  await signIn(page);
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await expect(page.getByRole("button", { name: /^Level \d/ })).toHaveCount(10);
  await expect(
    page.getByRole("button", { name: "Level 2 · Locked", exact: true }),
  ).toBeDisabled();
  await expect(page.getByLabel("Theme", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await currentRun(page);
  const firstId = new URL(page.url()).searchParams.get("run");
  await finishDictation(page);
  await expect(
    page.getByRole("heading", { name: "Level score: 100/100", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "All games", exact: true }),
  ).toBeEnabled();
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("run"))
    .not.toBe(firstId);
  expect(
    (
      await (
        await page.request.get(
          `http://127.0.0.1:4318/v1/games/levels/${firstId}`,
        )
      ).json()
    ).score,
  ).toBe(100);
  await page.reload();
  await expect(
    page.getByText("Level 1 · Question 1 of 3", { exact: true }),
  ).toBeVisible();
  await finishDictation(page);
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(
    page.getByText("Level 2 · Question 1 of 3", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await page
    .getByRole("button", { name: "Picture choice", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 2 · Locked", exact: true }),
  ).toBeDisabled();
});
test("picture_and_listening_question_levels_score_after_completion", async ({
  page,
}) => {
  await signIn(page, "Test learner B", false);
  await choose(page, "Picture choice");
  await expect(
    page.getByRole("button", { name: "Choose umbrella picture", exact: true }),
  ).toBeDisabled();
  // Level 1 uses its own four-picture theme and three rounds.
  for (const [index, picture] of ["umbrella", "bicycle", "book"].entries()) {
    if (index)
      await page
        .getByRole("button", { name: "Next question", exact: true })
        .click();
    await listen(page);
    await page
      .getByRole("button", { name: `Choose ${picture} picture`, exact: true })
      .click();
    await page
      .getByRole("button", { name: "Check answer", exact: true })
      .click();
  }
  await expect(
    page.getByRole("heading", { name: "Level score: 100/100", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await choose(page, "Listening questions");
  await listen(page);
  await page
    .getByRole("button", {
      name: "Residents needed more time to review it",
      exact: true,
    })
    .click();
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Level score: 100/100", exact: true }),
  ).toBeVisible();
});
test("speaking_modes_assess_automatically_and_finish_with_retry_and_next", async ({
  page,
}, testInfo) => {
  await signIn(page, "Test learner B");
  await page
    .getByRole("button", { name: "Speaking games", exact: true })
    .click();
  for (const name of ["Speaking questions", "Riddles", "Puzzles", "AI PK"]) {
    await choose(page, name);
    const run = await currentRun(page);
    for (let index = 0; index < run.rounds.length; index++) {
      await record(page);
      if (index < run.rounds.length - 1)
        await page
          .getByRole("button", { name: "Next question", exact: true })
          .click();
    }
    await expect(
      page.getByRole("heading", { name: "Level score: 80/100", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Retry", exact: true }),
    ).toBeEnabled();
    await expect(
      page.getByRole("button", { name: "Next", exact: true }),
    ).toBeEnabled();
    await page.screenshot({
      path: testInfo.outputPath(`${name.replaceAll(" ", "-")}-level.png`),
      fullPage: true,
    });
    await page.getByRole("button", { name: "All games", exact: true }).click();
  }
});
test("pending_answers_reuse_the_same_upload_and_expiry_stops_the_microphone", async ({
  page,
}) => {
  await signIn(page);
  const uploads: object[] = [];
  await page.route("http://127.0.0.1:4318/v1/games/attempts", async (route) => {
    uploads.push(route.request().postDataJSON());
    if (uploads.length === 1) await route.fulfill({ status: 503, json: {} });
    else await route.continue();
  });
  await page
    .getByRole("button", { name: "Speaking games", exact: true })
    .click();
  await choose(page, "Riddles");
  await record(page);
  await expect(
    page.getByRole("button", { name: "All games", exact: true }),
  ).toBeDisabled();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await page.getByRole("button", { name: "Retry answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Level score: 80/100", exact: true }),
  ).toBeVisible();
  expect(uploads).toHaveLength(2);
  expect(uploads[0]).toEqual(uploads[1]);
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await choose(page, "Puzzles");
  await listen(page);
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  const stops = await page.evaluate(() => (window as any).audioFixture.stops);
  await page.route("http://127.0.0.1:4318/auth/session", (route) =>
    route.fulfill({ status: 401, json: {} }),
  );
  await expect(
    page.getByRole("link", { name: "Sign in with Google", exact: true }),
  ).toBeVisible();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.stops))
    .toBeGreaterThan(stops);
  expect(uploads).toHaveLength(2);
});
test("credited_sources_filter_levels_low_scores_require_retry_and_stale_filters_recover", async ({
  page,
}) => {
  await signIn(page);
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("VOA Learning English");
  await page
    .getByLabel("Theme", { exact: true })
    .selectOption("News · education");
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await expect(
    page.getByRole("link", { name: /Source: VOA Learning English/ }),
  ).toHaveAttribute(
    "href",
    "https://learningenglish.voanews.com/a/how-international-students-should-think-about-writing-ai/7498850.html",
  );
  await expect(
    page.getByText(
      "Students must learn academic writing and research methods when moving from high school to college.",
      { exact: true },
    ),
  ).toHaveCount(0);
  await finishDictation(page, "wrong answer");
  await expect(
    page.getByRole("heading", { name: "Level score: 0/100", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Next", exact: true }),
  ).toBeDisabled();
  await page.getByText("Review answers", { exact: true }).click();
  await expect(
    page
      .getByRole("region", { name: "Level result", exact: true })
      .locator(".game-source-credit"),
  ).toContainText("Published 2024-02-24 · Archive feature");
  const previousRun = new URL(page.url()).searchParams.get("run");
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("run"))
    .not.toBe(previousRun);
  await finishDictation(page);
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(
    page.getByText("Level 2 · Question 1 of 3", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("ContextSpeak");
  await page.getByRole("button", { name: "Level 2", exact: true }).click();
  await page.getByLabel("Theme", { exact: true }).selectOption("News · work");
  await expect(page.getByLabel("Content source", { exact: true })).toHaveValue(
    "all",
  );
  await expect(
    page.getByText(
      "No matching questions. Choose another theme or All sources.",
      { exact: true },
    ),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Start level", exact: true }),
  ).toBeEnabled();
});
test("brand_is_visible_on_every_route_and_modes_remain_visible", async ({
  page,
}) => {
  for (const route of [
    "/",
    "/library",
    "/pair",
    "/games",
    "/library/item/unknown",
    "/practice/unknown",
  ]) {
    await page.goto(`http://127.0.0.1:3000${route}`);
    await expect(
      page
        .getByRole("navigation", { name: "Main" })
        .getByRole("link", { name: "ContextSpeak home" }),
    ).toBeVisible();
  }
  await signIn(page);
  await expect(
    page.getByRole("button", { name: "Dictation", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Picture choice", exact: true }),
  ).toBeVisible();
});

async function fillGaps(page: Page, words: string[]) {
  for (const [index, word] of words.entries())
    await page
      .getByLabel(`Missing word ${index + 1}`, { exact: true })
      .fill(word);
}

async function finishMissingWords(page: Page) {
  for (const [index, words] of [
    ["ingredients", "allergens"],
    ["availability", "reservation"],
    ["departure", "destination"],
  ].entries()) {
    await listen(page);
    await fillGaps(page, words);
    await page
      .getByRole("button", { name: "Check answer", exact: true })
      .click();
    if (index < 2)
      await page
        .getByRole("button", { name: "Next question", exact: true })
        .click();
  }
}

test("missing_words_keep_answers_after_a_lost_response_and_support_retry_reload_and_next", async ({
  page,
}) => {
  await signIn(page);
  const previous = await (
    await page.request.get("http://127.0.0.1:4318/v1/games")
  ).json();
  await choose(page, "Missing words");
  await currentRun(page);
  const firstId = new URL(page.url()).searchParams.get("run");
  const input = page.getByLabel("Missing word 1", { exact: true });
  const check = page.getByRole("button", { name: "Check answer", exact: true });
  await expect(input).toBeDisabled();
  await expect(check).toBeDisabled();
  await expect(
    page.getByText(
      "Could you confirm the ingredients in this sandwich and check whether it contains any allergens?",
      {
        exact: true,
      },
    ),
  ).toHaveCount(0);
  await listen(page);
  await expect(check).toBeDisabled();
  await input.fill("ingredents");
  await expect(check).toBeDisabled();
  await page.getByLabel("Missing word 2", { exact: true }).fill("allergins");
  const submissions: object[] = [];
  await page.route("http://127.0.0.1:4318/v1/games/attempts", async (route) => {
    submissions.push(route.request().postDataJSON());
    const saved = await route.fetch();
    if (submissions.length === 1) await route.abort("failed");
    else await route.fulfill({ response: saved });
  });
  await check.click();
  await expect(
    page.getByRole("button", { name: "Retry answer", exact: true }),
  ).toBeVisible();
  await expect(input).toBeDisabled();
  await page.getByRole("button", { name: "Retry answer", exact: true }).click();
  await expect(
    page.getByRole("region", { name: "Missing words feedback" }),
  ).toContainText("ingredents → ingredients");
  expect(submissions).toHaveLength(2);
  expect(submissions[0]).toEqual(submissions[1]);
  await page.unroute("http://127.0.0.1:4318/v1/games/attempts");
  let releaseNext!: () => void;
  const nextRead = new Promise<void>((resolve) => {
    releaseNext = resolve;
  });
  const runUrl = `http://127.0.0.1:4318/v1/games/levels/${firstId}`;
  await page.route(runUrl, async (route) => {
    await nextRead;
    await route.continue();
  });
  await page
    .getByRole("button", { name: "Next question", exact: true })
    .click();
  try {
    await expect(
      page.getByRole("button", { name: "Play challenge", exact: true }),
    ).toBeDisabled();
  } finally {
    releaseNext();
  }
  await expect(
    page.getByText("Level 1 · Question 2 of 3", { exact: true }),
  ).toBeVisible();
  await page.unroute(runUrl);
  await page.reload();
  await expect(
    page.getByText("Level 1 · Question 2 of 3", { exact: true }),
  ).toBeVisible();
  for (const [index, words] of [
    ["availability", "reservation"],
    ["departure", "destination"],
  ].entries()) {
    await listen(page);
    await fillGaps(page, words);
    await check.click();
    if (index === 0)
      await page
        .getByRole("button", { name: "Next question", exact: true })
        .click();
  }
  await expect(
    page.getByRole("heading", { name: "Level score: 67/100", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Next", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("run"))
    .not.toBe(firstId);
  await finishMissingWords(page);
  await expect(
    page.getByRole("heading", { name: "Level score: 100/100", exact: true }),
  ).toBeVisible();
  await page.reload();
  await page.getByText("Review answers", { exact: true }).click();
  await expect(
    page.getByRole("region", { name: "Missing words feedback" }),
  ).toHaveCount(3);
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(
    page.getByText("Level 2 · Question 1 of 3", { exact: true }),
  ).toBeVisible();
  const updated = await (
    await page.request.get("http://127.0.0.1:4318/v1/games")
  ).json();
  expect(updated.modeProgress.dictation).toEqual(
    previous.modeProgress.dictation,
  );
});

test("multiple_missing_words_show_per_blank_corrections_without_overflow_on_mobile", async ({
  page,
}, testInfo) => {
  await signIn(page);
  const session = await (
    await page.request.get("http://127.0.0.1:4318/auth/session")
  ).json();
  const headers = {
    Origin: "http://127.0.0.1:3000",
    "X-CSRF-Token": session.csrfToken,
  };
  // Seed prerequisite levels through the real API; the exercise still runs in the UI.
  for (const [index, words] of [
    [
      ["ingredients", "allergens"],
      ["availability", "reservation"],
      ["departure", "destination"],
    ],
    [
      ["substitution", "surcharge"],
      ["postpone", "Wednesday"],
      ["refund", "cancellation"],
    ],
    [
      ["recommend", "vegetarian", "seasonal"],
      ["reschedule", "circulate", "prepare"],
      ["retain", "operator", "compensation"],
    ],
  ].entries()) {
    const started = await page.request.post(
      "http://127.0.0.1:4318/v1/games/levels",
      {
        headers,
        data: {
          eventId: crypto.randomUUID(),
          kind: "gap-fill",
          level: index + 1,
        },
      },
    );
    expect(started.ok()).toBe(true);
    const run = await started.json();
    for (const [question, answers] of words.entries()) {
      const result = await page.request.post(
        "http://127.0.0.1:4318/v1/games/attempts",
        {
          headers,
          data: {
            eventId: crypto.randomUUID(),
            levelSessionId: run.id,
            roundId: run.rounds[question].id,
            completedPlays: 1,
            gapAnswers: answers,
          },
        },
      );
      expect(result.ok()).toBe(true);
    }
  }
  await page.reload();
  await page.setViewportSize({ width: 390, height: 844 });
  await page
    .getByRole("button", { name: "Missing words", exact: true })
    .click();
  await page.getByRole("button", { name: "Level 4", exact: true }).click();
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await listen(page);
  await fillGaps(page, ["dietary", "REQUIREMENTS", "contaminaton"]);
  await page.screenshot({
    path: testInfo.outputPath("missing-words-mobile.png"),
    fullPage: true,
  });
  await page.getByLabel("Missing word 3", { exact: true }).press("Enter");
  const feedback = page.getByRole("region", { name: "Missing words feedback" });
  await expect(feedback).toContainText("Your result: 67/100");
  await expect(feedback).toContainText("Correct: requirements");
  await expect(feedback.locator("mark")).toHaveText(
    "contaminaton → contamination",
  );
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("missing-words-feedback.png"),
    fullPage: true,
  });
});

test("academic_six_blank_mobile_rounds_and_credited_news_are_available_with_official_resources", async ({
  page,
}, testInfo) => {
  await signIn(page, "Test learner B");
  await page
    .getByRole("button", { name: "Missing words", exact: true })
    .click();
  await page.getByText("Difficulty and resources", { exact: true }).click();
  await page.getByText("Free news and IELTS practice", { exact: true }).click();
  await expect(
    page.getByRole("link", {
      name: "IELTS · official sample questions",
      exact: true,
    }),
  ).toHaveAttribute(
    "href",
    "https://ielts.org/take-a-test/preparation-resources/sample-test-questions",
  );
  await expect(
    page.getByText("Game levels are not IELTS band scores.", { exact: false }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", {
      name: "BBC Learning English · news vocabulary",
      exact: true,
    }),
  ).toHaveAttribute(
    "href",
    "https://downloads.bbc.co.uk/learningenglish/features/LEFTN/250827_LEFTN_heatwaves_speed_up_ageing_process_study_says_worksheet.pdf",
  );
  await expect(
    page.getByText(
      "Levels 1–3: Intermediate · 4–7: Upper-intermediate · 8–10: Advanced",
      { exact: true },
    ),
  ).toBeVisible();
  await page
    .getByLabel("Theme", { exact: true })
    .selectOption("News · education");
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("VOA Learning English");
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await expect(page.locator(".game-source-credit")).toContainText(
    "Dan Friedell · Published 2024-02-24 · Archive feature",
  );
  await expect(page.locator(".game-source-credit a").first()).toHaveAttribute(
    "href",
    /7498850/,
  );
  await listen(page);
  await fillGaps(page, ["academic", "research"]);
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Level score: 100/100", exact: true }),
  ).toBeVisible();
  const session = await (
    await page.request.get("http://127.0.0.1:4318/auth/session")
  ).json();
  const headers = {
    Origin: "http://127.0.0.1:3000",
    "X-CSRF-Token": session.csrfToken,
  };
  // Unlock prerequisites through real requests; only the final UI answer is under test.
  for (let level = 1; level <= 9; level++) {
    const started = await page.request.post(
      "http://127.0.0.1:4318/v1/games/levels",
      {
        headers,
        data: {
          eventId: crypto.randomUUID(),
          kind: "gap-fill",
          level,
          topic: "Café",
          source: "ContextSpeak",
        },
      },
    );
    expect(started.ok()).toBe(true);
    const run = await started.json();
    const expected = gapFillChallenges.find(
      (round) => round.id === run.rounds[0].id,
    )!;
    const checked = await page.request.post(
      "http://127.0.0.1:4318/v1/games/attempts",
      {
        headers,
        data: {
          eventId: crypto.randomUUID(),
          levelSessionId: run.id,
          roundId: expected.id,
          completedPlays: 1,
          gapAnswers: expected.gapAnswers,
        },
      },
    );
    expect(checked.ok()).toBe(true);
  }
  await page.goto("http://127.0.0.1:3000/games/web?mode=gap-fill");
  await page.getByRole("button", { name: "Level 2", exact: true }).click();
  for (const theme of [
    "News · technology",
    "News · work",
    "News · environment",
  ]) {
    await page.getByLabel("Theme", { exact: true }).selectOption(theme);
    await expect(
      page.getByRole("button", { name: "Start level", exact: true }),
    ).toBeEnabled();
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByRole("button", { name: "Level 10", exact: true }).click();
  await page
    .getByLabel("Theme", { exact: true })
    .selectOption("Academic study");
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("ContextSpeak");
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await listen(page);
  await expect(page.getByRole("textbox")).toHaveCount(6);
  await fillGaps(page, [
    "methodological",
    "confounding",
    "generalizability",
    "replication",
    "corroborating",
    "tentativ",
  ]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  // The native input is intentionally hidden; visible letter boxes must not clip glyphs.
  for (const cell of await page.locator(".letter-cell").all())
    expect(
      await cell.evaluate(
        (element) => element.scrollWidth <= element.clientWidth,
      ),
    ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("academic-six-blanks.png"),
    fullPage: true,
  });
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Level score: 83/100", exact: true }),
  ).toBeVisible();
  await page.getByText("Review answers", { exact: true }).click();
  await expect(
    page
      .getByRole("region", { name: "Missing words feedback" })
      .locator("mark"),
  ).toHaveText("tentativ → tentative");
});

test("all_modes_share_difficulty_and_old_foundation_results_retry_current_content", async ({
  page,
}) => {
  await signIn(page);
  for (const [track, modes] of [
    ["Listening games", ["Dictation", "Picture choice", "Listening questions"]],
    ["Speaking games", ["Speaking questions", "Riddles", "Puzzles", "AI PK"]],
  ] as const) {
    await page.getByRole("button", { name: track, exact: true }).click();
    for (const mode of modes) {
      await page.getByRole("button", { name: mode, exact: true }).click();
      await page.getByText("Difficulty and resources", { exact: true }).click();
      await expect(
        page.getByText(
          "Levels 1–3: Intermediate · 4–7: Upper-intermediate · 8–10: Advanced",
          { exact: true },
        ),
      ).toBeVisible();
      await page
        .getByRole("button", { name: "All games", exact: true })
        .click();
    }
  }
  const id = crypto.randomUUID();
  const old = {
    id: "open-tatoeba-12335770-listen-v1",
    track: "listening",
    kind: "dictation",
    level: 1,
    title: "Asking for help · listening",
    topic: "Asking for help",
    archived: true,
    instruction: "Listen, then type exactly what you hear.",
    audioText: "Help is available.",
    source: {
      id: "tatoeba-12335770",
      provider: "Tatoeba",
      title: "Help is available.",
      author: "Miktsoanit",
      url: "https://tatoeba.org/en/sentences/show/12335770",
      license: "CC0-1.0",
      licenseUrl: "https://creativecommons.org/publicdomain/zero/1.0/",
    },
  };
  // UI fixture models an owned historical result; the Retry creates a real current run.
  await page.route(`http://127.0.0.1:4318/v1/games/levels/${id}`, (route) =>
    route.fulfill({
      json: {
        id,
        kind: "dictation",
        level: 1,
        source: "Tatoeba",
        topic: "Asking for help",
        rounds: [old],
        receipts: [
          {
            id: crypto.randomUUID(),
            roundId: old.id,
            score: 100,
            createdAt: "2026-11-11T00:00:00Z",
            feedback: {
              correctAnswer: "Help is available.",
              explanation: "Saved original answer.",
            },
          },
        ],
        completed: true,
        score: 100,
        passed: true,
        nextLevel: 2,
      },
    }),
  );
  await page.goto(`http://127.0.0.1:3000/games/web?run=${id}`);
  await expect(page.getByText(/Earlier edition/)).toBeVisible();
  await page.getByText("Review answers", { exact: true }).click();
  await expect(
    page.getByRole("link", { name: /Source: Tatoeba/ }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("run"))
    .not.toBe(id);
  const current = await currentRun(page);
  expect(current.source).toBeNull();
  expect(current.topic).toBeNull();
  expect(
    current.rounds.every((round: { id: string }) =>
      round.id.startsWith("game-dictation-1-"),
    ),
  ).toBe(true);
});
