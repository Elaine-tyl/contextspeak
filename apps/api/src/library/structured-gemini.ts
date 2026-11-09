import { z } from "zod";
import { GEMINI_MODEL, geminiSchema } from "./gemini.provider";
const envelope = z.object({
  modelVersion: z.string().optional(),
  candidates: z
    .array(
      z.object({
        finishReason: z.literal("STOP"),
        content: z.object({
          parts: z.array(
            z.object({
              text: z.string().optional(),
              thought: z.boolean().optional(),
            }),
          ),
        }),
      }),
    )
    .length(1),
});
export async function structuredGemini(
  key: string,
  task: { system: string; input: unknown; schema: object; maxTokens?: number },
  fetcher = fetch,
) {
  if (!key) throw new Error("AI not configured");
  const response = await fetcher(
    `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_MODEL}:generateContent`,
    {
      method: "POST",
      redirect: "error",
      signal: AbortSignal.timeout(45000),
      headers: { "Content-Type": "application/json", "x-goog-api-key": key },
      body: JSON.stringify({
        systemInstruction: { parts: [{ text: task.system }] },
        contents: [
          { role: "user", parts: [{ text: JSON.stringify(task.input) }] },
        ],
        generationConfig: {
          temperature: 0.2,
          maxOutputTokens: task.maxTokens ?? 1800,
          responseMimeType: "application/json",
          responseJsonSchema: geminiSchema(task.schema),
        },
      }),
    },
  );
  if (!response.ok)
    throw Object.assign(new Error(`AI unavailable (${response.status})`), {
      status: response.status,
    });
  const raw = await response.text();
  if (raw.length > 64000) throw new Error("Oversized AI result");
  const result = envelope.parse(JSON.parse(raw));
  const text = result.candidates[0].content.parts
    .filter((part) => !part.thought)
    .map((part) => part.text ?? "")
    .join("");
  return {
    value: JSON.parse(text),
    model: result.modelVersion ?? GEMINI_MODEL,
  };
}
