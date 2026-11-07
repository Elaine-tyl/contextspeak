import { afterAll, beforeAll, expect, it } from "vitest";
import { createServer } from "node:http";
import { LibreTranslateProvider } from "../src/library/libretranslate.provider";
let url = "",
  status = 200,
  malformed = false,
  delay = false;
const requests: Record<string, unknown>[] = [];
const server = createServer(async (req, res) => {
  if (req.url === "/languages")
    return res.end(
      JSON.stringify([
        { code: "en", name: "English", targets: ["en", "zh"] },
        { code: "zh", name: "Chinese", targets: ["en", "zh"] },
      ]),
    );
  let body = "";
  for await (const chunk of req) body += chunk;
  requests.push(JSON.parse(body));
  if (delay) return;
  res.statusCode = status;
  res.end(
    malformed
      ? '{"translatedText":null}'
      : JSON.stringify({
          translatedText: "你好，世界。",
          detectedLanguage: { language: "en", confidence: 95 },
        }),
  );
});
beforeAll(async () => {
  await new Promise<void>((r) => server.listen(0, "127.0.0.1", r));
  const a = server.address();
  if (!a || typeof a === "string") throw new Error("Missing address");
  url = `http://127.0.0.1:${a.port}`;
});
afterAll(async () => {
  server.closeAllConnections();
  await new Promise<void>((r) => server.close(() => r()));
});
const input = {
  text: "Hello, world.",
  sourceLanguage: "auto",
  targetLanguage: "zh",
  context: "Private context",
  sourceUrl: "https://private.example/page",
};
it("translates_arbitrary_sentences_and_sends_only_selected_text_with_requested_languages", async () => {
  const provider = new LibreTranslateProvider({ url });
  expect(await provider.translate(input)).toMatchObject({
    meaning: "你好，世界。",
    provider: "libretranslate-local",
    detectedSourceLanguage: "en",
  });
  expect(requests.at(-1)).toEqual({
    q: "Hello, world.",
    source: "auto",
    target: "zh",
    format: "text",
  });
  expect((await provider.languages()).map((x) => x.code)).toEqual(["en", "zh"]);
});
it("reports_unsupported_pairs_quota_failures_and_invalid_responses_without_leaking_provider_details", async () => {
  const provider = new LibreTranslateProvider({ url });
  await expect(
    provider.translate({ ...input, targetLanguage: "ja" }),
  ).rejects.toMatchObject({ code: "UNSUPPORTED_LANGUAGE" });
  status = 429;
  await expect(provider.translate(input)).rejects.toMatchObject({
    code: "TRANSLATOR_BUSY",
  });
  status = 500;
  await expect(provider.translate(input)).rejects.toMatchObject({
    code: "TRANSLATION_FAILED",
  });
  status = 200;
  malformed = true;
  await expect(provider.translate(input)).rejects.toMatchObject({
    code: "INVALID_TRANSLATION_RESPONSE",
  });
  malformed = false;
});
it("bounds_request_time_and_reports_offline_service", async () => {
  delay = true;
  await expect(
    new LibreTranslateProvider({ url, timeoutMs: 50 }).translate(input),
  ).rejects.toMatchObject({ code: "TRANSLATION_TIMEOUT" });
  delay = false;
  await expect(
    new LibreTranslateProvider({
      url: "http://127.0.0.1:1",
      timeoutMs: 100,
    }).translate(input),
  ).rejects.toMatchObject({ code: "TRANSLATOR_OFFLINE" });
});
