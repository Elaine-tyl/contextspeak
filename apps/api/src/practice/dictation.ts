import { alignWords, type DictationFeedback } from "@contextspeak/contracts";
export function gradeDictation(
  transcript: string,
  response: string,
  language: string | null,
): DictationFeedback {
  const alignment = alignWords(transcript, response, language);
  const correctWords = alignment.filter(
    (word) => word.status === "correct",
  ).length;
  const totalWords = alignment.filter((word) => word.status !== "extra").length;
  const errors = alignment.length - correctWords;
  return {
    method: "word-error-rate-v1",
    transcript,
    score: totalWords
      ? Math.max(
          0,
          Math.min(
            errors ? 99 : 100,
            Math.round(100 * (1 - errors / totalWords)),
          ),
        )
      : 0,
    correctWords,
    totalWords,
    alignment,
    missing: alignment
      .filter((word) => word.status === "missing")
      .map((word) => word.expected.toLowerCase()),
    extra: alignment
      .filter((word) => word.status === "extra")
      .map((word) => word.actual.toLowerCase()),
    replacements: alignment
      .filter((word) => word.status === "replaced")
      .map((word) => ({
        expected: word.expected.toLowerCase(),
        actual: word.actual.toLowerCase(),
      })),
  };
}
