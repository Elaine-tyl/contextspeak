import { expect, test, type Page } from "@playwright/test";
import {
  gameModes,
  type GameKind,
  type GameRound,
} from "@contextspeak/contracts";
import { installAudioFixtures } from "../helpers/practice-audio";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";

async function openStudio(page: Page, kind: GameKind) {
  const mode = gameModes.find((item) => item.kind === kind)!;
  const round: GameRound = {
    id: `studio-${kind}`,
    kind,
    track: mode.track,
    level: 4,
    title: mode.label,
    instruction: "Listen, then give your answer.",
    audioText: "The library closes at six.",
    ...(kind === "scene-words"
      ? {
          audioText: "There is a wardrobe in the room.",
          gapText: ["There is a ", " in the room."],
          scene: { room: "bedroom" as const, focus: "wardrobe" as const },
        }
      : {}),
    ...(kind === "gap-fill" ? { gapText: ["The ", " closes at ", "."] } : {}),
    ...(["spelling", "word-order"].includes(kind)
      ? { tiles: ["library", "The", "closes"] }
      : {}),
    choices: [
      {
        id: "a",
        label: "Library",
        ...(kind === "picture" ? { picture: "book" as const } : {}),
      },
      {
        id: "b",
        label: "Coffee",
        ...(kind === "picture" ? { picture: "coffee" as const } : {}),
      },
    ],
    ...(kind === "pk" ? { opponentScore: 70 } : {}),
  };
  const audio = await new FixtureSpeechProvider().generate();
  const run = {
    id: "22222222-2222-4222-8222-222222222222",
    kind,
    level: 4,
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
        csrfToken: "studio-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: [round],
        progress: {
          listening: { level: 4, passed: [] },
          speaking: { level: 4, passed: [] },
        },
      },
    }),
  );
  await page.route("**/v1/games/levels/*", (route) =>
    route.fulfill({ json: run }),
  );
  await page.route("**/v1/speech", (route) => route.fulfill({ json: audio }));
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  return round;
}

test("each mode has its own studio, help restores focus and mobile controls stay usable", async ({
  page,
}, testInfo) => {
  await installAudioFixtures(page);
  const environments = new Set<string>();
  for (const mode of gameModes) {
    await openStudio(page, mode.kind);
    const studio = page.getByTestId("game-studio");
    await expect(studio).toHaveAttribute("data-kind", mode.kind);
    environments.add(await studio.locator("header .studio-name").innerText());
    // Listening answers must stay hidden even when the scene is fully rendered.
    if (mode.track === "listening") {
      await expect(studio).not.toContainText("The library closes at six.");
    }
    const help = page.getByRole("button", { name: "How to play", exact: true });
    await help.click();
    const dialog = page.getByRole("dialog", { name: "How to play" });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText(mode.description);
    await dialog.getByRole("button", { name: "Got it", exact: true }).focus();
    await page.keyboard.press("Shift+Tab");
    await expect(
      dialog.getByRole("button", { name: "Close instructions", exact: true }),
    ).toBeFocused();
    // Native modal dialogs make the background inert, including programmatic focus.
    await help.evaluate((element) => element.focus());
    await expect(
      dialog.getByRole("button", { name: "Close instructions", exact: true }),
    ).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(help).toBeFocused();
    await expect(studio.locator(".studio-visual")).toHaveAttribute(
      "data-renderer",
      "webgl",
    );
    if (["dictation", "gap-fill", "picture", "response"].includes(mode.kind)) {
      await studio.screenshot({
        path: testInfo.outputPath(`contextspeak-studio-${mode.kind}.png`),
      });
    }
  }
  expect(environments.size).toBe(gameModes.length);
  await page.setViewportSize({ width: 390, height: 844 });
  await openStudio(page, "gap-fill");
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(
    page.getByLabel("Missing word 1", { exact: true }),
  ).toBeEnabled();
  await page.getByLabel("Missing word 1", { exact: true }).fill("library");
  await page.getByLabel("Missing word 2", { exact: true }).fill("six");
  await page.getByTestId("game-studio").screenshot({
    path: testInfo.outputPath("contextspeak-studio-mobile.png"),
  });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});

