import { installAudioFixtures } from "../helpers/practice-audio";
import { expect, test, type Page } from "@playwright/test";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
const itemId = "550e8400-e29b-41d4-a716-446655440000";
const lookupId = "550e8400-e29b-41d4-a716-446655440001";
async function setup(page: Page) {
  const saved: any[] = [];
  const wave = await new FixtureSpeechProvider().generate();
  await installAudioFixtures(page);
  await page.route("http://127.0.0.1:4318/**", async (route) => {
    const path = new URL(route.request().url()).pathname;
    let data: any = {};
    if (path === "/auth/session")
      data = {
        csrfToken: "fixture",
        identity: { subject: "practice-a" },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      };
    if (path === "/v1/speech") data = wave;
    if (path === `/v1/practice/${itemId}`)
      data = {
        itemId,
        lookupId,
        version: 1,
        expression: "available",
        speakingQuestion:
          "How could you use available to describe something from your own life?",
        sourceText: "Are you available for a call?",
        translation: "你有空接电话吗？",
        language: "en",
        recent: [],
      };
    if (path === `/v1/learning-items/${itemId}`)
      data = {
        item: {
          id: itemId,
          text: "available",
          sourceLanguage: "en",
          targetLanguage: "zh-Hans",
        },
        lookup: {
          id: lookupId,
          text: "available",
          meaning: "有空的",
          translationStatus: "succeeded",
        },
      };
    if (path.endsWith("/attempts")) {
      const body = route.request().postDataJSON();
      saved.push(body);
      if (saved.length === 1) {
        await route.fulfill({ status: 503, json: {} });
        return;
      }
      data = {
        id: "attempt-1",
        ...body,
        ...(body.activity === "dictation"
          ? {
              feedback: {
                method: "word-error-rate-v1",
                score: 67,
                correctWords: 4,
                totalWords: 6,
                transcript: body.scenario
                  ? "Would you like to meet for coffee this weekend?"
                  : "Are you available for a call?",
                missing: ["are"],
                extra: [],
                replacements: [{ expected: "available", actual: "ready" }],
              },
            }
          : {}),
        createdAt: new Date().toISOString(),
      };
    }
    if (path.endsWith("/speaking-assessment"))
      data = {
        id: "automatic-assessment",
        mode: "speaking",
        plays: 0,
        revealed: false,
        durationMs: 1000,
        createdAt: new Date().toISOString(),
        speakingAssessment: {
          speechDetected: true,
          overall: 80,
          scores: {
            clarity: 80,
            fluency: 80,
            grammar: 80,
            vocabulary: 80,
            taskResponse: 80,
          },
          transcript: "I am available.",
          strengths: [],
          improvements: ["Keep practising."],
          suggestedResponse: "I am available tomorrow.",
          model: "fixture",
          rubricVersion: 1,
        },
      };
    const scenario = new URL(route.request().url()).searchParams.get(
      "scenario",
    );
    if (path === `/v1/practice/${itemId}` && scenario) {
      data = {
        ...data,
        sourceText: "Would you like to meet for coffee this weekend?",
        translation: "A friend suggests meeting for coffee.",
        scenario: {
          id: scenario,
          title:
            scenario === "making-plans" ? "Making plans" : "Asking for help",
          goal: "Reply naturally using your saved expression.",
        },
      };
    }
    await route.fulfill({ json: data });
  });
  await page.goto(
    `http://127.0.0.1:3000/practice/${itemId}?lookup=${lookupId}`,
  );
  return saved;
}
test("listen_before_reveal_save_retry_and_record_speaking_without_uploading_audio", async ({
  page,
}) => {
  const saved = await setup(page);
  await expect(
    page.getByRole("button", { name: "Submit answer", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Submit answer", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("Are you ready for a call?");
  await expect(
    page.getByText("Are you available for a call?", { exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await expect(
    page.getByText("Are you available for a call?", { exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Your result: 67/100", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Are you available for a call?", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("ready → available", { exact: true }),
  ).toBeVisible();
  expect(saved[0]).toEqual(saved[1]);
  expect(saved[0]).toMatchObject({
    activity: "dictation",
    mode: "listening",
    completedPlays: 1,
    revealed: false,
  });
  expect(saved[0].selfCheck).toBeUndefined();
  expect(
    await page.evaluate(() => (window as any).audioFixture.spoken),
  ).toEqual(["AI"]);
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(page.getByLabel("Your recording")).toBeVisible();
  await expect(page.getByLabel("Your recording")).toHaveAttribute(
    "src",
    /^blob:/,
  );
  await page
    .getByLabel("Your recording")
    .evaluate((element) =>
      element.dispatchEvent(new Event("ended", { bubbles: true })),
    );
  expect(
    await page.evaluate(() => (window as any).audioFixture.stops),
  ).toBeGreaterThan(0);
  await page
    .getByRole("button", { name: "Needs practice", exact: true })
    .click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  expect(saved[2]).toMatchObject({
    mode: "speaking",
    selfCheck: "needs-practice",
  });
  expect(saved[2].durationMs).toBeGreaterThan(0);
  expect(saved[2].audio).toBeUndefined();
});
test("AI_quota_failure_does_not_enable_grading_and_microphone_denial_is_recoverable", async ({
  page,
}) => {
  await setup(page);
  await page.route("http://127.0.0.1:4318/v1/speech", (route) =>
    route.fulfill({ status: 429, json: {} }),
  );
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toContainText("quota");
  await page.getByLabel("Type what you hear").fill("A guess");
  await expect(
    page.getByRole("button", { name: "Submit answer", exact: true }),
  ).toBeDisabled();
  for (let retry = 0; retry < 20; retry++) {
    await page.getByRole("button", { name: "Play audio", exact: true }).click();
    await expect(page.locator("main").getByRole("alert")).toContainText(
      "quota",
    );
    await expect(
      page.getByRole("button", { name: "Play audio", exact: true }),
    ).toBeEnabled();
  }
  await page.unroute("http://127.0.0.1:4318/v1/speech");
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page.evaluate(() => {
    (window as any).audioFixture.deny = true;
  });
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "microphone",
  );
  await page.evaluate(() => {
    (window as any).audioFixture.deny = false;
  });
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("link", { name: "Back to vocabulary", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "available", exact: true }),
  ).toBeVisible();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.stops))
    .toBeGreaterThan(0);
});

test("session_revocation_clears_practice_and_stops_microphone_capture", async ({
  page,
}) => {
  await page.clock.install();
  await setup(page);
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  await page.route("http://127.0.0.1:4318/auth/session", (route) =>
    route.fulfill({ status: 401, json: {} }),
  );
  await page.clock.fastForward(3500);
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "session expired or changed",
  );
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toHaveCount(0);
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.stops))
    .toBeGreaterThan(0);
});

test("a_late_permission_grant_after_navigation_immediately_stops_its_tracks", async ({
  page,
}) => {
  await setup(page);
  await page.evaluate(() => {
    (window as any).audioFixture.delay = true;
  });
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", {
      name: "Cancel microphone request",
      exact: true,
    }),
  ).toBeVisible();
  await page
    .getByRole("link", { name: "Back to vocabulary", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "available", exact: true }),
  ).toBeVisible();
  await page.evaluate(() => (window as any).audioFixture.grant());
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.stops))
    .toBeGreaterThan(0);
});

