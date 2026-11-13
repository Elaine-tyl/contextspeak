import type { Challenge } from "./catalog";

// Editorial content words, rather than random function words. Each occurs once
// in its passage so a visible repetition cannot accidentally give away a gap.
const keywords: Record<string, string> = {
  "A seat by the window": "table order water",
  "The blue notebook": "library librarian drawer",
  "Rainy-day plans": "Saturday museum weekend",
  "Changing a booking": "employer afternoon ticket",
  "The borrowed bicycle": "lights brakes river",
  "A different entrance": "closed repairs exhibition",
  "The repair estimate": "inspection budget approval",
  "Volunteer shifts": "weekday coordinator packing",
  "An alternative route": "Roadworks transport predictable",
  "A quieter workspace": "concentrating discussion collaborative interruptions",
  "The refill scheme": "packaging weighing illustrated demonstration",
  "Shared equipment": "funding maintaining repairs discarded",
  "Trial before expansion": "uncertain limited timetable targeted",
  "Beyond attendance": "registrations practical evidence substitute",
  "Keeping the service running": "insurance recurring contribution reliance",
  "Cooling the high street": "underground adapting constraint benefit",
  "The convenience trade-off": "administrative telephone efficiency exclusion",
  "A measure that changed behaviour": "rewarded selecting discussions gaming",
  "The consultation gap": "consensus analysts supplementary underrepresented",
  "A contingency, not a prediction":
    "confirmation precaution uncertainty inspection",
  "Interpreting a discrepancy":
    "sampling definitions methodological documented",
  "What the evidence can support":
    "volunteered comparison isolate causal association conclusive",
  "Who bears the burden?":
    "distributional substantially poorer eligible contribution proportional",
  "A qualified endorsement":
    "endorsed conditional accessible credible unconditional qualifications",
  "Independent corroboration":
    "corroborate unpublished multiplicity evidential verification substantiated",
  "A useful but incomplete measure":
    "divergence aspirations headline limitations assumption educational",
  "The risk of a premature remedy":
    "restriction substantial prioritised targeted intervention consumption",
  "Reconciling competing objectives":
    "disused affordable obscured accommodate financing proportionate",
  "The limits of an apparent consensus":
    "assumptions independence synthesis robustness frameworks boundaries",
  "An exception and its implications":
    "procurement favouritism safeguards exceptional discretion justified",
};
const sourceKeywords: Record<string, string[]> = {
  "asp-0001": ["doorway handle frames", "chair fork ate", "house forest years"],
  "voa-ancient-ai-6488034": [
    "historians restore identify",
    "valuable structured societies",
    "technological estimate ceramic",
  ],
  "voa-worker-ai-7296498": [
    "involved Massachusetts business advisors",
    "creativity analytical writing persuasiveness",
    "researchers barriers effectively completing",
  ],
  "voa-writing-ai-7498850": [
    "methods grammar online material",
    "experienced degrees publications discovery",
    "journal prevent understanding project",
  ],
  "voa-carbon-fuel-5090884": [
    "greenhouse climate activities electricity transportation industries",
    "attempted cleaner solutions device reactor converts",
    "pure intense processes costly purification burning",
  ],
};

export function makeReadingRecall(
  original: Challenge,
  words: string[],
): Challenge {
  const text = original.passage!;
  const targets = new Set(words.map((word) => word.toLowerCase()));
  const gapText: string[] = [];
  const gapAnswers: string[] = [];
  let cursor = 0;
  for (const match of text.matchAll(/\p{L}+/gu)) {
    if (!targets.has(match[0].toLowerCase())) continue;
    gapText.push(text.slice(cursor, match.index));
    gapAnswers.push(match[0]);
    cursor = match.index + match[0].length;
  }
  gapText.push(text.slice(cursor));
  if (
    words.length < 3 ||
    words.length > 6 ||
    targets.size !== words.length ||
    gapAnswers.length !== words.length ||
    new Set(gapAnswers.map((word) => word.toLowerCase())).size !== words.length
  )
    throw new Error(`Recall keywords must occur exactly once: ${original.id}`);
  const { choices, answer, explanation, ...base } = original;
  return {
    ...base,
    id: original.id.replace(/-v1$/, "-v2"),
    recall: true,
    audioText: text,
    gapText,
    gapAnswers,
    answer: text,
    instruction:
      "Press Start. Listen and read the highlighted words, then fill them in after the audio ends.",
    explanation:
      "Each highlighted word counts equally. Compare any red answer with its correction; case and punctuation are ignored.",
  };
}

export function readingRecallEditions(readings: Challenge[]): Challenge[] {
  return readings.flatMap((reading) => {
    const index = Number(reading.id.match(/-(\d+)-v1$/)?.[1] ?? 0);
    const selected = reading.source
      ? sourceKeywords[reading.source.id]?.[index]
      : keywords[reading.title];
    if (!selected) throw new Error(`Missing recall keywords: ${reading.id}`);
    return [
      { ...reading, archived: true },
      makeReadingRecall(reading, selected.split(" ")),
    ];
  });
}
