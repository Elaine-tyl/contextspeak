import type { z } from "zod";
import { speakingModelSchema } from "@contextspeak/contracts";
export function assessmentScore(
  value: z.infer<typeof speakingModelSchema>,
): number | null {
  if (!value.speechDetected || value.audioQuality !== "clear") return null;
  if (!value.scores || !/[a-z]/i.test(value.transcript))
    throw new Error("No scorable transcript");
  const dimensions = Object.keys(value.scores);
  if (
    value.evidence.length !== 5 ||
    new Set(value.evidence.map((item) => item.dimension)).size !== 5
  )
    throw new Error("Missing rubric evidence");
  for (const item of value.evidence) {
    if (!dimensions.includes(item.dimension))
      throw new Error("Unknown evidence dimension");
    if (item.quote && !value.transcript.includes(item.quote))
      throw new Error("Ungrounded evidence");
    if (
      !["clarity", "fluency"].includes(item.dimension) &&
      (!item.quote.trim() || !value.transcript.includes(item.quote))
    )
      throw new Error("Ungrounded evidence");
  }
  const score = value.scores;
  const weighted = Math.round(
    score.clarity * 0.2 +
      score.fluency * 0.2 +
      score.grammar * 0.15 +
      score.vocabulary * 0.15 +
      score.taskResponse * 0.3,
  );
  // Fluent but irrelevant speech cannot obtain a passing mark through averaging.
  return Math.min(weighted, score.taskResponse + 20);
}
