import { expect, it, vi } from "vitest";
import { GeminiSpeakingProvider } from "../src/practice/speaking.provider";
const result = {
  speechDetected: true,
  transcript: "I am available on Saturday.",
  scores: {
    clarity: 80,
    fluency: 70,
    grammar: 90,
    vocabulary: 80,
    taskResponse: 80,
  },
  strengths: ["Clear request."],
  improvements: ["Use a complete closing question."],
  suggestedResponse: "I am available on Saturday. Does that work for you?",
};
const input = {
  audio: Buffer.from("test-audio").toString("base64"),
  mimeType: "audio/webm" as const,
  task: {
    expression: "available",
    sourceText: "Are you free this weekend?",
    language: "en",
  },
};
it("sends_inline_audio_with_an_explicit_rubric_and_validates_the_model_result", async () => {
  const fetcher = vi.fn(
    async (_url: unknown, _options?: RequestInit) =>
      new Response(
        JSON.stringify({
          candidates: [
            {
              finishReason: "STOP",
              content: { parts: [{ text: JSON.stringify(result) }] },
            },
          ],
        }),
      ),
  );
  const provider = new GeminiSpeakingProvider({
    apiKey: "fake-key",
    fetcher: fetcher as typeof fetch,
  });
  const feedback = await provider.assess(input);
  expect(feedback).toMatchObject({ ...result, overall: 80, rubricVersion: 1 });
  const options = fetcher.mock.calls[0][1] as RequestInit;
  const body = JSON.parse(options.body as string);
  expect(body.contents[0].parts[1].inlineData).toEqual({
    mimeType: "audio/webm",
    data: input.audio,
  });
  expect(body.systemInstruction.parts[0].text).toContain("Never score silence");
  expect(body.contents[0].parts[0].text).not.toContain("userId");
});
it("returns_no_mark_for_silence_and_maps_quota_without_fabricated_feedback", async () => {
  const provider = new GeminiSpeakingProvider({
    apiKey: "fake",
    fetcher: (async () =>
      new Response(
        JSON.stringify({
          candidates: [
            {
              finishReason: "STOP",
              content: {
                parts: [
                  {
                    text: JSON.stringify({
                      ...result,
                      speechDetected: false,
                      transcript: "",
                      scores: null,
                    }),
                  },
                ],
              },
            },
          ],
        }),
      )) as typeof fetch,
  });
  expect(await provider.assess(input)).toMatchObject({
    speechDetected: false,
    overall: null,
    scores: null,
  });
  const quota = new GeminiSpeakingProvider({
    apiKey: "fake",
    fetcher: (async () => new Response("", { status: 429 })) as typeof fetch,
  });
  await expect(quota.assess(input)).rejects.toMatchObject({ status: 429 });
});

it("rejects_invalid_scores_and_speech_without_a_transcript", async () => {
  for (const invalid of [
    { ...result, scores: { ...result.scores, clarity: 101 } },
    { ...result, transcript: "" },
    { ...result, scores: null },
  ]) {
    const provider = new GeminiSpeakingProvider({
      apiKey: "fake",
      fetcher: (async () =>
        new Response(
          JSON.stringify({
            candidates: [
              {
                finishReason: "STOP",
                content: { parts: [{ text: JSON.stringify(invalid) }] },
              },
            ],
          }),
        )) as typeof fetch,
    });
    await expect(provider.assess(input)).rejects.toMatchObject({ status: 503 });
  }
});
