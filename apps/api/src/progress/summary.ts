import type {
  SkillProgress,
  SpeakingAssessment,
} from "@contextspeak/contracts";
export type ScoredSample = {
  exercise: string;
  score: number;
  independent: boolean;
  scores?: SpeakingAssessment["scores"];
  at: number;
};
export function summarize(
  samples: ScoredSample[],
  unscorable: number,
): SkillProgress {
  const valid = samples.filter(
    (value) =>
      Number.isFinite(value.score) && value.score >= 0 && value.score <= 100,
  );
  const independent = new Map<string, ScoredSample>();
  for (const value of [...valid].sort((a, b) => b.at - a.at))
    if (value.independent && !independent.has(value.exercise))
      independent.set(value.exercise, value);
  const evidence = [...independent.values()];
  const dimensions = [
    "clarity",
    "fluency",
    "grammar",
    "vocabulary",
    "taskResponse",
  ] as const;
  const spoken = evidence.filter((value) => value.scores);
  let focus: SkillProgress["focus"] = null;
  if (spoken.length >= 2) {
    const means = dimensions
      .map((dimension) => ({
        dimension,
        mean:
          spoken.reduce((sum, value) => sum + value.scores![dimension], 0) /
          spoken.length,
      }))
      .sort((a, b) => a.mean - b.mean);
    if (means[0].mean < 80)
      focus = { dimension: means[0].dimension, evidenceCount: spoken.length };
  } else if (
    !valid.some((value) => value.scores) &&
    evidence.length >= 3 &&
    evidence.reduce((sum, value) => sum + value.score, 0) / evidence.length < 80
  )
    focus = { dimension: "listening accuracy", evidenceCount: evidence.length };
  return {
    scored: valid.length,
    average: valid.length
      ? Math.round(
          valid.reduce((sum, value) => sum + value.score, 0) / valid.length,
        )
      : null,
    unscorable,
    focus,
  };
}