test("dictation scene follows playback and input without changing the saved answer", async ({
  page,
}) => {
  await installAudioFixtures(page);
  await openStudio(page, "dictation");
  const stage = page.locator(".studio-visual");
  await expect(stage).toHaveAttribute("data-renderer", "webgl");
  await expect(stage).toHaveAttribute("data-state", "ready");
  await page.evaluate(() => {
    (window as any).audioFixture.holdAudio = true;
  });
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(stage).toHaveAttribute("data-state", "listening");
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByText("AI audio in progress.", { exact: true }),
  ).toBeVisible();
  await page.evaluate(() => {
    (window as any).audioFixture.finishAudio();
  });
  await page
    .getByLabel("Type what you hear")
    .fill("The library closes at six.");
  await expect(page.locator(".studio-answer-count")).toContainText("5 words");
  let submissions = 0;
  await page.route("**/v1/games/attempts", async (route) => {
    submissions++;
    expect(route.request().postDataJSON().response).toBe(
      "The library closes at six.",
    );
    await route.fulfill({
      json: {
        id: "studio-receipt",
        roundId: "studio-dictation",
        score: 82,
        createdAt: new Date().toISOString(),
        feedback: { explanation: "Fixture feedback." },
      },
    });
  });
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(stage).toHaveAttribute("data-state", "checked");
  await expect(page.getByRole("region", { name: "Game result" })).toContainText(
    "82",
  );
  expect(submissions).toBe(1);
});

test("picture choices work without WebGL and motion preference freezes the live studio", async ({
  page,
}) => {
  await installAudioFixtures(page);
  await openStudio(page, "picture");
  const canvas = page.locator(".studio-visual canvas");
  await expect(page.locator(".studio-visual")).toHaveAttribute(
    "data-renderer",
    "webgl",
  );
  await page.getByRole("button", { name: "3D effects", exact: true }).click();
  await expect(page.getByTestId("game-studio")).toHaveAttribute(
    "data-motion",
    "off",
  );
  const first = await canvas.screenshot();
  await page.evaluate(
    () =>
      new Promise<void>((resolve) => {
        let frames = 0;
        function next() {
          if (++frames === 8) resolve();
          else requestAnimationFrame(next);
        }
        requestAnimationFrame(next);
      }),
  );
  expect(await canvas.screenshot()).toEqual(first);
  // A real lost context falls back in-place without losing the round or answer controls.
  await canvas.evaluate((element) => {
    (element as HTMLCanvasElement)
      .getContext("webgl2")
      ?.getExtension("WEBGL_lose_context")
      ?.loseContext();
  });
  await expect(page.locator(".studio-visual")).toHaveAttribute(
    "data-renderer",
    "fallback",
  );
  const choice = page.getByRole("button", {
    name: "Choose library picture",
    exact: true,
  });
  await expect(choice).toBeDisabled();
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await choice.click();
  await expect(choice).toHaveAttribute("aria-pressed", "true");
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeEnabled();
});

test("speaking studio records and automatically submits once without a new assessment button", async ({
  page,
}) => {
  await installAudioFixtures(page);
  await openStudio(page, "response");
  let submissions = 0;
  await page.route("**/v1/games/attempts", async (route) => {
    submissions++;
    const answer = route.request().postDataJSON();
    expect(answer.audio.length).toBeGreaterThan(0);
    expect(answer.completedPlays).toBe(1);
    await route.fulfill({
      json: {
        id: "voice-receipt",
        roundId: "studio-response",
        score: 78,
        createdAt: new Date().toISOString(),
        feedback: { explanation: "Fixture voice feedback." },
      },
    });
  });
  await expect(
    page.getByRole("button", { name: "Start recording", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(page.locator(".studio-visual")).toHaveAttribute(
    "data-state",
    "recording",
  );
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(page.getByRole("region", { name: "Game result" })).toContainText(
    "78",
  );
  expect(submissions).toBe(1);
  expect(
    await page.evaluate(() => (window as any).audioFixture.stops),
  ).toBeGreaterThan(0);
});

test("quick-fire help cannot consume timed answering seconds", async ({
  page,
}) => {
  await installAudioFixtures(page);
  await openStudio(page, "true-false");
  const help = page.getByRole("button", { name: "How to play", exact: true });
  await expect(help).toBeEnabled();
  await page
    .getByRole("button", { name: "Play challenge", exact: true })
    .click();
  await expect(page.locator(".studio-visual")).toHaveAttribute(
    "data-state",
    "answering",
  );
  await expect(help).toBeDisabled();
  await expect(
    page.getByRole("dialog", { name: "How to play" }),
  ).not.toBeVisible();
  await page.getByRole("button", { name: "Library", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeEnabled();
});
