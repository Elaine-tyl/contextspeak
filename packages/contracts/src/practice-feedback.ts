export type AlignedWord = {
  actual: string;
  expected: string;
  status: "correct" | "missing" | "extra" | "replaced";
};
function words(text: string, language?: string | null) {
  const normalized = text
    .normalize("NFKC")
    .replace(/['’‘]/gu, "")
    .replace(/[^\p{L}\p{N}\p{M}\s]/gu, " ");
  return [
    ...new Intl.Segmenter(language ?? undefined, {
      granularity: "word",
    }).segment(normalized),
  ]
    .filter((value) => value.isWordLike)
    .map((value) => value.segment);
}
// Backtracking preserves positions, including repeated words and missing tokens.
export function alignWords(
  expectedText: string,
  actualText: string,
  language?: string | null,
): AlignedWord[] {
  const expected = words(expectedText, language);
  const actual = words(actualText, language);
  const equal = (i: number, j: number) =>
    expected[i].toLowerCase() === actual[j].toLowerCase();
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
        costs[i - 1][j - 1] + Number(!equal(i - 1, j - 1)),
      );
    }
  }
  const result: AlignedWord[] = [];
  let i = expected.length;
  let j = actual.length;
  while (i || j) {
    if (
      i &&
      j &&
      costs[i][j] === costs[i - 1][j - 1] + Number(!equal(i - 1, j - 1))
    ) {
      result.push({
        actual: actual[j - 1],
        expected: expected[i - 1],
        status: equal(i - 1, j - 1) ? "correct" : "replaced",
      });
      i--;
      j--;
    } else if (i && costs[i][j] === costs[i - 1][j] + 1) {
      result.push({ actual: "", expected: expected[--i], status: "missing" });
    } else result.push({ actual: actual[--j], expected: "", status: "extra" });
  }
  return result.reverse();
}
export type SpeakingCorrection = {
  original: string;
  corrected: string;
  reason: string;
};
export function correctionRanges(
  transcript: string,
  corrections: SpeakingCorrection[],
) {
  const ranges: (SpeakingCorrection & { start: number; end: number })[] = [];
  for (const correction of corrections) {
    const start = transcript.indexOf(correction.original);
    const end = start + correction.original.length;
    if (
      !correction.original ||
      correction.original === correction.corrected ||
      /[\p{L}\p{N}\p{M}'’]/u.test(transcript[start - 1] ?? "") ||
      /[\p{L}\p{N}\p{M}'’]/u.test(transcript[end] ?? "") ||
      start < 0 ||
      transcript.indexOf(correction.original, start + 1) >= 0 ||
      ranges.some((range) => start < range.end && end > range.start)
    )
      continue;
    ranges.push({ ...correction, start, end });
  }
  return ranges.sort((a, b) => a.start - b.start);
}