test("recording_stops_automatically_at_the_duration_limit", async ({
  page,
}) => {
  await page.clock.install();
  await setup(page);
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  await page.clock.fastForward(61000);
  await expect(page.getByLabel("Your recording")).toBeVisible();
  await expect(page.getByLabel("Your recording")).toHaveAttribute(
    "src",
    /^blob:/,
  );
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toHaveCount(0);
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.stops))
    .toBeGreaterThan(0);
});

test("hiding_the_page_stops_an_active_recording", async ({ page }) => {
  await setup(page);
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  await page.evaluate(() => {
    Object.defineProperty(document, "hidden", {
      configurable: true,
      value: true,
    });
    document.dispatchEvent(new Event("visibilitychange"));
  });
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toHaveCount(0);
  expect(
    await page.evaluate(() => (window as any).audioFixture.stops),
  ).toBeGreaterThan(0);
});

test("practice_saves_through_the_real_API_and_native_recording_produces_decodable_audio", async ({
  page,
}, testInfo) => {
  await installAudioFixtures(page);
  await page.goto("http://127.0.0.1:3000/library");
  await page
    .getByRole("link", { name: "Sign in with Google", exact: true })
    .click();
  await page.getByRole("link", { name: "Test learner A", exact: true }).click();
  await expect(page.getByText("Connected", { exact: true })).toBeVisible();
  const session = await (
    await page.request.get("http://127.0.0.1:4318/auth/session")
  ).json();
  const headers = {
    Origin: "http://127.0.0.1:3000",
    "X-CSRF-Token": session.csrfToken,
  };
  await page.request.put("http://127.0.0.1:4318/v1/settings", {
    headers,
    data: { enabled: true, excludedOrigins: [] },
  });
  const captured = await page.request.post("http://127.0.0.1:4318/v1/lookups", {
    headers,
    data: {
      eventId: crypto.randomUUID(),
      text: "available",
      sourceLanguage: "en",
      targetLanguage: "en",
    },
  });
  expect(captured.status()).toBe(201);
  const receipt = await captured.json();
  await expect
    .poll(
      async () =>
        (
          await (
            await page.request.get(
              `http://127.0.0.1:4318/v1/lookups/${receipt.lookupId}/translation`,
            )
          ).json()
        ).translationStatus,
    )
    .toBe("succeeded");
  const detailPath = `/library/item/${receipt.candidateId}?lookup=${receipt.lookupId}`;
  await page.goto("http://127.0.0.1:3000" + detailPath);
  await page
    .getByRole("link", { name: "Practice listening and speaking", exact: true })
    .click();
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("AVAILABLE!");

  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("listening-practice.png"),
  });
  await page.reload();
  await expect(
    page.getByRole("region", { name: "Recent practice", exact: true }),
  ).toContainText("Listening · 100/100");
  await page.evaluate(() => {
    const native = (window as any).nativeAudio;
    Object.defineProperty(window, "MediaRecorder", { value: native.Recorder });
    Object.defineProperty(navigator.mediaDevices, "getUserMedia", {
      value: async () => {
        const context = new AudioContext();
        (window as any).practiceAudioContext = context;
        const tone = context.createOscillator();
        const destination = context.createMediaStreamDestination();
        tone.connect(destination);
        tone.start();
        await context.resume();
        return destination.stream;
      },
    });
  });
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  await expect
    .poll(() =>
      page.evaluate(() => (window as any).practiceAudioContext.currentTime),
    )
    .toBeGreaterThan(0.2);
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(page.getByLabel("Your recording")).toBeVisible();
  await expect(page.getByLabel("Your recording")).toHaveAttribute(
    "src",
    /^blob:/,
  );
  // Decode the container without requiring an audio-output device in headless CI.
  await page
    .getByLabel("Your recording")
    .evaluate((element: HTMLAudioElement) => element.load());
  await expect
    .poll(() =>
      page
        .getByLabel("Your recording")
        .evaluate((element: HTMLAudioElement) => element.readyState),
    )
    .toBeGreaterThanOrEqual(1);
  await page
    .getByLabel("Your recording")
    .evaluate((element) =>
      element.dispatchEvent(new Event("ended", { bubbles: true })),
    );
  await expect(
    page.getByRole("button", { name: "Needs practice", exact: true }),
  ).toBeEnabled();
  await page.evaluate(() => (window as any).practiceAudioContext.close());
  await page
    .getByRole("button", { name: "Needs practice", exact: true })
    .click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  await page.screenshot({ path: testInfo.outputPath("speaking-practice.png") });
  await expect(
    page.getByRole("heading", {
      name: "AI speaking score: 80/100",
      exact: true,
    }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("ai-speaking-assessment.png"),
    fullPage: true,
  });
  const task = await (
    await page.request.get(
      `http://127.0.0.1:4318/v1/practice/${receipt.candidateId}?lookup=${receipt.lookupId}`,
    )
  ).json();
  expect(task.recent.slice(0, 3).map((value: any) => value.mode)).toEqual([
    "speaking",
    "speaking",
    "listening",
  ]);
  expect(
    task.recent.find((value: any) => value.speakingAssessment)
      ?.speakingAssessment.overall,
  ).toBe(80);
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await expect(
    page.getByRole("heading", { name: "Making plans", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Speaking", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "Listening", exact: true }).click();
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page
    .getByLabel("Type what you hear")
    .fill("My friend suggests coffee on Saturday.");

  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  const scenarioTask = await (
    await page.request.get(
      `http://127.0.0.1:4318/v1/practice/${receipt.candidateId}?lookup=${receipt.lookupId}&scenario=making-plans`,
    )
  ).json();
  expect(scenarioTask.recent[0]).toMatchObject({
    scenario: "making-plans",
    mode: "listening",
    feedback: { score: expect.any(Number) },
  });
  await page.screenshot({ path: testInfo.outputPath("everyday-scenario.png") });
});

test("speaking_requires_replay_and_replay_is_stopped_when_changing_modes", async ({
  page,
}) => {
  await setup(page);
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(page.getByLabel("Your recording")).toBeVisible();
  await expect(page.getByLabel("Your recording")).toHaveAttribute(
    "src",
    /^blob:/,
  );
  await expect(
    page.getByRole("button", { name: "Comfortable", exact: true }),
  ).toBeDisabled();
  await page
    .getByLabel("Your recording")
    .evaluate((element) =>
      element.dispatchEvent(new Event("ended", { bubbles: true })),
    );
  await expect(
    page.getByRole("button", { name: "Comfortable", exact: true }),
  ).toBeEnabled();
  const pauses = await page.evaluate(() => (window as any).audioFixture.pauses);
  await page.getByRole("button", { name: "Listening", exact: true }).click();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.pauses))
    .toBeGreaterThan(pauses);
});

