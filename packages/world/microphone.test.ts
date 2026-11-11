import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import { expect, it } from "vitest";

it("the real capture worklet averages channels and stops at exactly twenty seconds", async () => {
  let Capture: any;
  const messages: any[] = [];
  runInNewContext(await readFile("games/cafe/web/capture-worklet.js", "utf8"), {
    AudioWorkletProcessor: class {
      port = {
        onmessage: null,
        postMessage: (data: unknown) => messages.push(data),
      };
    },
    sampleRate: 16000,
    registerProcessor: (_name: string, value: unknown) => {
      Capture = value;
    },
  });
  const capture = new Capture();
  const left = new Float32Array(128).fill(0.4);
  const right = new Float32Array(128).fill(-0.2);
  for (let block = 0; block < 2600; block++) capture.process([[left, right]]);
  const chunks = messages.filter((message) => message.samples);
  expect(chunks.reduce((sum, chunk) => sum + chunk.samples.length, 0)).toBe(
    20 * 16000,
  );
  expect(chunks[0].samples[0]).toBeCloseTo(0.1);
  expect(messages.filter((message) => message.done)).toHaveLength(1);
});

it("stopping during microphone permission setup cannot later submit a recording", async () => {
  let permissionRequested!: () => void;
  const requested = new Promise<void>((resolve) => {
    permissionRequested = resolve;
  });
  let resolvePermission!: (value: unknown) => void;
  let stopped = 0;
  let ready = 0;
  let submitted = 0;
  const failures: string[] = [];
  const window: any = { addEventListener() {} };
  runInNewContext(await readFile("games/cafe/web/microphone.js", "utf8"), {
    window,
    navigator: {
      mediaDevices: {
        getUserMedia: () =>
          new Promise((resolve) => {
            resolvePermission = resolve;
            permissionRequested();
          }),
      },
    },
    AudioContext: class {
      async resume() {}
      async close() {}
    },
  });
  const starting = window.contextSpeakMicrophone.start(
    () => ready++,
    () => {},
    () => submitted++,
    (error: string) => failures.push(error),
  );
  await requested;
  window.contextSpeakMicrophone.stop();
  resolvePermission({ getTracks: () => [{ stop: () => stopped++ }] });
  await starting;
  expect(stopped).toBeGreaterThan(0);
  expect(submitted).toBe(0);
  expect(ready).toBe(0);
  expect(failures).toEqual([
    expect.stringContaining("before microphone was ready"),
  ]);
});

it("a 96 kHz default device still produces WAV accepted by the companion", async () => {
  const { normalizeWave } = await import("./protocol");
  let node: any;
  let rate = 0;
  let captured: Uint8Array | undefined;
  const chain = {
    connect() {
      return chain;
    },
    disconnect() {},
  };
  const window: any = { addEventListener() {} };
  runInNewContext(await readFile("games/cafe/web/microphone.js", "utf8"), {
    window,
    navigator: {
      mediaDevices: {
        async getUserMedia() {
          return { getTracks: () => [{ stop() {} }] };
        },
      },
    },
    AudioContext: class {
      sampleRate: number;
      audioWorklet = { async addModule() {} };
      constructor(options?: { sampleRate?: number }) {
        this.sampleRate = options?.sampleRate ?? 96000;
        rate = this.sampleRate;
      }
      async resume() {}
      async close() {}
      createMediaStreamSource() {
        return chain;
      }
      createGain() {
        return { ...chain, gain: { value: 0 } };
      }
    },
    AudioWorkletNode: class {
      port = { onmessage: (_data: any) => {}, postMessage() {} };
      constructor() {
        node = this;
      }
      connect() {
        return chain;
      }
      disconnect() {}
    },
  });
  await window.contextSpeakMicrophone.start(
    () => {},
    () => {},
    (audio: Uint8Array) => {
      captured = audio;
    },
    (error: string) => {
      throw new Error(error);
    },
  );
  node.port.onmessage({
    data: { samples: new Float32Array(rate / 2).fill(0.2) },
  });
  node.port.onmessage({ data: { done: true } });
  const normalized = normalizeWave(Buffer.from(captured!));
  expect(normalized.readUInt32LE(24)).toBe(16000);
  expect(normalized.length).toBe(16044);
});
