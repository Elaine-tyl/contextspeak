import { openWorld } from "./helpers";
import { conversationInput } from "../../../packages/world/conversation";
import { normalizeWave } from "../../../packages/world/protocol";
import { expect, test, type Page } from "@playwright/test";
import { FixtureSpeechProvider } from "../../../apps/api/src/speech/gemini-speech.provider";

async function openCafe(page: Page) {
  page.on("console", (message) => {
    if (message.type() === "error")
      console.log("Unity runtime:", message.text().slice(0, 700));
  });
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/speech", (route) =>
    route.fulfill({
      contentType: "audio/wav",
      body: Buffer.from(audio.audio, "base64"),
    }),
  );
  await page.route("**/conversation", async (route) => {
    const input = conversationInput.parse(route.request().postDataJSON());
    const accepted = !input.text.toLowerCase().includes("unsure");
    await route.fulfill({
      json: {
        accepted,
        acknowledgement: "That sounds nice!",
        feedback: accepted
          ? "Your answer meets the communication goal."
          : "Try adding a reason for your choice.",
      },
    });
  });
  await openWorld(page, "http://127.0.0.1:4320/unity/");
  await expect(page.locator("#unity-status")).toContainText(
    '"phase":"counter"',
    { timeout: 120000 },
  );
}
async function reply(page: Page, text: string) {
  await page.getByLabel("Your reply", { exact: true }).fill(text);
  await page.getByRole("button", { name: "Send reply", exact: true }).click();
}
test("native_unity_walks_towards_the_counter_and_interacts", async ({
  page,
}, info) => {
  await openCafe(page);
  const start = JSON.parse(
    (await page.locator("#unity-status").textContent())!,
  ).z;
  await page.locator("#unity-canvas").focus();
  await page.keyboard.down("w");
  await expect
    .poll(
      async () =>
        JSON.parse((await page.locator("#unity-status").textContent())!).z,
    )
    .toBeLessThan(start - 1);
  await page.keyboard.up("w");
  await page.screenshot({
    path: info.outputPath("unity-cafe.png"),
    fullPage: true,
  });
});