test("an_unconfirmed_save_can_be_explicitly_abandoned_for_a_new_attempt", async ({
  page,
}) => {
  await setup(page);
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("Available for a call");

  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await page
    .getByRole("button", { name: "Start a new attempt instead", exact: true })
    .click();
  await expect(page.getByLabel("Type what you hear")).toHaveValue("");
  await expect(
    page.getByRole("button", { name: "Speaking", exact: true }),
  ).toBeEnabled();
});

test("scenarios_hide_the_partner_turn_lock_retries_and_clear_recordings_on_switch", async ({
  page,
}) => {
  const saved = await setup(page);
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await expect(
    page.getByRole("heading", { name: "Making plans", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Would you like to meet for coffee this weekend?", {
      exact: true,
    }),
  ).toHaveCount(0);
  await expect(page.getByText("available", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("Meet a friend for coffee");

  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await expect(page.getByLabel("Conversation scenario")).toBeDisabled();
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  expect(saved[0]).toEqual(saved[1]);
  expect(saved[1].scenario).toBe("making-plans");
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Show partner's turn", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  const stops = await page.evaluate(() => (window as any).audioFixture.stops);
  await page
    .getByLabel("Conversation scenario")
    .selectOption("asking-for-help");
  await expect(
    page.getByRole("heading", { name: "Asking for help", exact: true }),
  ).toBeVisible();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.stops))
    .toBeGreaterThan(stops);
  await expect(page.getByLabel("Your recording")).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Speaking", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "Listening", exact: true }).click();
  await expect(page.getByLabel("Type what you hear")).toHaveValue("");
  await expect(
    page.getByText("Would you like to meet for coffee this weekend?", {
      exact: true,
    }),
  ).toHaveCount(0);
});

