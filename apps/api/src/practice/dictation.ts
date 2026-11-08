import type { DictationFeedback } from "@contextspeak/contracts";
function words(text: string, language: string | null) {
  const normalized = text
    .normalize("NFKC")
    .toLowerCase()
    .replace(/['’‘]/gu, "")
    .replace(/[^\p{L}\p{N}\p{M}\s]/gu, " ");
  const segmenter = new Intl.Segmenter(language ?? undefined, {
    granularity: "word",
  });
  return [...segmenter.segment(normalized)]
    .filter((value) => value.isWordLike)
    .map((value) => value.segment);
}
export function gradeDictation(
  transcript: string,
  response: string,
  language: string | null,
): DictationFeedback {
  const expected = words(transcript, language);
  const actual = words(response, language);
  // Full alignment identifies omissions/insertions, rather than comparing word positions.
  // Inputs are bounded by the task's 600-character and answer's 2,000-character limits.
  const costs = Array.from(
    { length: expected.length + 1 },
    () => new Uint16Array(actual.length + 1),
  );
  for (let i = 0; i <= expected.length; i++) costs[i][0] = i;
  for (let j = 0; j <= actual.length; j++) costs[0][j] = j;
  for (let i = 1; i <= expected.length; i++) {
    for (let j = 1; j <= actual.length; j++) {
      costs[i][j] = Math.min(
        costs[i - 1][j] + 1,
        costs[i][j - 1] + 1,
        costs[i - 1][j - 1] + Number(expected[i - 1] !== actual[j - 1]),
      );
    }
  }
  const missing: string[] = [];
  const extra: string[] = [];
  const replacements: DictationFeedback["replacements"] = [];
  let correctWords = 0;
  let i = expected.length;
  let j = actual.length;
  while (i || j) {
    if (
      i &&
      j &&
      costs[i][j] ===
        costs[i - 1][j - 1] + Number(expected[i - 1] !== actual[j - 1])
    ) {
      if (expected[i - 1] === actual[j - 1]) correctWords++;
      else
        replacements.push({ expected: expected[i - 1], actual: actual[j - 1] });
      i--;
      j--;
    } else if (i && costs[i][j] === costs[i - 1][j] + 1)
      missing.push(expected[--i]);
    else extra.push(actual[--j]);
  }
  const errors = costs[expected.length][actual.length];
  return {
    method: "word-error-rate-v1",
    transcript,
    score: expected.length
      ? Math.max(
          0,
          Math.min(
            errors ? 99 : 100,
            Math.round(100 * (1 - errors / expected.length)),
          ),
        )
      : 0,
    correctWords,
    totalWords: expected.length,
    missing: missing.reverse(),
    extra: extra.reverse(),
    replacements: replacements.reverse(),
  };
}
