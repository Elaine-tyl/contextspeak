import { expect, test, type Page } from "@playwright/test";
const itemId = "550e8400-e29b-41d4-a716-446655440000";
const lookupId = "550e8400-e29b-41d4-a716-446655440001";
async function installAudioFixtures(page: Page) {
  await page.addInitScript(() => {
    (window as any).nativeAudio = {
      Recorder: window.MediaRecorder,
      microphone: navigator.mediaDevices.getUserMedia.bind(
        navigator.mediaDevices,
      ),
    };
    const state: any = ((window as any).audioFixture = {
      stops: 0,
      pauses: 0,
      deny: false,
      remoteOnly: false,
      spoken: [] as string[],
    });
    const pause = HTMLMediaElement.prototype.pause;
    HTMLMediaElement.prototype.pause = function () {
      state.pauses++;
      pause.call(this);
    };
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
      state.spoken.push(utterance.voice.name);
      utterance.onstart?.(new Event("start"));
      setTimeout(() => utterance.onend?.(new Event("end")), 0);
    };
    synth.cancel = () => {};
    Object.defineProperty(window, "speechSynthesis", { value: synth });
    Object.defineProperty(navigator.mediaDevices, "getUserMedia", {
      configurable: true,
      value: async () => {
        if (state.deny) throw new DOMException("Denied", "NotAllowedError");
        const stream = { getTracks: () => [{ stop: () => state.stops++ }] };
        if (state.delay)
          return new Promise((resolve) => {
            state.grant = () => resolve(stream);
          });
        return stream;
      },
    });
    function silentRecording() {
      const buffer = new ArrayBuffer(1644);
      const view = new DataView(buffer);
      for (const [offset, text] of [
        [0, "RIFF"],
        [8, "WAVE"],
        [12, "fmt "],
        [36, "data"],
      ] as const)
        for (let index = 0; index < text.length; index++)
          view.setUint8(offset + index, text.charCodeAt(index));
      view.setUint32(4, 1636, true);
      view.setUint32(16, 16, true);
      view.setUint16(20, 1, true);
      view.setUint16(22, 1, true);
      view.setUint32(24, 8000, true);
      view.setUint32(28, 16000, true);
      view.setUint16(32, 2, true);
      view.setUint16(34, 16, true);
      view.setUint32(40, 1600, true);
      return new Blob([buffer], { type: "audio/wav" });
    }
    class Recorder {
      state = "inactive";
      mimeType = "audio/wav";
      ondataavailable: any;
      onstop: any;
      onerror: any;
      static isTypeSupported() {
        return true;
      }
      start() {
        this.state = "recording";
      }
      stop() {
        this.state = "inactive";
        this.ondataavailable?.({
          data: silentRecording(),
        });
        this.onstop?.(new Event("stop"));
      }
    }
    Object.defineProperty(window, "MediaRecorder", {
      configurable: true,
      value: Recorder,
    });
  });
}
async function setup(page: Page) {
  const saved: any[] = [];
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
    if (path === `/v1/practice/${itemId}`)
      data = {
        itemId,
        lookupId,
        version: 1,
        expression: "available",
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
      data = { id: "attempt-1", ...body, createdAt: new Date().toISOString() };
    }
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
    page.getByRole("heading", { name: "Listening and speaking", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("Are you available for a call?", { exact: true }),
  ).toHaveCount(0);
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page.getByLabel("What did it mean?").fill("Can you take a call?");
  await page
    .getByRole("button", { name: "Reveal transcript and meaning", exact: true })
    .click();
  await expect(
    page.getByText("你有空接电话吗？", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  expect(saved[0]).toEqual(saved[1]);
  expect(saved[0]).toMatchObject({
    mode: "listening",
    completedPlays: 1,
    revealed: true,
  });
  expect(
    await page.evaluate(() => (window as any).audioFixture.spoken),
  ).toEqual(["Local"]);
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
test("missing_local_voice_and_denied_microphone_are_recoverable", async ({
  page,
}) => {
  await setup(page);
  await page.evaluate(() => {
    (window as any).audioFixture.remoteOnly = true;
    speechSynthesis.dispatchEvent(new Event("voiceschanged"));
  });
  await expect(page.getByText(/No on-device voice/)).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Play audio", exact: true }),
  ).toBeDisabled();
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
  await page.getByLabel("What did it mean?").fill("Free to take part");
  await page
    .getByRole("button", { name: "Reveal transcript and meaning", exact: true })
    .click();
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
  await expect(
    page.getByText("Practice saved.", { exact: true }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("listening-practice.png"),
  });
  await page.reload();
  await expect(
    page.getByRole("region", { name: "Recent practice", exact: true }),
  ).toContainText("Listening · Comfortable");
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
  const task = await (
    await page.request.get(
      `http://127.0.0.1:4318/v1/practice/${receipt.candidateId}?lookup=${receipt.lookupId}`,
    )
  ).json();
  expect(task.recent.slice(0, 2).map((value: any) => value.mode)).toEqual([
    "speaking",
    "listening",
  ]);
  await page.getByLabel("Conversation scenario").selectOption("making-plans");
  await expect(
    page.getByRole("heading", { name: "Making plans", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Play audio", exact: true }).click();
  await expect(
    page.getByText("Playback complete.", { exact: true }),
  ).toBeVisible();
  await page
    .getByLabel("What did it mean?")
    .fill("My friend suggests coffee on Saturday.");
  await page
    .getByRole("button", { name: "Reveal transcript and meaning", exact: true })
    .click();
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
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
    selfCheck: "comfortable",
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
  await page.getByLabel("What did it mean?").fill("Available for a call");
  await page
    .getByRole("button", { name: "Reveal transcript and meaning", exact: true })
    .click();
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await page
    .getByRole("button", { name: "Start a new attempt instead", exact: true })
    .click();
  await expect(page.getByLabel("What did it mean?")).toHaveValue("");
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
  await page.getByLabel("What did it mean?").fill("Meet a friend for coffee");
  await page
    .getByRole("button", { name: "Reveal transcript and meaning", exact: true })
    .click();
  await expect(
    page.getByText("Would you like to meet for coffee this weekend?", {
      exact: true,
    }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
  await expect(page.locator("main").getByRole("alert")).toContainText(
    "not confirmed",
  );
  await expect(page.getByLabel("Conversation scenario")).toBeDisabled();
  await page.getByRole("button", { name: "Comfortable", exact: true }).click();
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
  await expect(page.getByLabel("What did it mean?")).toHaveValue("");
  await expect(
    page.getByText("Would you like to meet for coffee this weekend?", {
      exact: true,
    }),
  ).toHaveCount(0);
});
