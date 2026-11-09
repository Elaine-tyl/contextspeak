import { expect, it } from "vitest";
import { SpeechService } from "../src/speech/speech.service";
import type { LibraryService } from "../src/library/library.service";
const input = { text: "private sentence", language: "en" as const };
const audio = { audio: "fake", mimeType: "audio/wav" as const };
it("purges deleted-owner speech without clearing another owner", async () => {
  let calls = 0;
  const library = {
    actor: async (identity: { subject: string }) => ({ id: identity.subject }),
  } as unknown as LibraryService;
  const speech = new SpeechService(library, {
    generate: async () => {
      calls++;
      return audio;
    },
  });
  const a = { issuer: "test", subject: "a" },
    b = { issuer: "test", subject: "b" };
  await speech.speak(a, input);
  await speech.speak(b, input);
  speech.forgetUser("a");
  await speech.speak(b, input);
  await speech.speak(a, input);
  expect(calls).toBe(3);
});
it("does not recache speech when a deleted owner's provider work completes late", async () => {
  let release!: () => void;
  let entered!: () => void;
  let calls = 0;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const started = new Promise<void>((resolve) => {
    entered = resolve;
  });
  const speech = new SpeechService(
    { actor: async () => ({ id: "a" }) } as unknown as LibraryService,
    {
      generate: async () => {
        calls++;
        entered();
        await gate;
        return audio;
      },
    },
  );
  const identity = { issuer: "test", subject: "a" };
  const pending = speech.speak(identity, input);
  await started;
  speech.forgetUser("a");
  release();
  await pending;
  await speech.speak(identity, input);
  expect(calls).toBe(2);
});
it("does not let forgotten work overwrite a newer speech generation", async () => {
  const gates = [0, 1].map(() => {
    let release!: () => void;
    let entered!: () => void;
    const wait = new Promise<void>((resolve) => {
      release = resolve;
    });
    const started = new Promise<void>((resolve) => {
      entered = resolve;
    });
    return { release, entered, wait, started };
  });
  let calls = 0;
  const speech = new SpeechService(
    { actor: async () => ({ id: "a" }) } as unknown as LibraryService,
    {
      generate: async () => {
        const index = calls++;
        gates[index].entered();
        await gates[index].wait;
        return { ...audio, audio: String(index) };
      },
    },
  );
  const identity = { issuer: "test", subject: "a" };
  const old = speech.speak(identity, input);
  await gates[0].started;
  speech.forgetUser("a");
  const newer = speech.speak(identity, input);
  await gates[1].started;
  gates[0].release();
  await old;
  const coalesced = speech.speak(identity, input);
  gates[1].release();
  await newer;
  expect((await coalesced).audio).toBe("1");
});
