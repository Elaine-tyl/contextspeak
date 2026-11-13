import { expect, test, type Page } from "@playwright/test";
import type { GameLevelRun } from "@contextspeak/contracts";
import { sceneWordChallenges } from "../../apps/api/src/games/scene-words";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
import { installAudioFixtures } from "../helpers/practice-audio";

async function fixture(page: Page, correct = true, completes = false) {
  await installAudioFixtures(page);
  const secret = sceneWordChallenges[0];
  const { answer, gapAnswers, sceneWordNotes, ...round } = secret;
  const run: GameLevelRun = {
    id: "66666666-6666-4666-8666-666666666666",
    kind: "scene-words",
    level: 1,
    topic: null,
    source: null,
    rounds: [round],
    receipts: [],
    completed: false,
    score: null,
    passed: false,
    nextLevel: null,
  };
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "scene-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: [round],
        progress: {
          listening: { level: 1, passed: [] },
          speaking: { level: 1, passed: [] },
        },
        modeProgress: { "scene-words": { level: 1 } },
      },
    }),
  );
  await page.route("**/v1/games/levels/*", (route) =>
    route.fulfill({ json: run }),
  );
  const audio = await new FixtureSpeechProvider().generate();
  let speeches = 0,
    submissions = 0;
  await page.route("**/v1/speech", (route) => {
    speeches++;
    return route.fulfill({ json: audio });
  });
  await page.route("**/v1/games/attempts", async (route) => {
    submissions++;
    const body = route.request().postDataJSON();
    expect(body.gapAnswers).toEqual([
      correct ? secret.gapAnswers![0] : "wrong",
    ]);
    expect(body.completedPlays).toBe(1);
    expect(body.levelSessionId).toBe(run.id);
    const receipt = {
      id: "scene-receipt",
      roundId: round.id,
      score: correct ? 100 : 0,
      createdAt: new Date().toISOString(),
      feedback: {
        explanation: secret.explanation,
        sceneWordNotes,
        gapFill: {
          score: correct ? 100 : 0,
          words: [
            {
              answer: body.gapAnswers[0],
              expected: secret.gapAnswers![0],
              correct,
            },
          ],
        },
      },
    };
    if (completes) {
      run.receipts = [receipt];
      run.completed = true;
      run.score = receipt.score;
      run.passed = correct;
      run.nextLevel = correct ? 2 : null;
    }
    await route.fulfill({ json: receipt });
  });
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  return { secret, counts: () => ({ speeches, submissions }) };
}

test("a 3D room supports listen, keyboard fill, saved marks and a word notebook", async ({
  page,
}, info) => {
  const { secret, counts } = await fixture(page);
  const room = page.getByTestId("scene-word-stage");
  await expect(room).toBeVisible();
  await expect(room.locator(".scene-word-room")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  await expect(page.getByRole("region", { name: "Word notebook" })).toHaveCount(
    0,
  );
  const input = page.getByRole("textbox", {
    name: "Missing word",
    exact: true,
  });
  await expect(input).toBeDisabled();
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(input).toBeEnabled();
  await input.fill(secret.gapAnswers![0]);
  await input.press("Enter");
  const notes = page.getByRole("region", { name: "Word notebook" });
  await expect(notes).toContainText(secret.sceneWordNotes!.meaning);
  await expect(notes).toContainText(secret.sceneWordNotes!.translation);
  await expect(room).toContainText("PERFECT!");
  await expect(input).toBeDisabled();
  expect(counts()).toEqual({ speeches: 1, submissions: 1 });
  await page
    .getByTestId("game-studio")
    .screenshot({ path: info.outputPath("scene-words-success.png") });
});

test("a wrong word stays playable without WebGL and the mobile notebook shows the correction", async ({
  page,
}, info) => {
  await page.addInitScript(() => {
    const original = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function (
      this: HTMLCanvasElement,
      kind: any,
      ...args: any[]
    ) {
      if (String(kind).startsWith("webgl")) return null;
      return (original as any).call(this, kind, ...args);
    } as any;
  });
  const { counts } = await fixture(page, false);
  await page.setViewportSize({ width: 390, height: 844 });
  const room = page.getByTestId("scene-word-stage");
  await expect(room.locator(".scene-word-room")).toHaveAttribute(
    "data-renderer",
    "fallback",
  );
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  const input = page.getByRole("textbox", {
    name: "Missing word",
    exact: true,
  });
  await expect(input).toBeEnabled();
  await input.fill("wrong");
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(
    page.getByRole("region", { name: "Word notebook" }),
  ).toContainText("wardrobe");
  await expect(room).not.toContainText("PERFECT!");
  await expect(
    page.getByRole("region", { name: "Missing words feedback" }),
  ).toContainText("wardrobe");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(counts()).toEqual({ speeches: 1, submissions: 1 });
  await page.screenshot({
    path: info.outputPath("scene-words-mobile-fallback.png"),
    fullPage: true,
  });
});

test("the final word notebook survives level completion and a page reload", async ({
  page,
}) => {
  const { secret } = await fixture(page, true, true);
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  const input = page.getByRole("textbox", {
    name: "Missing word",
    exact: true,
  });
  await expect(input).toBeEnabled();
  await input.fill(secret.gapAnswers![0]);
  await input.press("Enter");
  await expect(
    page.getByRole("region", { name: "Level result" }),
  ).toBeVisible();
  await page.getByText("Review answers", { exact: true }).click();
  await expect(
    page.getByRole("region", { name: "Word notebook" }),
  ).toContainText(secret.sceneWordNotes!.meaning);
  await expect(
    page.getByRole("button", { name: "Hear wardrobe", exact: true }),
  ).toBeVisible();
  await page.reload();
  await page.getByText("Review answers", { exact: true }).click();
  await expect(
    page.getByRole("region", { name: "Word notebook" }),
  ).toContainText(secret.sceneWordNotes!.translation);
});
