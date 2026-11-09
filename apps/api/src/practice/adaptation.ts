import type {
  PracticeRecommendation,
  SpeakingAssessment,
} from "@contextspeak/contracts";
export type PerformanceSample = {
  exercise: string;
  level: number;
  score: number | null;
  independent: boolean;
  scores?: SpeakingAssessment["scores"];
};
export function practiceExerciseKey(
  mode: string,
  expression: string,
  scenario: string | undefined,
  level: number,
) {
  return JSON.stringify([
    mode,
    expression.normalize("NFKC").trim().toLowerCase().replace(/\s+/g, " "),
    scenario ?? "word",
    level,
  ]);
}
export function recommendPractice(
  samples: PerformanceSample[],
  maxLevel = 3,
): PracticeRecommendation {
  const usable = samples.filter(
    (sample) =>
      sample.score !== null &&
      Number.isFinite(sample.score) &&
      sample.score >= 0 &&
      sample.score <= 100 &&
      sample.level >= 1 &&
      sample.level <= maxLevel,
  );
  let level = 1;
  // Promotion needs different independent tasks, never repeated attempts at one answer.
  while (level < maxLevel) {
    const passed = new Set(
      usable
        .filter(
          (sample) =>
            sample.level === level && sample.independent && sample.score! >= 80,
        )
        .map((sample) => sample.exercise),
    );
    if (passed.size < 3) break;
    level++;
  }
  const seen = new Set<string>();
  const recent = usable
    .filter((sample) => {
      if (seen.has(sample.exercise)) return false;
      seen.add(sample.exercise);
      return true;
    })
    .slice(0, 3);
  const struggling = recent.filter((sample) => sample.score! < 55).length >= 2;
  if (struggling) level = Math.max(1, level - 1);
  const dimensions = recent.filter((sample) => sample.scores);
  let focus: string | undefined;
  if (dimensions.length >= 2) {
    const keys = [
      "clarity",
      "fluency",
      "grammar",
      "vocabulary",
      "taskResponse",
    ] as const;
    const weakest = [...keys].sort((a, b) =>
      dimensions.reduce(
        (sum, sample) => sum + sample.scores![a] - sample.scores![b],
        0,
      ),
    )[0];
    if (
      dimensions.reduce((sum, sample) => sum + sample.scores![weakest], 0) /
        dimensions.length <
      80
    )
      focus = weakest;
  }
  return {
    level,
    evidenceCount: new Set(usable.map((sample) => sample.exercise)).size,
    reason: struggling
      ? "Two recent different tasks were difficult. Try a shorter, more supported task."
      : level > 1
        ? "Three independent 80+ results at each prior level support a harder task."
        : "Build evidence across three different independent tasks before increasing difficulty.",
    ...(focus ? { focus } : {}),
  };
}
