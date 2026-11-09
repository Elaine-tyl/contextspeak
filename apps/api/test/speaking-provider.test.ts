import { expect, it, vi } from "vitest";
import { GeminiSpeakingProvider } from "../src/practice/speaking.provider";
const result = {
  audioQuality: "clear",
  evidence: ["clarity", "fluency", "grammar", "vocabulary", "taskResponse"].map(
    (dimension) => ({
      dimension,
      quote: "I am available on Saturday.",
      explanation: "Evidence from the audible response.",
    }),
  ),
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
  expect(feedback).toMatchObject({ ...result, overall: 80, rubricVersion: 2 });
  const options = fetcher.mock.calls[0][1] as RequestInit;
  const body = JSON.parse(options.body as string);
  expect(body.contents[0].parts[1].inlineData).toEqual({
    mimeType: "audio/webm",
    data: input.audio,
  });
  expect(body.systemInstruction.parts[0].text).toContain("Never score silence");
  expect(body.systemInstruction.parts[0].text).toContain(
    "A single word can fully satisfy a beginner task",
  );
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
    pronunciation: [],
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
    {
      ...result,
      pronunciation: [
        {
          word: "available",
          ipa: "missing IPA slashes",
          stress: "VAIL",
          tip: "Practise.",
        },
      ],
    },
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

it("uses dialogue context and discards invented or ambiguous corrections", async () => {
  let body: any;
  const provider = new GeminiSpeakingProvider({
    apiKey: "fake",
    fetcher: (async (_url, options) => {
      body = JSON.parse(options!.body as string);
      return new Response(
        JSON.stringify({
          candidates: [
            {
              finishReason: "STOP",
              content: {
                parts: [
                  {
                    text: JSON.stringify({
                      ...result,
                      transcript: "I available Saturday. yes yes.",
                      evidence: result.evidence.map((item) => ({
                        ...item,
                        quote: "I available Saturday.",
                      })),
                      corrections: [
                        {
                          original: "I available",
                          corrected: "I am available",
                          reason: "Use a verb.",
                        },
                        {
                          original: "not spoken",
                          corrected: "changed",
                          reason: "Invented.",
                        },
                        {
                          original: "yes",
                          corrected: "no",
                          reason: "Ambiguous.",
                        },
                      ],
                      nextTurn: "Saturday works. Where shall we meet?",
                    }),
                  },
                ],
              },
            },
          ],
        }),
      );
    }) as typeof fetch,
  });
  const feedback = await provider.assess({
    ...input,
    dialogue: {
      turn: 2,
      history: [{ partner: "When?", response: "Saturday." }],
    },
  });
  expect(feedback.corrections).toEqual([
    {
      original: "I available",
      corrected: "I am available",
      reason: "Use a verb.",
    },
  ]);
  expect(feedback.nextTurn).toContain("Where");
  expect(
    JSON.parse(body.contents[0].parts[0].text).dialogue.history[0].response,
  ).toBe("Saturday.");
  expect(
    (await provider.assess({ ...input, dialogue: { turn: 4, history: [] } }))
      .nextTurn,
  ).toBeNull();
});
it("does not fabricate a missing follow-up when generation is incomplete", async () => {
  const provider = new GeminiSpeakingProvider({
    apiKey: "fake",
    fetcher: (async () =>
      new Response(
        JSON.stringify({
          candidates: [
            {
              finishReason: "STOP",
              content: { parts: [{ text: JSON.stringify(result) }] },
            },
          ],
        }),
      )) as typeof fetch,
  });
  await expect(
    provider.assess({ ...input, dialogue: { turn: 1, history: [] } }),
  ).rejects.toMatchObject({ status: 503 });
});

it("returns bounded IPA guidance only for words grounded in the exercise or transcript", async () => {
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
                      pronunciation: [
                        {
                          word: "available",
                          ipa: "/əˈveɪləbəl/",
                          stress: "Stress the second syllable: VAIL.",
                          tip: "Touch your lower lip lightly to your upper teeth for /v/.",
                        },
                        {
                          word: "invented",
                          ipa: "/test/",
                          stress: "Test",
                          tip: "Not in the recording.",
                        },
                      ],
                    }),
                  },
                ],
              },
            },
          ],
        }),
      )) as typeof fetch,
  });
  const feedback = await provider.assess(input);
  expect(feedback.pronunciation).toHaveLength(1);
  expect(feedback.pronunciation![0].ipa).toBe("/əˈveɪləbəl/");
});

it("keeps limited-audio coaching but withholds numeric marks and dialogue advancement", async () => {
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
                      audioQuality: "limited",
                      transcript: "I am available on Saturday.",
                      nextTurn: "Where?",
                    }),
                  },
                ],
              },
            },
          ],
        }),
      )) as typeof fetch,
  });
  const value = await provider.assess({
    ...input,
    dialogue: { turn: 1, history: [] },
  });
  expect(value).toMatchObject({
    overall: null,
    scores: null,
    nextTurn: null,
    rubricVersion: 2,
  });
  expect(value.transcript).toBe(result.transcript);
});
