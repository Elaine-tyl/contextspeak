import { expect, it, vi } from "vitest";
import {
  OllamaSpeechProvider,
  LOCAL_SPEECH_DIGEST,
  LOCAL_SPEECH_MODEL,
} from "../src/speech/ollama-speech.provider";
import {
  validateWav,
  FixtureSpeechProvider,
} from "../src/speech/gemini-speech.provider";
import { SpeechService } from "../src/speech/speech.service";
const library = { actor: async () => ({ id: "learner" }) } as any;
const identity = { issuer: "test", subject: "a" };
it("wraps bounded local PCM as validated WAV and rejects changed models or non-English input", async () => {
  const fetcher = vi.fn(async (url: string, _options?: RequestInit) =>
    url.endsWith("/api/tags")
      ? new Response(
          JSON.stringify({
            models: [{ name: LOCAL_SPEECH_MODEL, digest: LOCAL_SPEECH_DIGEST }],
          }),
        )
      : new Response(new Uint8Array(2400)),
  );
  const provider = new OllamaSpeechProvider({
    fetcher: fetcher as typeof fetch,
  });
  const result = await provider.generate({ text: "Hello", language: "en" });
  validateWav(Buffer.from(result.audio, "base64"));
  expect(JSON.parse(fetcher.mock.calls[1][1]!.body as string)).toMatchObject({
    response_format: "pcm",
    voice: "default",
  });
  expect(fetcher.mock.calls[1][1]!.headers).not.toHaveProperty(
    "x-goog-api-key",
  );
  await expect(
    provider.generate({ text: "你好", language: "zh-Hans" }),
  ).rejects.toMatchObject({ status: 422 });
  const wrong = new OllamaSpeechProvider({
    fetcher: (async () =>
      new Response(JSON.stringify({ models: [] }))) as typeof fetch,
  });
  await expect(
    wrong.generate({ text: "Hi", language: "en" }),
  ).rejects.toMatchObject({ status: 503 });
  expect(
    () => new OllamaSpeechProvider({ url: "https://example.com" }),
  ).toThrow();
});
it("rejects malformed, oversized and failed local audio responses", async () => {
  for (const response of [
    () => new Response(new Uint8Array(3)),
    () => new Response(new Uint8Array(3100000)),
    () => new Response("failed", { status: 500 }),
  ]) {
    const provider = new OllamaSpeechProvider({
      fetcher: (async (url) =>
        String(url).endsWith("/api/tags")
          ? new Response(
              JSON.stringify({
                models: [
                  { name: LOCAL_SPEECH_MODEL, digest: LOCAL_SPEECH_DIGEST },
                ],
              }),
            )
          : response()) as typeof fetch,
    });
    await expect(
      provider.generate({ text: "Hi", language: "en" }),
    ).rejects.toBeDefined();
  }
});
it("queues local generation serially without a six-request-per-minute cap", async () => {
  let active = 0;
  let maximum = 0;
  let calls = 0;
  const service = new SpeechService(library, {
    local: true,
    async generate() {
      active++;
      maximum = Math.max(maximum, active);
      calls++;
      await new Promise((resolve) => setTimeout(resolve, 2));
      active--;
      return new FixtureSpeechProvider().generate();
    },
  });
  for (let batch = 0; batch < 4; batch++)
    await Promise.all(
      [0, 1].map((index) =>
        service.speak(identity, {
          text: `Text ${batch}-${index}`,
          language: "en",
        }),
      ),
    );
  expect(calls).toBe(8);
  expect(maximum).toBe(1);
});
it("bounds the local queue and releases it after provider failure", async () => {
  const releases: (() => void)[] = [];
  const service = new SpeechService(library, {
    local: true,
    async generate() {
      await new Promise<void>((resolve) => releases.push(resolve));
      throw new Error("failed");
    },
  });
  const jobs = [0, 1].map((index) =>
    service
      .speak(identity, { text: `Text ${index}`, language: "en" })
      .catch(() => null),
  );
  await vi.waitFor(() => expect(releases).toHaveLength(1));
  await expect(
    service.speak(identity, { text: "overflow", language: "en" }),
  ).rejects.toMatchObject({ status: 429 });
  for (let index = 0; index < 2; index++) {
    await vi.waitFor(() => expect(releases).toHaveLength(index + 1));
    releases[index]();
  }
  await Promise.all(jobs);
});

it("rejects a third unique local request rather than accepting work beyond the client deadline", async () => {
  let release!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const service = new SpeechService(library, {
    local: true,
    async generate() {
      await gate;
      return new FixtureSpeechProvider().generate();
    },
  });
  const jobs = [0, 1].map((index) =>
    service.speak(identity, { text: `held-${index}`, language: "en" }),
  );
  const third = service.speak(identity, { text: "third", language: "en" }).then(
    () => "accepted",
    () => "rejected",
  );
  const result = await Promise.race([
    third,
    new Promise<string>((resolve) => setTimeout(() => resolve("pending"), 20)),
  ]);
  release();
  await Promise.all(jobs);
  await third;
  expect(result).toBe("rejected");
});
