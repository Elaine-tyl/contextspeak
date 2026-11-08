import { expect, it, vi } from "vitest";
import { GeminiTranslationProvider } from "../src/library/gemini.provider";
const input = {
  text: "library",
  context: "I borrowed a book from the library.",
  sourceLanguage: "en",
  targetLanguage: "zh-Hans",
  sourceUrl: "https://private.example/article",
};
const answer = {
  selection: "library",
  sourceLanguage: "en",
  targetLanguage: "zh-Hans",
  kind: "word",
  meaning: "图书馆",
  definition:
    "通常指供人借阅或阅读书籍的场所，也可指藏书或计算机程序库。这里指图书馆。",
  partOfSpeech: "名词",
  notes: "根据阅读场景区分图书馆与程序库。",
  collocations: [{ source: "public library", translation: "公共图书馆" }],
  examples: [
    {
      source: "I visit the library on weekends.",
      translation: "我周末去图书馆。",
    },
    { source: "She has a library card.", translation: "她有一张借书证。" },
  ],
};
function fixture(
  status = 200,
  payload: any = {
    candidates: [
      {
        finishReason: "STOP",
        content: { parts: [{ text: JSON.stringify(answer) }] },
      },
    ],
    modelVersion: "gemini-3.1-flash-lite",
  },
) {
  const fetcher = vi.fn(
    async () => new Response(JSON.stringify(payload), { status }),
  );
  return {
    fetcher,
    provider: new GeminiTranslationProvider({
      apiKey: "fake-test-key",
      fetcher: fetcher as typeof fetch,
    }),
  };
}
it("uses_official_HTTPS_and_a_server_only_key_header_with_bounded_selection_data", async () => {
  const { provider, fetcher } = fixture();
  const result = await provider.translate(input);
  expect(result).toMatchObject({
    meaning: "图书馆",
    provider: "gemini",
    learningDetails: { examples: answer.examples, modelDigest: null },
  });
  const [url, options] = fetcher.mock.calls[0] as unknown as [
    string,
    RequestInit,
  ];
  expect(url).toBe(
    "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent",
  );
  expect(url).not.toContain("fake-test-key");
  expect(options.headers).toMatchObject({ "x-goog-api-key": "fake-test-key" });
  const body = JSON.parse(options.body as string);
  expect(body.generationConfig.responseMimeType).toBe("application/json");
  expect(
    body.generationConfig.responseJsonSchema.properties.selection.enum,
  ).toEqual(["library"]);
  expect(JSON.stringify(body)).not.toContain("private.example");
  expect(body.systemInstruction.parts[0].text).toContain("common senses");
});
it("requires_a_key_without_attempting_a_network_request", async () => {
  const fetcher = vi.fn();
  await expect(
    new GeminiTranslationProvider({ apiKey: "", fetcher }).translate(input),
  ).rejects.toMatchObject({ code: "GEMINI_NOT_CONFIGURED" });
  expect(fetcher).not.toHaveBeenCalled();
});
it.each([
  [429, "GEMINI_QUOTA_EXCEEDED"],
  [403, "GEMINI_ACCESS_DENIED"],
  [500, "GEMINI_UNAVAILABLE"],
])(
  "handles_HTTP_%s_without_retries_or_exposing_provider_errors",
  async (status, code) => {
    const { provider, fetcher } = fixture(status as number, {
      error: { message: "private secret fake-test-key" },
    });
    await expect(provider.translate(input)).rejects.toMatchObject({ code });
    expect(fetcher).toHaveBeenCalledTimes(1);
  },
);
it.each([
  { candidates: [] },
  {
    candidates: [
      {
        finishReason: "MAX_TOKENS",
        content: { parts: [{ text: JSON.stringify(answer) }] },
      },
    ],
  },
  {
    candidates: [
      {
        finishReason: "STOP",
        content: {
          parts: [
            { text: JSON.stringify({ ...answer, selection: input.context }) },
          ],
        },
      },
    ],
  },
])("rejects_blocked_truncated_or_wrong_scope_results", async (payload) => {
  await expect(
    fixture(200, payload).provider.translate(input),
  ).rejects.toMatchObject({ code: "MODEL_INVALID_RESPONSE" });
});
it("blocks_excess_requests_locally_and_preserves_supported_languages", async () => {
  const { provider, fetcher } = fixture();
  expect((await provider.languages()).length).toBe(7);
  for (let i = 0; i < 5; i++) await provider.translate(input);
  await expect(provider.translate(input)).rejects.toMatchObject({
    code: "GEMINI_QUOTA_EXCEEDED",
  });
  expect(fetcher).toHaveBeenCalledTimes(5);
});

it("translates_to_Vietnamese_with_bilingual_examples_and_target_language_instructions", async () => {
  const vietnamese = {
    ...answer,
    targetLanguage: "vi",
    meaning: "thư viện",
    definition: "Nơi mọi người đọc và mượn sách.",
    partOfSpeech: "danh từ",
    notes: "Dùng để chỉ nơi đọc hoặc mượn sách.",
    collocations: [
      { source: "public library", translation: "thư viện công cộng" },
    ],
    examples: [
      {
        source: "I visit the library on weekends.",
        translation: "Tôi đến thư viện vào cuối tuần.",
      },
      {
        source: "She has a library card.",
        translation: "Cô ấy có thẻ thư viện.",
      },
    ],
  };
  const { provider, fetcher } = fixture(200, {
    candidates: [
      {
        finishReason: "STOP",
        content: { parts: [{ text: JSON.stringify(vietnamese) }] },
      },
    ],
  });
  const result = await provider.translate({ ...input, targetLanguage: "vi" });
  expect(result.meaning).toBe("thư viện");
  expect(result.learningDetails?.examples[0]).toEqual(vietnamese.examples[0]);
  const [, options] = fetcher.mock.calls[0] as unknown as [string, RequestInit];
  const body = JSON.parse(options.body as string);
  expect(body.systemInstruction.parts[0].text).toContain(
    "TARGET: Vietnamese (vi)",
  );
});
