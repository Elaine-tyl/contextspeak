import { expect, test, type Page } from "@playwright/test";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
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
  await page.goto("http://127.0.0.1:3000/games");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: learner, exact: true }).click();
  await expect(page).toHaveURL("http://127.0.0.1:3000/games");
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
  await listen(page);
  await page
    .getByRole("button", { name: "Choose umbrella picture", exact: true })
    .click();
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Level score: 100/100", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await choose(page, "Listening questions");
  await listen(page);
  await page
    .getByRole("button", { name: "At the station", exact: true })
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
test("credited_sources_and_themes_filter_a_level_and_low_scores_require_retry", async ({
  page,
}) => {
  await signIn(page);
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("Tatoeba");
  await page
    .getByLabel("Theme", { exact: true })
    .selectOption("Asking for help");
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await expect(
    page.getByRole("link", { name: /Source: Tatoeba/ }),
  ).toHaveAttribute("href", "https://tatoeba.org/en/sentences/show/12335770");
  await expect(
    page.getByText("Help is available.", { exact: true }),
  ).toHaveCount(0);
  await finishDictation(page, "wrong answer");
  await expect(
    page.getByRole("heading", { name: "Level score: 0/100", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Next", exact: true }),
  ).toBeDisabled();
  const previousRun = new URL(page.url()).searchParams.get("run");
  await page.getByRole("button", { name: "Retry", exact: true }).click();
  await expect
    .poll(() => new URL(page.url()).searchParams.get("run"))
    .not.toBe(previousRun);
  await finishDictation(page);
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await expect(
    page.getByText("Level 2 · Question 1 of 2", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "All games", exact: true }).click();
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("African Storybook");
  await page.getByRole("button", { name: "Level 2", exact: true }).click();
  await expect(page.getByLabel("Content source", { exact: true })).toHaveValue(
    "African Storybook",
  );
  await expect(
    page.getByText(
      "No matching questions. Choose another theme or All sources.",
      { exact: true },
    ),
  ).toBeVisible();
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
      page.getByRole("banner").getByRole("link", { name: "ContextSpeak home" }),
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