test("unfinished_AI_audio_does_not_unlock_submission_or_survive_scenario_switch", async ({
  page,
}) => {
  await setup(page);
  await page.evaluate(() => {
    (window as any).audioFixture.holdAudio = true;
  });
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.spoken.length))
    .toBe(1);
  await page.getByLabel("Type what you hear").fill("An unfinished guess");
  await expect(
    page.getByRole("button", { name: "Submit answer", exact: true }),
  ).toBeDisabled();
  const pauses = await page.evaluate(() => (window as any).audioFixture.pauses);
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await expect(
    page.getByRole("heading", { name: "Making plans", exact: true }),
  ).toBeVisible();
  await expect
    .poll(() => page.evaluate(() => (window as any).audioFixture.pauses))
    .toBeGreaterThan(pauses);
  await page.evaluate(() => {
    (window as any).audioFixture.finishAudio();
    (window as any).audioFixture.holdAudio = false;
  });
  await page.getByLabel("Type what you hear").fill("A new guess");
  await expect(
    page.getByRole("button", { name: "Submit answer", exact: true }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Submit answer", exact: true }),
  ).toBeEnabled();
});

test("scenario_refresh_AI_loading_and_nearby_result_actions", async ({
  page,
}) => {
  await setup(page);
  const returnTo =
    "/library?view=practice&practiceQ=available&practiceLanguage=zh-Hans";
  await page.goto(
    `http://127.0.0.1:3000/practice/${itemId}?lookup=${lookupId}&returnTo=${encodeURIComponent(returnTo)}`,
  );

  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await expect(page).toHaveURL(/scenario=making-plans/);
  await page.reload();
  await expect(page.getByLabel("Conversation scenario")).toHaveValue(
    "making-plans",
  );
  const wave = await new FixtureSpeechProvider().generate();
  let release: (() => void) | undefined;
  await page.route("http://127.0.0.1:4318/v1/speech", async (route) => {
    await new Promise<void>((resolve) => {
      release = resolve;
    });
    await route.fulfill({ json: wave });
  });
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Preparing AI audio…", { exact: true }),
  ).toBeVisible();
  await expect.poll(() => typeof release).toBe("function");
  release!();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("Would you like coffee?");
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Your result: 67/100", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Replay audio", exact: true }),
  ).toBeEnabled();
  await expect(
    page.getByRole("link", { name: "Choose next expression", exact: true }),
  ).toHaveAttribute("href", returnTo);
  await page.getByRole("button", { name: "Try again", exact: true }).click();
  await expect(page.getByLabel("Type what you hear")).toHaveValue("");
  await expect(
    page.getByRole("heading", { name: "Your result: 67/100", exact: true }),
  ).toHaveCount(0);
});

