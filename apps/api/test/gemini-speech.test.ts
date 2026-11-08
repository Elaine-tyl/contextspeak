import { expect, it, vi, afterEach } from "vitest";
import {
  GeminiSpeechProvider,
  FixtureSpeechProvider,
} from "../src/speech/gemini-speech.provider";
import { SpeechService } from "../src/speech/speech.service";
afterEach(() => vi.restoreAllMocks());
function envelope(audio: string, mimeType = "audio/wav") {
  return new Response(
    JSON.stringify({
      candidates: [
        {
          finishReason: "STOP",
          content: { parts: [{ inlineData: { data: audio, mimeType } }] },
        },
      ],
    }),
  );
}
it("keeps_the_transcript_separate_from_style_and_accepts_valid_bounded_WAV", async () => {
  const wave = await new FixtureSpeechProvider().generate();
  let body: any;
  const provider = new GeminiSpeechProvider({
    apiKey: "test-key",
    fetcher: async (_url, options) => {
      body = JSON.parse(String(options?.body));
      expect(new Headers(options?.headers).get("x-goog-api-key")).toBe(
        "test-key",
      );
      return envelope(wave.audio);
    },
  });
  expect(
    await provider.generate({
      text: "Academic research is useful.",
      language: "en",
    }),
  ).toEqual(wave);
  expect(body.contents[0].parts[0].text).toBe("Academic research is useful.");
  expect(body.generationConfig.speechConfig.voiceConfig.voice).toBe("Kore");
  expect(JSON.stringify(body)).not.toContain("test-key");
});
it("rejects_non_WAV_malformed_and_oversized_provider_audio_and_surfaces_quota", async () => {
  for (const response of [
    envelope("YWJj"),
    envelope("YWJj", "text/html"),
    new Response("x".repeat(4300001)),
  ]) {
    const provider = new GeminiSpeechProvider({
      apiKey: "test-key",
      fetcher: async () => response,
    });
    await expect(
      provider.generate({ text: "academic", language: "en" }),
    ).rejects.toMatchObject({ status: 502 });
  }
  const limited = new GeminiSpeechProvider({
    apiKey: "test-key",
    fetcher: async () => new Response("", { status: 429 }),
  });
  await expect(
    limited.generate({ text: "academic", language: "en" }),
  ).rejects.toMatchObject({ status: 429 });
});
it("expires_and_evicts_audio_without_reusing_another_users_entry", async () => {
  let now = 100000;
  vi.spyOn(Date, "now").mockImplementation(() => now);
  let calls = 0;
  const service = new SpeechService(
    { actor: async (identity: any) => ({ id: identity.subject }) } as any,
    {
      async generate() {
        calls++;
        return { audio: "a".repeat(100), mimeType: "audio/wav" };
      },
    },
  );
  const identity = { issuer: "test", subject: "a" };
  const input = { text: "first", language: "en" as const };
  await service.speak(identity, input);
  await service.speak(identity, input);
  expect(calls).toBe(1);
  now += 86400001;
  await service.speak(identity, input);
  expect(calls).toBe(2);
  for (let index = 0; index < 101; index++) {
    now += 61000;
    await service.speak(identity, { text: String(index), language: "en" });
  }
  now += 61000;
  await service.speak(identity, input);
  expect(calls).toBe(104);
  await service.speak({ ...identity, subject: "b" }, input);
  expect(calls).toBe(105);
});

it("rejects_a_RIFF_container_without_a_valid_PCM_format", async () => {
  const wave = await new FixtureSpeechProvider().generate();
  const broken = Buffer.from(wave.audio, "base64");
  broken.write("JUNK", 12);
  const provider = new GeminiSpeechProvider({
    apiKey: "test-key",
    fetcher: async () => envelope(broken.toString("base64")),
  });
  await expect(
    provider.generate({ text: "academic", language: "en" }),
  ).rejects.toMatchObject({ status: 502 });
});
