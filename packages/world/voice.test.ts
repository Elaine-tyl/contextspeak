import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import { expect, it } from "vitest";

it("decodes, connects and starts an NPC voice before reporting playback", async () => {
  const events: string[] = [];
  let source: any;
  const window: any = { addEventListener() {} };
  runInNewContext(await readFile("games/cafe/web/voice.js", "utf8"), {
    window,
    AbortController,
    fetch: async (_url: string, init: RequestInit) => {
      expect(init.headers).toHaveProperty(
        "Authorization",
        "Bearer test-session",
      );
      return {
        ok: true,
        async arrayBuffer() {
          return new ArrayBuffer(48);
        },
      };
    },
    AudioContext: class {
      destination = {};
      async resume() {
        events.push("resume");
      }
      async decodeAudioData() {
        events.push("decode");
        return {};
      }
      createBufferSource() {
        source = {
          buffer: null,
          onended: null,
          connect() {
            events.push("connect");
          },
          start() {
            events.push("start");
          },
          stop() {},
          disconnect() {},
        };
        return source;
      }
    },
  });
  await window.contextSpeakVoice.speak(
    "test-session",
    "Hello!",
    () => events.push("started"),
    () => events.push("ended"),
    () => events.push("failed"),
  );
  expect(events).toEqual(["resume", "decode", "connect", "start", "started"]);
  source.onended();
  expect(events.at(-1)).toBe("ended");
});

it("stopped voice requests cannot play or trigger a late journey transition", async () => {
  let resolveAudio!: (value: ArrayBuffer) => void;
  const received = new Promise<ArrayBuffer>((resolve) => {
    resolveAudio = resolve;
  });
  const events: string[] = [];
  const window: any = { addEventListener() {} };
  runInNewContext(await readFile("games/cafe/web/voice.js", "utf8"), {
    window,
    AbortController,
    fetch: async () => ({ ok: true, arrayBuffer: () => received }),
    AudioContext: class {
      async resume() {}
      async decodeAudioData() {
        return {};
      }
      createBufferSource() {
        throw new Error("Canceled audio must never reach the speakers");
      }
    },
  });
  const request = window.contextSpeakVoice.speak(
    "test-session",
    "Hello!",
    () => events.push("started"),
    () => events.push("ended"),
    () => events.push("failed"),
  );
  await Promise.resolve();
  window.contextSpeakVoice.stop();
  resolveAudio(new ArrayBuffer(48));
  await request;
  expect(events).toEqual([]);
});
