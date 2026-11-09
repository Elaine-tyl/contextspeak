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
test("listening_games_persist_distinct_progress_and_unlock_the_next_level", async ({
  page,
}) => {
  await signIn(page, "Test learner A", false);
  await expect(
    page.getByRole("button", { name: "Level 2 · Locked", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: /Hear it, find it/ }).click();
  await expect(
    page.getByRole("button", { name: "Choose umbrella picture", exact: true }),
  ).toBeDisabled();
  await listen(page);
  await page
    .getByRole("button", { name: "Choose umbrella picture", exact: true })
    .click();
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Game score: 100/100", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Back to games", exact: true })
    .click();
  await expect(
    page.getByText("Level 1: 1 different challenges passed", { exact: false }),
  ).toBeVisible();
  await page.getByRole("button", { name: /Listen and answer/ }).click();
  await listen(page);
  await page
    .getByRole("button", { name: "At the station", exact: true })
    .click();
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Game score: 100/100", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Back to games", exact: true })
    .click();
  const catalog = await (
    await page.request.get("http://127.0.0.1:4318/v1/games")
  ).json();
  const passage = catalog.rounds.find(
    (round: any) => round.id === "listen-1-0-v2",
  );
  await page.getByRole("button", { name: /Word dictation/ }).click();
  await listen(page);
  await page.getByLabel("Type what you hear").fill(passage.audioText);
  await expect(page.getByLabel("Highlighted answer")).toHaveCount(0);
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(page.getByLabel("Highlighted answer")).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Game score: 100/100", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Back to games", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 2", exact: true }),
  ).toBeEnabled();
  await page.reload();
  await expect(
    page.getByRole("region", { name: "Practice recommendation" }),
  ).toContainText("Recommended: Level 2");
  await page
    .getByRole("button", { name: "Use recommended level", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 2", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "Level 1", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Level 1", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.getByRole("button", { name: "Level 2", exact: true }),
  ).toBeEnabled();
  await page
    .getByRole("button", { name: "Speaking games", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 2 · Locked", exact: true }),
  ).toBeDisabled();
});
test("speaking_riddle_puzzle_and_simulated_PK_assess_automatically_and_unlock_independently", async ({
  page,
}, testInfo) => {
  await signIn(page, "Test learner B");
  await page
    .getByRole("button", { name: "Speaking games", exact: true })
    .click();
  for (const title of [
    "Speak the riddle",
    "Talk through a puzzle",
    "AI sparring match",
  ]) {
    await page.getByRole("button", { name: new RegExp(title) }).click();
    await expect(
      page.getByRole("button", { name: "Start recording", exact: true }),
    ).toBeDisabled();
    await record(page);
    await expect(
      page.getByRole("heading", { name: "Game score: 80/100", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Get AI feedback", exact: true }),
    ).toHaveCount(0);
    if (title === "AI sparring match") {
      await expect(
        page.getByText("Echo · simulated AI opponent", { exact: false }),
      ).toBeVisible();
      await expect(
        page.getByText("You win · You 80 : Echo 43", { exact: true }),
      ).toBeVisible();
      await page.screenshot({
        path: testInfo.outputPath("game-pk.png"),
        fullPage: true,
      });
    }
    await page
      .getByRole("button", { name: "Back to games", exact: true })
      .click();
  }
  await expect(
    page.getByRole("button", { name: "Level 2", exact: true }),
  ).toBeEnabled();
  await page
    .getByRole("button", { name: "Listening games", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Level 2 · Locked", exact: true }),
  ).toBeDisabled();
});
test("game_retry_keeps_one_recording_and_session_expiry_stops_the_microphone", async ({
  page,
}) => {
  await signIn(page);
  const uploads: any[] = [];
  await page.route("http://127.0.0.1:4318/v1/games/attempts", async (route) => {
    uploads.push(route.request().postDataJSON());
    if (uploads.length === 1) await route.fulfill({ status: 503, json: {} });
    else await route.continue();
  });
  await page
    .getByRole("button", { name: "Speaking games", exact: true })
    .click();
  await page.getByRole("button", { name: /Speak the riddle/ }).click();
  await record(page);
  await expect(
    page.getByRole("button", { name: "Listening games", exact: true }),
  ).toBeDisabled();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await page.getByRole("button", { name: "Retry answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Game score: 80/100", exact: true }),
  ).toBeVisible();
  expect(uploads).toHaveLength(2);
  expect(uploads[0]).toEqual(uploads[1]);
  await page
    .getByRole("button", { name: "Back to games", exact: true })
    .click();
  await page.getByRole("button", { name: /Talk through a puzzle/ }).click();
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

test("brand_is_visible_on_every_route_and_game_has_ten_levels", async ({
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
  await signIn(page, "Test learner B");
  const levels = page.getByRole("button", { name: /^Level \d/ });
  await expect(levels).toHaveCount(10);
  await expect(
    page.getByRole("button", { name: "Level 10 · Locked", exact: true }),
  ).toBeDisabled();
});

test("open_sources_can_be_filtered_practised_and_saved_with_visible_credit", async ({
  page,
}) => {
  await signIn(page);
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("Tatoeba");
  await expect(
    page.getByRole("button", { name: /Hear it, find it/ }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: /help · listening 1/ }).click();
  await expect(
    page.getByRole("link", { name: /Source: Tatoeba/ }),
  ).toHaveAttribute("href", "https://tatoeba.org/en/sentences/show/12335770");
  await expect(
    page.getByRole("link", { name: "CC0-1.0", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Help is available.", { exact: true }),
  ).toHaveCount(0);
  await listen(page);
  await page.getByLabel("Type what you hear").fill("Help is available.");
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Game score: 100/100", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Back to games", exact: true })
    .click();
  await page.getByRole("button", { name: "Level 1", exact: true }).click();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("African Storybook");
  await expect(
    page.getByRole("button", { name: /What are you doing/ }).first(),
  ).toBeVisible();
  await page
    .getByLabel("Content source", { exact: true })
    .selectOption("ContextSpeak");
  await expect(
    page.getByRole("button", { name: /Hear it, find it/ }),
  ).toBeVisible();
});