async function counter(page: Page) {
  await openCafe(page);
  await page.locator("#stage").selectOption("counter");
  await expect(page.locator("#objective")).toContainText("Practice stage");
  await page.locator("#interact").click();
  await expect(page.locator("#speaker")).toHaveText("Maya · Barista");
}
async function placeOrder(page: Page) {
  await reply(page, "One coffee and one tea, please.");
  await expect(page.locator("#line")).toContainText("hot or iced");
  await reply(page, "Hot, please.");
  await expect(page.locator("#line")).toContainText("for here or to go");
  await reply(page, "For here, please.");
  await expect(page.locator("#line")).toContainText("Shall I place your order");
  await reply(page, "No, that's not right.");
  await expect(page.locator("#unity-status")).toContainText(
    '"stage":"confirm"',
  );
  await reply(page, "Yes, please.");
  await expect(
    page.getByRole("button", { name: "Join Alex", exact: true }),
  ).toBeVisible();
}
test("changing_practice_stage_does_not_cancel_the_initial_connection", async ({
  page,
}) => {
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route("**/session", async (route) => {
    await held;
    await route.continue();
  });
  try {
    await counter(page);
    release();
    await expect(page.locator("#status")).toContainText("Local AI connected");
    await page
      .getByRole("button", { name: "Record reply", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Stop recording", exact: true }),
    ).toBeVisible();
  } finally {
    release();
  }
});
test("a_late_old_request_cannot_disconnect_a_successful_reconnection", async ({
  page,
}) => {
  await openCafe(page);
  await expect(page.locator("#status")).toContainText("Local AI connected");
  await page.locator("#stage").selectOption("friend");
  await page.locator("#interact").click();
  let release!: () => void;
  const held = new Promise<void>((resolve) => {
    release = resolve;
  });
  let requested = false;
  await page.route("**/conversation", async (route) => {
    requested = true;
    await held;
    await route.fulfill({
      status: 401,
      json: { error: "Old session expired" },
    });
  });
  try {
    await reply(page, "I enjoy tea because it helps me relax.");
    await expect.poll(() => requested).toBe(true);
    await page.getByRole("button", { name: "Reconnect local AI" }).click();
    await expect(page.locator("#status")).toContainText("Local AI connected");
    release();
    await expect(page.locator("#status")).toContainText("unavailable or busy");
    await page
      .getByRole("button", { name: "Record reply", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Stop recording", exact: true }),
    ).toBeVisible();
  } finally {
    release();
  }
});
test("ordering_walk_to_friend_corrections_and_three_followups_complete_the_task", async ({
  page,
}, info) => {
  await counter(page);
  const input = page.getByLabel("Your reply", { exact: true });
  await input.click();
  await page.keyboard.type("Two lattes, please.");
  await expect(input).toHaveValue("Two lattes, please.");
  await page.getByRole("button", { name: "Send reply", exact: true }).click();
  await expect(page.locator("#line")).toContainText("over your budget");
  await placeOrder(page);
  await page.getByRole("button", { name: "Join Alex", exact: true }).click();
  await expect(page.locator("#unity-status")).toContainText(
    '"phase":"walking"',
  );
  await expect(page.locator("#interact")).toContainText("Alex", {
    timeout: 20000,
  });
  await page.locator("#interact").click();
  await expect(page.locator("#line")).toContainText("drink, and why");
  await reply(page, "I'm unsure.");
  await expect(page.locator("#feedback")).toContainText("adding a reason");
  await expect(page.locator("#unity-status")).toContainText('"turn":0');
  await reply(page, "I like tea because it helps me relax.");
  await expect(page.locator("#line")).toContainText("this weekend");
  await reply(page, "I'd like to go hiking this weekend.");
  await expect(page.locator("#line")).toContainText("suggest a time");
  await reply(page, "Would you like to meet at ten on Saturday?");
  await expect(page.locator("#unity-status")).toContainText(
    '"phase":"complete"',
  );
  await expect(page.locator("#objective")).toContainText("Cafe visit complete");
  await page.screenshot({
    path: info.outputPath("unity-friend-complete.png"),
    fullPage: true,
  });
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText(
    '"phase":"counter"',
    { timeout: 120000 },
  );
  await expect(page.locator("#unity-status")).toContainText('"practice":false');
});

test("a_real_browser_microphone_capture_submits_and_advances_automatically", async ({
  page,
}) => {
  let bytes = 0;
  await page.route("**/transcribe", async (route) => {
    const wave = route.request().postDataBuffer()!;
    bytes = wave.length;
    expect(wave.subarray(0, 4).toString()).toBe("RIFF");
    expect(normalizeWave(wave).length).toBeGreaterThan(3244);
    await route.fulfill({ json: { text: "One coffee and one tea, please." } });
  });
  await counter(page);
  await page.getByRole("button", { name: "Record reply", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Stop recording", exact: true }),
  ).toBeVisible();
  // Collect more than the companion's minimum 0.1 seconds of PCM audio.
  await page.waitForTimeout(350);
  await page
    .getByRole("button", { name: "Stop recording", exact: true })
    .click();
  await expect(page.locator("#transcript")).toContainText(
    "One coffee and one tea",
  );
  await expect(page.locator("#line")).toContainText("hot or iced");
  expect(bytes).toBeGreaterThan(3244);
});

test("closing_a_pending_permission_rejects_late_microphone_callbacks", async ({
  page,
}) => {
  let uploads = 0;
  await page.route("**/transcribe", (route) => {
    uploads++;
    return route.fulfill({ json: { text: "Stale recording" } });
  });
  await page.addInitScript(() => {
    const original = navigator.mediaDevices.getUserMedia.bind(
      navigator.mediaDevices,
    );
    navigator.mediaDevices.getUserMedia = async (constraints) => {
      const stream = await original(constraints);
      (window as any).lateStream = stream;
      return new Promise<MediaStream>((resolve) => {
        (window as any).allowLateMic = () => resolve(stream);
      });
    };
  });
  await counter(page);
  await page.getByRole("button", { name: "Record reply", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Cancel request", exact: true }),
  ).toBeVisible();
  await expect
    .poll(() => page.evaluate(() => !!(window as any).allowLateMic))
    .toBe(true);
  await page
    .getByRole("button", { name: "Close conversation", exact: true })
    .click();
  await page.evaluate(() => (window as any).allowLateMic());
  await expect
    .poll(() =>
      page.evaluate(() =>
        (window as any).lateStream
          .getTracks()
          .every((track: MediaStreamTrack) => track.readyState === "ended"),
      ),
    )
    .toBe(true);
  expect(uploads).toBe(0);
  await expect(page.locator("#dialogue")).toBeHidden();
  await expect(page.locator("#unity-status")).toContainText('"stage":"order"');
});

for (const [name, output, message] of [
  ["malformed", "not json", "could not be checked"],
  ["too long", JSON.stringify({ text: "a".repeat(401) }), "shorter reply"],
]) {
  test(`transcription_that_is_${name}_recovers_without_advancing`, async ({
    page,
  }) => {
    await page.route("**/transcribe", (route) =>
      route.fulfill({ contentType: "application/json", body: output }),
    );
    await counter(page);
    await page
      .getByRole("button", { name: "Record reply", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Stop recording", exact: true }),
    ).toBeVisible();
    await page.waitForTimeout(350);
    await page
      .getByRole("button", { name: "Stop recording", exact: true })
      .click();
    await expect(page.locator("#status")).toContainText(message);
    await expect(
      page.getByRole("button", { name: "Record reply", exact: true }),
    ).toBeEnabled();
    await expect(page.locator("#unity-status")).toContainText(
      '"stage":"order"',
    );
    await reply(page, "Two coffees, please.");
    await expect(page.locator("#line")).toContainText("hot or iced");
  });
}

test("joining_a_friend_routes_around_the_bar_from_the_players_actual_position", async ({
  page,
}) => {
  await openCafe(page);
  await page.locator("#stage").selectOption("counter");
  const coordinate = async (axis: string) =>
    JSON.parse((await page.locator("#unity-status").textContent())!)[
      axis
    ] as number;
  await page.locator("#unity-canvas").focus();
  // Walk for precise steering; the new controller jogs by default and decelerates smoothly.
  await page.keyboard.down("Control");
  await page.keyboard.down("d");
  await expect
    .poll(() => coordinate("x"), { intervals: [50] })
    .toBeLessThan(1.4);
  await page.keyboard.up("d");
  await page.keyboard.down("w");
  await expect
    .poll(() => coordinate("z"), { intervals: [50] })
    .toBeLessThan(-5.8);
  await page.keyboard.up("w");
  await page.keyboard.down("a");
  await expect
    .poll(() => coordinate("x"), { intervals: [50] })
    .toBeGreaterThan(2.5);
  await page.keyboard.up("a");
  await page.keyboard.up("Control");
  await page.locator("#interact").click();
  await placeOrder(page);
  await page.getByRole("button", { name: "Join Alex", exact: true }).click();
  await expect
    .poll(
      async () =>
        Math.hypot((await coordinate("x")) + 4, (await coordinate("z")) - 2.8),
      { timeout: 18000 },
    )
    .toBeLessThan(0.3);
});

test("an_aborted_assessment_cannot_unlock_a_newer_conversation_request", async ({
  page,
}) => {
  await openCafe(page);
  await page.locator("#stage").selectOption("friend");
  await page.locator("#interact").click();
  const releases: (() => void)[] = [];
  let requests = 0;
  await page.route("**/conversation", async (route) => {
    requests++;
    await new Promise<void>((resolve) => releases.push(resolve));
    await route
      .fulfill({
        json: {
          accepted: true,
          acknowledgement: "That sounds nice.",
          feedback: "Clear reason.",
        },
      })
      .catch(() => {});
  });
  try {
    await reply(page, "I like tea because it tastes good.");
    await expect.poll(() => requests).toBe(1);
    // Real public controls, in one event turn, reproduce a fast close/reopen/reply.
    await page.evaluate(() => {
      document.getElementById("close")!.click();
      document.getElementById("interact")!.click();
      (document.getElementById("reply") as HTMLTextAreaElement).value =
        "I enjoy coffee because it smells lovely.";
      document.getElementById("send")!.click();
    });
    await expect.poll(() => requests).toBe(2);
    await page.waitForTimeout(500); // Let the aborted coroutine resume while request two is held.
    await expect(
      page.getByRole("button", { name: "Send reply", exact: true }),
    ).toBeDisabled();
    await expect(page.locator("#unity-status")).toContainText('"busy":true');
  } finally {
    for (const release of releases) release();
  }
});

test("additive_district_switches_restore_each_world_without_carrying_practice_state", async ({
  page,
}) => {
  await openCafe(page);
  await page.locator("#stage").selectOption("counter");
  await expect(page.locator("#unity-status")).toContainText('"practice":true');
  await page
    .getByRole("button", { name: "Explore Life town", exact: true })
    .click();
  await expect(page.locator("#unity-status")).toContainText('"mode":"life"');
  await page.locator("#stage").selectOption("market");
  await expect(page.locator("#unity-status")).toContainText('"practice":true');
  await page
    .getByRole("button", { name: "Original cafe", exact: true })
    .click();
  await expect(page.locator("#unity-status")).toContainText(
    '"phase":"counter"',
  );
  await expect(page.locator("#unity-status")).toContainText('"practice":false');
  await page
    .getByRole("button", { name: "Explore Life town", exact: true })
    .click();
  await expect(page.locator("#unity-status")).toContainText('"mode":"life"');
  await expect(page.locator("#unity-status")).toContainText('"step":0');
  await expect(page.locator("#unity-status")).toContainText('"practice":false');
});