test("AI_speaking_assesses_automatically_once_and_only_retries_on_request", async ({
  page,
}) => {
  await setup(page);
  const uploads: any[] = [];
  await page.route(
    `http://127.0.0.1:4318/v1/practice/${itemId}/speaking-assessment`,
    async (route) => {
      uploads.push(route.request().postDataJSON());
      if (uploads.length === 1) {
        await route.fulfill({ status: 503, json: {} });
        return;
      }
      await route.fulfill({
        json: {
          id: "assessed-1",
          mode: "speaking",
          plays: 0,
          revealed: false,
          durationMs: 1000,
          createdAt: new Date().toISOString(),
          speakingAssessment: {
            speechDetected: true,
            overall: 80,
            scores: {
              clarity: 80,
              fluency: 70,
              grammar: 90,
              vocabulary: 80,
              taskResponse: 80,
            },
            transcript: "I am available on Saturday.",
            strengths: ["Clear sentence."],
            improvements: ["Add a closing question."],
            suggestedResponse:
              "I am available on Saturday. Does that work for you?",
            pronunciation: [
              {
                word: "available",
                ipa: "/əˈveɪləbəl/",
                stress: "Stress the second syllable: VAIL.",
                tip: "Touch your lower lip lightly to your upper teeth for /v/.",
              },
            ],
            model: "fixture",
            rubricVersion: 1,
          },
        },
      });
    },
  );
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(page.getByRole("checkbox")).toHaveCount(0);
  await expect(page.locator(".practice-question")).toContainText("available");
  await expect(
    page.getByRole("button", { name: "Get AI feedback", exact: true }),
  ).toHaveCount(0);
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await expect(
    page.getByRole("button", { name: "Start recording", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("button", { name: "Listening", exact: true }),
  ).toBeDisabled();
  expect(uploads).toHaveLength(1);
  await page.waitForTimeout(100);
  expect(uploads).toHaveLength(1);
  await page
    .getByRole("button", { name: "Retry AI feedback", exact: true })
    .click();
  await expect(
    page.getByRole("heading", {
      name: "AI speaking score: 80/100",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByText("I am available on Saturday.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Add a closing question.", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Pronunciation guidance" }),
  ).toContainText("/əˈveɪləbəl/");
  await expect(
    page.getByRole("region", { name: "Pronunciation guidance" }),
  ).toContainText("Stress the second syllable");
  expect(uploads[0].consent).toBeUndefined();
  expect(uploads[0]).toEqual(uploads[1]);
  expect(uploads[0]).toMatchObject({
    replayed: false,
    mimeType: "audio/wav",
  });
  expect(uploads[0].audio.length).toBeGreaterThan(100);
});

test("unscorable_speaking_shows_no_numeric_mark", async ({ page }) => {
  await setup(page);
  await page.route(
    `http://127.0.0.1:4318/v1/practice/${itemId}/speaking-assessment`,
    (route) =>
      route.fulfill({
        json: {
          id: "silent-1",
          mode: "speaking",
          revealed: false,
          plays: 0,
          durationMs: 1000,
          createdAt: new Date().toISOString(),
          speakingAssessment: {
            speechDetected: false,
            overall: null,
            scores: null,
            transcript: "",
            strengths: [],
            improvements: [
              "No clear speech was detected. Check your microphone and record a spoken sentence.",
            ],
            suggestedResponse: "",
            model: "fixture",
            rubricVersion: 1,
          },
        },
      }),
  );
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await page
    .getByLabel("Your recording")
    .evaluate((element) =>
      element.dispatchEvent(new Event("ended", { bubbles: true })),
    );
  await expect(
    page.getByRole("heading", {
      name: "No reliable speaking score",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByText("No clear speech was detected.", { exact: false }),
  ).toBeVisible();
  await expect(page.locator(".speaking-scores")).toHaveCount(0);
});

test("guided_dialogue_advances_four_turns_with_direct_submit_and_red_speaking_suggestions", async ({
  page,
}) => {
  await setup(page);
  const uploads: any[] = [];
  await page.route(
    `http://127.0.0.1:4318/v1/practice/${itemId}/speaking-assessment`,
    async (route) => {
      const body = route.request().postDataJSON();
      uploads.push(body);
      const turn = uploads.length;
      await route.fulfill({
        json: {
          id: `550e8400-e29b-41d4-a716-44665544001${turn}`,
          mode: "speaking",
          plays: 0,
          revealed: false,
          durationMs: 1000,
          createdAt: new Date().toISOString(),
          dialogue: { turn, history: [] },
          speakingAssessment: {
            speechDetected: true,
            overall: 80,
            scores: {
              clarity: 80,
              fluency: 80,
              grammar: 80,
              vocabulary: 80,
              taskResponse: 80,
            },
            transcript: "I available Saturday.",
            corrections: [
              {
                original: "I available",
                corrected: "I am available",
                reason: "Use am before the adjective.",
              },
            ],
            strengths: ["Relevant reply."],
            improvements: ["Use a complete sentence."],
            suggestedResponse: "I am available on Saturday.",
            nextTurn:
              turn < 4
                ? `Saturday works. What shall we do for turn ${turn + 1}?`
                : null,
            model: "fixture",
            rubricVersion: 1,
          },
        },
      });
    },
  );
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await page.getByRole("button", { name: "Conversation", exact: true }).click();
  for (let turn = 1; turn <= 4; turn++) {
    await expect(
      page.getByRole("heading", {
        name: `Conversation · turn ${turn} of 4`,
        exact: true,
      }),
    ).toBeVisible();
    await expect(
      page.getByRole("button", { name: "Start recording", exact: true }),
    ).toBeDisabled();
    await page.getByRole("button", { name: "Play audio", exact: true }).click();
    await expect(
      page.getByText("Playback complete.", { exact: true }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Start recording", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Stop recording", exact: true })
      .click();
    await expect(page.getByRole("checkbox")).toHaveCount(0);
    const completed = page.getByRole("region", {
      name: `Completed turn ${turn}`,
      exact: true,
    });
    await expect(
      completed.getByLabel("Assessed transcript").locator("mark"),
    ).toHaveText("I available");
    await expect(
      completed
        .getByRole("listitem")
        .filter({ hasText: "Use am before the adjective." }),
    ).toBeVisible();
    expect(uploads[turn - 1].conversation).toBe(true);
    if (turn > 1)
      expect(uploads[turn - 1].previousTurnId).toBe(
        `550e8400-e29b-41d4-a716-44665544001${turn - 1}`,
      );
    if (turn < 4)
      await page
        .getByRole("button", { name: "Continue conversation", exact: true })
        .click();
  }
  await expect(
    page.getByRole("heading", { name: "Conversation complete", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", {
      name: "Conversation estimate: 80/100",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Continue conversation", exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "New attempt", exact: true }).click();
  await expect(
    page.getByRole("heading", {
      name: "Conversation · turn 1 of 4",
      exact: true,
    }),
  ).toBeVisible();
});

test("listening_highlights_a_replaced_word_only_after_answer_submission", async ({
  page,
}) => {
  await setup(page);
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("Are you ready for a call?");
  await expect(page.getByLabel("Highlighted answer")).toHaveCount(0);
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toBeVisible();
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(
    page.getByLabel("Highlighted answer").locator("mark"),
  ).toHaveText("ready → available");
  await expect(page.getByLabel("Highlighted answer").locator("mark")).toHaveCSS(
    "color",
    "rgb(143, 36, 18)",
  );
});

test("unclear_audio_mid_conversation_does_not_advance_or_lose_previous_turn", async ({
  page,
}) => {
  await setup(page);
  const uploads: any[] = [];
  await page.route(
    `http://127.0.0.1:4318/v1/practice/${itemId}/speaking-assessment`,
    async (route) => {
      uploads.push(route.request().postDataJSON());
      const silent = uploads.length === 2;
      await route.fulfill({
        json: {
          id: `550e8400-e29b-41d4-a716-44665544002${uploads.length}`,
          mode: "speaking",
          plays: 0,
          revealed: false,
          durationMs: 1000,
          createdAt: new Date().toISOString(),
          speakingAssessment: {
            speechDetected: !silent,
            overall: silent ? null : 80,
            scores: silent
              ? null
              : {
                  clarity: 80,
                  fluency: 80,
                  grammar: 80,
                  vocabulary: 80,
                  taskResponse: 80,
                },
            transcript: silent ? "" : "Saturday works for me.",
            strengths: [],
            improvements: [
              silent ? "No clear speech was detected." : "Keep practising.",
            ],
            suggestedResponse: "",
            nextTurn: silent ? null : "Where shall we meet?",
            model: "fixture",
            rubricVersion: 1,
          },
        },
      });
    },
  );
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await page.getByRole("button", { name: "Conversation", exact: true }).click();
  async function recordReply() {
    await page.getByRole("button", { name: "Play audio", exact: true }).click();
    await expect(
      page.getByText("Playback complete.", { exact: true }),
    ).toBeVisible();
    await page
      .getByRole("button", { name: "Start recording", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Stop recording", exact: true })
      .click();
    await expect(page.getByRole("checkbox")).toHaveCount(0);
  }
  await recordReply();
  await page
    .getByRole("button", { name: "Continue conversation", exact: true })
    .click();
  await recordReply();
  await expect(
    page.getByRole("heading", {
      name: "No reliable speaking score",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", {
      name: "Conversation · turn 2 of 4",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Continue conversation", exact: true }),
  ).toHaveCount(0);
  await page
    .getByRole("button", { name: "Record another response", exact: true })
    .click();
  await recordReply();
  await expect(
    page.getByRole("region", { name: "Completed turn 2", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Continue conversation", exact: true }),
  ).toBeVisible();
  expect(uploads[1].previousTurnId).toBe(
    "550e8400-e29b-41d4-a716-446655440021",
  );
  expect(uploads[2].previousTurnId).toBe(uploads[1].previousTurnId);
  expect(uploads[2].eventId).not.toBe(uploads[1].eventId);
});

test("consecutive_missing_words_share_one_phrase_label", async ({ page }) => {
  await setup(page);
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("Type what you hear").fill("ss");
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  await expect(page.locator("main").getByRole("alert")).toBeVisible();
  await page
    .getByRole("button", { name: "Submit answer", exact: true })
    .click();
  const marks = page.getByLabel("Highlighted answer").locator("mark");
  await expect(marks).toHaveText([
    "Missing: Are you available for a",
    "ss → call",
  ]);
  await expect(
    page.getByRole("heading", { name: "Your result: 67/100", exact: true }),
  ).toBeVisible();
});

test("limited_audio_shows_feedback_without_a_numeric_score_or_difficulty_penalty", async ({
  page,
}) => {
  await setup(page);
  await page.route(
    `http://127.0.0.1:4318/v1/practice/${itemId}/speaking-assessment`,
    (route) =>
      route.fulfill({
        json: {
          id: "limited-audio",
          mode: "speaking",
          plays: 0,
          revealed: false,
          durationMs: 1000,
          createdAt: new Date().toISOString(),
          speakingAssessment: {
            speechDetected: true,
            transcript: "I am available.",
            audioQuality: "limited",
            evidence: [],
            scores: null,
            overall: null,
            strengths: [],
            improvements: ["Record again in a quieter place."],
            suggestedResponse: "",
            model: "fixture",
            rubricVersion: 2,
          },
        },
      }),
  );
  await page.getByRole("button", { name: "Speaking", exact: true }).click();
  await page
    .getByRole("button", { name: "Start recording", exact: true })
    .click();
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(
    page.getByRole("heading", {
      name: "No reliable speaking score",
      exact: true,
    }),
  ).toBeVisible();
  await expect(
    page.getByText("this result will not change your recommended difficulty", {
      exact: false,
    }),
  ).toBeVisible();
  await expect(page.locator(".speaking-scores")).toHaveCount(0);
});
