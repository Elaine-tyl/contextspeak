import { existsSync } from "node:fs";
import { expect, test, type Page } from "@playwright/test";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
import { gapFillChallenges } from "../../apps/api/src/games/gap-fill";
import { installAudioFixtures } from "../helpers/practice-audio";

async function signIn(page: Page) {
  await installAudioFixtures(page);
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("http://127.0.0.1:4318/v1/speech", (route) =>
    route.fulfill({ json: audio }),
  );
  await page.goto("http://127.0.0.1:3000/games");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  // A unique test-only identity keeps unlocks independent of the existing web suite.
  const identity = new URL(
    (await page
      .getByRole("link", { name: "Test learner A", exact: true })
      .getAttribute("href"))!,
  );
  identity.searchParams.set("test_user", "arcade-" + crypto.randomUUID());
  await page.goto(identity.href);
  await expect(page).toHaveURL("http://127.0.0.1:3000/games");
}
async function engine(page: Page) {
  const frame = page.frameLocator('iframe[title="ContextSpeak Godot Arcade"]');
  await expect(frame.locator("#arcade-status")).toContainText(
    '"phase":"answer"',
    { timeout: 60000 },
  );
  return frame;
}
async function run(page: Page) {
  await expect(page).toHaveURL(/run=[a-f0-9-]+/);
  const id = new URL(page.url()).searchParams.get("run");
  return (
    await page.request.get(`http://127.0.0.1:4318/v1/games/levels/${id}`)
  ).json();
}
test.describe("Exported Godot board", () => {
  test.skip(
    !existsSync("apps/web/public/arcade-engine/index.html"),
    "Run pnpm arcade:build to test the optional Godot Web export.",
  );
  test("godot_plays_a_real_account_owned_round", async ({ page }) => {
    await signIn(page);
    await page.getByRole("button", { name: "Dictation", exact: true }).click();
    await page
      .getByRole("button", { name: "Start level", exact: true })
      .click();
    const initial = await run(page);
    const frame = await engine(page);
    const canvas = frame.locator("#canvas");
    await canvas.focus();
    await canvas.press("Enter");
    await expect(frame.locator("#arcade-status")).toContainText(
      '"played":true',
    );
    await page.keyboard.press("Tab");
    await page.keyboard.type(initial.rounds[0].audioText);
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    await page.screenshot({
      path: "/private/tmp/arcade-typed.png",
      fullPage: true,
    });
    await expect(
      page.getByRole("heading", {
        name: "Answer checked: 100/100",
        exact: true,
      }),
    ).toBeVisible();
    await expect(frame.locator("#arcade-status")).toContainText(
      '"phase":"result"',
    );
    await canvas.press("Enter");
    await expect(
      page.getByText("Level 1 · Question 2 of 3", { exact: true }),
    ).toBeVisible();
    await page.reload();
    await expect(
      page.getByText("Level 1 · Question 2 of 3", { exact: true }),
    ).toBeVisible();
    expect((await run(page)).receipts).toHaveLength(1);
    await engine(page);
    await page.setViewportSize({ width: 390, height: 844 });
    await expect
      .poll(
        async () =>
          JSON.parse((await frame.locator("#arcade-status").textContent())!)
            .width,
      )
      .toBeLessThan(600);
    await page.screenshot({
      path: "/private/tmp/contextspeak-arcade-first.png",
      fullPage: true,
    });
  });

  async function startMode(page: Page, mode: string) {
    await page.goto("http://127.0.0.1:3000/games?mode=" + mode);
    await page
      .getByRole("button", { name: "Start level", exact: true })
      .click();
    const current = await run(page);
    const frame = await engine(page);
    await frame.locator("#canvas").focus();
    return { current, frame };
  }
  async function listen(page: Page, frame: ReturnType<Page["frameLocator"]>) {
    await page.keyboard.press("Enter");
    await expect(frame.locator("#arcade-status")).toContainText(
      '"played":true',
    );
  }

  test("native_missing_words_and_choices_keep_the_server_scoring", async ({
    page,
  }) => {
    await signIn(page);
    const { current, frame } = await startMode(page, "gap-fill");
    await listen(page, frame);
    const task = gapFillChallenges.find(
      (item) => item.id === current.rounds[0].id,
    )!;
    for (const [index, answer] of task.gapAnswers!.entries()) {
      await page.keyboard.press("Tab");
      await expect
        .poll(
          async () =>
            JSON.parse((await frame.locator("#arcade-status").textContent())!)
              .focus,
        )
        .toBe("word_" + (index + 1));
      await page.keyboard.type(answer);
    }
    await expect
      .poll(
        async () =>
          JSON.parse((await frame.locator("#arcade-status").textContent())!)
            .answerReady,
      )
      .toBe(true);
    await page.keyboard.press("Tab");
    await expect
      .poll(
        async () =>
          JSON.parse((await frame.locator("#arcade-status").textContent())!)
            .focus,
      )
      .toBe("check_answer");
    await page.keyboard.press("Enter");
    await expect(
      page.getByRole("heading", {
        name: "Answer checked: 100/100",
        exact: true,
      }),
    ).toBeVisible();
    for (const [mode, expected] of [
      ["picture", "bridge"],
      ["comprehension", "Residents needed more time"],
    ]) {
      const { current, frame } = await startMode(page, mode);
      await listen(page, frame);
      const choices = current.rounds[0].choices as {
        id: string;
        label: string;
      }[];
      const index = choices.findIndex((item) =>
        item.label.toLowerCase().includes(expected.toLowerCase()),
      );
      expect(index).toBeGreaterThanOrEqual(0);
      for (let step = 0; step <= index; step++)
        await page.keyboard.press("Tab");
      await page.keyboard.press("Enter");
      for (let step = index; step < choices.length; step++)
        await page.keyboard.press("Tab");
      await page.keyboard.press("Enter");
      await expect(
        page.getByRole("heading", {
          name: "Level score: 100/100",
          exact: true,
        }),
      ).toBeVisible();
    }
  });

  test("all_four_native_speaking_modes_submit_recordings_automatically", async ({
    page,
  }) => {
    await signIn(page);
    for (const mode of ["response", "riddle", "puzzle", "pk"]) {
      const { frame } = await startMode(page, mode);
      await listen(page, frame);
      await page.keyboard.press("Tab");
      await page.keyboard.press("Enter");
      await expect(frame.locator("#arcade-status")).toContainText(
        '"recording":"recording"',
      );
      const uploaded = page.waitForRequest(
        (request) =>
          request.url().endsWith("/v1/games/attempts") &&
          request.method() === "POST",
      );
      await page.keyboard.press("Enter");
      const body = (await uploaded).postDataJSON();
      expect(body.audio.length).toBeGreaterThan(0);
      expect(body.completedPlays).toBe(1);
      await expect.poll(async () => (await run(page)).receipts.length).toBe(1);
    }
  });

  test("frame_spoofs_are_ignored_and_pending_answers_retry_exactly_once", async ({
    page,
  }) => {
    await signIn(page);
    const { current, frame } = await startMode(page, "dictation");
    const attempts: unknown[] = [];
    await page.route("**/v1/games/attempts", async (route) => {
      attempts.push(route.request().postDataJSON());
      if (attempts.length === 1) {
        await route.fetch();
        await route.abort();
      } else await route.continue();
    });
    const forged = {
      channel: "contextspeak-arcade",
      action: "submit",
      roundId: current.rounds[0].id,
      answer: { response: "Forged answer" },
    };
    await page.evaluate(
      (data) => window.postMessage(data, location.origin),
      forged,
    );
    await frame
      .locator("body")
      .evaluate((_, data) => parent.postMessage(data, location.origin), forged);
    await listen(page, frame);
    expect(attempts).toHaveLength(0);
    await page.keyboard.press("Tab");
    await page.keyboard.type(current.rounds[0].audioText);
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    await expect(frame.locator("#arcade-status")).toContainText(
      '"pending":true',
    );
    await expect(
      page.getByRole("article", { name: "Godot 2D game" }).getByRole("alert"),
    ).toContainText("Result not confirmed");
    await frame
      .locator("body")
      .evaluate((_, data) => parent.postMessage(data, location.origin), forged);
    expect(attempts).toHaveLength(1);
    await expect
      .poll(
        async () =>
          JSON.parse((await frame.locator("#arcade-status").textContent())!)
            .focus,
      )
      .toBe("retry_answer");
    await page.keyboard.press("Enter");
    await expect(
      page.getByRole("heading", {
        name: "Answer checked: 100/100",
        exact: true,
      }),
    ).toBeVisible();
    expect(attempts).toHaveLength(2);
    expect(attempts[1]).toEqual(attempts[0]);
    expect((await run(page)).receipts).toHaveLength(1);
  });

  test("leaving_a_native_recording_stops_microphone_tracks", async ({
    page,
  }) => {
    await signIn(page);
    const { frame } = await startMode(page, "response");
    await listen(page, frame);
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    await expect(frame.locator("#arcade-status")).toContainText(
      '"recording":"recording"',
    );
    await page.keyboard.press("Tab");
    await page.keyboard.press("Enter");
    await expect(page.locator("iframe")).toHaveCount(0);
    await expect(
      page.getByRole("button", { name: "Start level", exact: true }),
    ).toBeVisible();
    expect(
      await page.evaluate(() => (window as any).audioFixture.stops),
    ).toBeGreaterThan(0);
  });
});

test("missing_export_offers_the_same_run_with_web_controls", async ({
  page,
}) => {
  await signIn(page);
  await page.route("**/arcade-engine/index.html", (route) =>
    route.fulfill({ status: 404, body: "Not built" }),
  );
  await page.getByRole("button", { name: "Dictation", exact: true }).click();
  await page.getByRole("button", { name: "Start level", exact: true }).click();
  await expect(
    page.getByText("Godot export is missing.", { exact: false }),
  ).toBeVisible();
  const id = (await run(page)).id;
  await page
    .getByRole("link", { name: "continue with web controls", exact: true })
    .click();
  await expect(page).toHaveURL("http://127.0.0.1:3000/games/web?run=" + id);
  await expect(
    page.getByRole("button", { name: "Play challenge", exact: true }),
  ).toBeVisible();
});
