import { expect, test, type Page } from "@playwright/test";
import { openWorld } from "./helpers";
import { FixtureSpeechProvider } from "../../../apps/api/src/speech/gemini-speech.provider";

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
async function state(page: Page) {
  return JSON.parse(
    (await page.locator("#unity-status").textContent())?.trim() || "{}",
  );
}
async function ready(page: Page, name?: string) {
  await openWorld(page, "http://127.0.0.1:4320/unity/?world=slice", name);
  await expect.poll(async () => (await state(page)).roomReady).toBe(true);
}

test("mute remembers volume across reloads and still permits recording", async ({
  page,
}, info) => {
  const audio = await new FixtureSpeechProvider().generate();
  await page.route("**/speech", (route) =>
    route.fulfill({
      contentType: "audio/wav",
      body: Buffer.from(audio.audio, "base64"),
    }),
  );
  await page.route("**/transcribe", (route) =>
    route.fulfill({ json: { text: "Some apples, please." } }),
  );
  await page.route("**/slice/review", (route) =>
    route.fulfill({
      json: {
        correction: "",
        explanation: "",
        lexicalTip: "",
        acknowledgement: "Clear.",
        relevant: true,
        assessedGoal: true,
        goalMet: true,
      },
    }),
  );
  await ready(page);
  await command(page, "volume", "0.35");
  await command(page, "mute", "true");
  await command(page, "save");
  await expect(page.locator("#unity-status")).toContainText(
    '"saveState":"saved"',
  );
  await command(page, "panel", "pause");
  await page.screenshot({ path: info.outputPath("audio-settings.png") });
  await page.reload();
  await expect(page.locator("#unity-status")).toContainText('"mode":"slice"', {
    timeout: 120000,
  });
  await expect.poll(async () => (await state(page)).muted).toBe(true);
  expect((await state(page)).volume).toBeCloseTo(0.35);
  expect((await state(page)).listenerVolume).toBe(0);
  await command(page, "visit", "market");
  await command(page, "talk");
  await command(page, "record");
  await expect(page.locator("#unity-status")).toContainText(
    '"recording":"recording"',
  );
  await page.waitForTimeout(300);
  await command(page, "record");
  await expect(page.locator("#unity-status")).toContainText('"lessonStep":1');
  expect((await state(page)).muted).toBe(true);
  await command(page, "mute", "false");
  await expect.poll(async () => (await state(page)).muted).toBe(false);
  expect((await state(page)).listenerVolume).toBeCloseTo(0.35);
  expect((await state(page)).volume).toBeCloseTo(0.35);
});

test("two clients share moving avatars and NPC activity without sharing learning progress", async ({
  browser,
}, info) => {
  const a = await browser.newContext();
  const b = await browser.newContext();
  try {
    const host = await a.newPage();
    const guest = await b.newPage();
    await ready(host, "Emma");
    await ready(guest, "Lucas");
    await command(guest, "character", "Boy");
    await command(host, "room-create");
    await expect
      .poll(async () => (await state(host)).roomCode)
      .toMatch(/^[A-F0-9]{12}$/);
    const code = (await state(host)).roomCode;
    await command(guest, "room-join", code);
    await expect.poll(async () => (await state(host)).roomCount).toBe(2);
    await expect.poll(async () => (await state(guest)).roomCount).toBe(2);
    expect(
      (await state(host)).roomPlayers.some(
        (player: any) => player.look === "Boy",
      ),
    ).toBe(true);
    const original = await state(guest);
    await command(guest, "visit", "park");
    await expect
      .poll(async () => {
        const players = (await state(host)).roomPlayers;
        const peer = players.find((player: any) => player.look === "Boy");
        return Math.hypot(peer.x - original.x, peer.z - original.z);
      })
      .toBeGreaterThan(5);
    await guest.locator("#unity-canvas").click();
    await guest.keyboard.down("w");
    await guest.waitForTimeout(700);
    await guest.keyboard.up("w");
    const moved = await state(guest);
    await expect
      .poll(async () => {
        const peer = (await state(host)).roomPlayers.find(
          (player: any) => player.look === "Boy",
        );
        return Math.hypot(peer.x - moved.x, peer.z - moved.z);
      })
      .toBeLessThan(0.8);
    await command(host, "visit", "park");
    await guest.keyboard.down("d");
    await guest.waitForTimeout(400);
    await guest.keyboard.up("d");
    await host.waitForTimeout(600);
    await host.screenshot({ path: info.outputPath("friend-avatars.png") });
    await command(guest, "visit", "market");
    await command(guest, "talk");
    await expect
      .poll(async () =>
        (await state(host)).roomPlayers.some(
          (player: any) =>
            player.look === "Boy" &&
            player.activity === "talking" &&
            player.npc === "Rose",
        ),
      )
      .toBe(true);
    expect((await state(host)).dialogue).toBe(false);
    await command(host, "panel", "friends");
    await host.screenshot({ path: info.outputPath("friend-room.png") });
    await command(guest, "close");
    await command(guest, "room-leave");
    await expect.poll(async () => (await state(host)).roomCount).toBe(1);
    await expect.poll(async () => (await state(guest)).roomCount).toBe(0);
    expect((await state(host)).roomCode).toBe(code);
    await command(host, "room-leave");
  } finally {
    await a.close();
    await b.close();
  }
});

test("cancelling an in-flight create cleans up the late room and never reconnects", async ({
  page,
}) => {
  let release!: () => void;
  const held = new Promise<void>((resolve) => (release = resolve));
  let issuedCode = "";
  let seen = false;
  await page.route("**/rooms/create", async (route) => {
    const response = await route.fetch();
    issuedCode = (await response.json()).code;
    seen = true;
    await held;
    await route.fulfill({ response });
  });
  await ready(page);
  await command(page, "room-create");
  await expect.poll(() => seen).toBe(true);
  await command(page, "room-leave");
  release();
  await expect
    .poll(async () => {
      const response = await page.request.post(
        "http://127.0.0.1:4330/rooms/join",
        {
          headers: { "X-ContextSpeak-Client": "native" },
          data: {
            name: "Probe",
            look: "Boy",
            world: "ForestSlice",
            code: issuedCode,
            pose: {
              x: 0,
              y: 0,
              z: 0,
              yaw: 0,
              moving: false,
              activity: "exploring",
              npc: "",
            },
          },
        },
      );
      const value = await response.json();
      if (response.status() === 200)
        await page.request.post("http://127.0.0.1:4330/rooms/leave", {
          headers: {
            "X-ContextSpeak-Client": "native",
            Authorization: "Bearer " + value.token,
          },
          data: {},
        });
      return response.status();
    })
    .toBe(404);
  expect((await state(page)).roomCode).toBe("");
  expect((await state(page)).roomCount).toBe(0);
});
