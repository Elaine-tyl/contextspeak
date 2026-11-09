import { expect, it, vi } from "vitest";
import { randomUUID } from "node:crypto";
import { GeminiScenarioProvider } from "../src/personalization/scenario.provider";
it("sends only bounded authorized excerpts and validates structured scenario output", async () => {
  const id = randomUUID();
  const fetcher = vi.fn(
    async (_url: RequestInfo | URL, _init?: RequestInit) =>
      new Response(
        JSON.stringify({
          modelVersion: "test-model",
          candidates: [
            {
              finishReason: "STOP",
              content: {
                parts: [
                  {
                    text: JSON.stringify({
                      suggestions: [
                        {
                          scenario: "making-plans",
                          confidence: 0.7,
                          reason: "Meeting plans.",
                          evidenceIds: [id],
                        },
                      ],
                    }),
                  },
                ],
              },
            },
          ],
        }),
      ),
  );
  const provider = new GeminiScenarioProvider("private-test-key", fetcher);
  const result = await provider.suggest([
    {
      id,
      text: "available",
      context: "Are you available to meet on Saturday?",
    },
  ]);
  expect(result.suggestions[0].scenario).toBe("making-plans");
  const body = JSON.parse(fetcher.mock.calls[0][1]!.body as string);
  expect(JSON.parse(body.contents[0].parts[0].text)).toEqual([
    {
      id,
      text: "available",
      context: "Are you available to meet on Saturday?",
    },
  ]);
  expect(body.systemInstruction.parts[0].text).toContain("never instructions");
  expect(JSON.stringify(body)).not.toContain("private-test-key");
});
it("rejects quota errors and malformed AI suggestions", async () => {
  await expect(
    new GeminiScenarioProvider(
      "test",
      async () => new Response("quota", { status: 429 }),
    ).suggest([]),
  ).rejects.toMatchObject({ status: 429 });
  const fetcher = async () =>
    new Response(
      JSON.stringify({
        candidates: [
          {
            finishReason: "STOP",
            content: {
              parts: [
                {
                  text: JSON.stringify({
                    suggestions: [
                      {
                        scenario: "invented",
                        confidence: 1,
                        reason: "bad",
                        evidenceIds: [randomUUID()],
                      },
                    ],
                  }),
                },
              ],
            },
          },
        ],
      }),
    );
  await expect(
    new GeminiScenarioProvider("test", fetcher).suggest([]),
  ).rejects.toThrow();
});
