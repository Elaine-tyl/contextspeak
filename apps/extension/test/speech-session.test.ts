import { expect, it } from "vitest";
import { ExtensionSpeech } from "../src/speech-session";
it("never_plays_a_late_response_after_the_card_closes_or_account_changes", async () => {
  let release!: (value: any) => void;
  let identity = "a";
  let plays = 0;
  const speech = new ExtensionSpeech({
    session: async () => ({ identity, token: identity }),
    generate: () =>
      new Promise((resolve) => {
        release = resolve;
      }),
    play: async () => {
      plays++;
      return { ok: true };
    },
    stop: async () => {},
  });
  const input = { text: "academic", language: "en" as const };
  const pending = speech.start("tab-a", "id-a", input);
  await Promise.resolve();
  speech.stop("tab-a", "id-a");
  release({ audio: "wav", mimeType: "audio/wav" });
  expect(await pending).toMatchObject({ cancelled: true });
  expect(plays).toBe(0);
  const switched = speech.start("tab-a", "id-b", input);
  await Promise.resolve();
  identity = "b";
  release({ audio: "wav", mimeType: "audio/wav" });
  expect(await switched).toMatchObject({ cancelled: true });
  expect(plays).toBe(0);
});
it("does_not_let_another_tab_stop_the_active_request", async () => {
  let release!: (value: any) => void;
  let plays = 0;
  const speech = new ExtensionSpeech({
    session: async () => ({ identity: "a", token: "a" }),
    generate: () =>
      new Promise((resolve) => {
        release = resolve;
      }),
    play: async () => {
      plays++;
      return { ok: true };
    },
    stop: async () => {},
  });
  const pending = speech.start("tab-a", "id-a", {
    text: "academic",
    language: "en",
  });
  await Promise.resolve();
  speech.stop("tab-b", "id-a");
  release({ audio: "wav", mimeType: "audio/wav" });
  expect(await pending).toMatchObject({ ok: true });
  expect(plays).toBe(1);
});

it("cancels_pending_speech_when_its_source_tab_leaves", async () => {
  let release!: (value: any) => void;
  let plays = 0;
  const speech = new ExtensionSpeech({
    session: async () => ({ identity: "a", token: "a" }),
    generate: () =>
      new Promise((resolve) => {
        release = resolve;
      }),
    play: async () => {
      plays++;
      return { ok: true };
    },
    stop: async () => {},
  });
  const pending = speech.start("12:0", "request", {
    text: "academic",
    language: "en",
  });
  await Promise.resolve();
  speech.stopTab(12);
  release({ audio: "wav", mimeType: "audio/wav" });
  expect(await pending).toMatchObject({ cancelled: true });
  expect(plays).toBe(0);
});
