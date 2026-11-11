import corpus from "../content/corpus.json";
import { validateCorpus } from "../content/validation";
import type { Challenge } from "./catalog";
import { speakingInstruction, speakingRequirements } from "./difficulty";
import { makeGapFill } from "./gap-fill";
const reviewed = validateCorpus(corpus);
const sources = new Map(reviewed.sources.map((source) => [source.id, source]));
export const openChallenges: Challenge[] = reviewed.excerpts.flatMap(
  (excerpt, index) => {
    const original = sources.get(excerpt.sourceId)!;
    const { fetchUrl, ...source } = original;
    const requirements =
      excerpt.level <= 2
        ? [
            "Give a short relevant phrase or sentence. Extra explanation is not required.",
          ]
        : excerpt.level <= 5
          ? [
              "Answer the question relevantly",
              "Give one concrete detail or reason when requested",
            ]
          : [
              "Answer all parts of the question",
              "Connect your ideas with a reason or example",
            ];
    const news = !!original.publishedAt;
    return [
      {
        id: `open-${excerpt.id}-listen-v1`,
        ...(!news ? { archived: true } : {}),
        track: "listening",
        kind: "dictation",
        level: excerpt.level,
        topic: excerpt.topic,
        title: `${excerpt.topic === "story" ? original.title : excerpt.topic} · listening ${index + 1}`,
        instruction: "Listen, then type exactly what you hear.",
        audioText: excerpt.text,
        answer: excerpt.text,
        explanation:
          "Compare your answer with this credited excerpt. Case and punctuation are ignored.",
        source,
      },
      {
        id: `open-${excerpt.id}-speak-v1`,
        archived: true,
        track: "speaking",
        kind: "response",
        level: excerpt.level,
        topic: excerpt.topic,
        title: `${excerpt.topic === "story" ? original.title : excerpt.topic} · speaking ${index + 1}`,
        instruction:
          "Listen to the excerpt and question, then answer aloud. Feedback is automatic after recording.",
        audioText: `${excerpt.text}\n\n${excerpt.question}`,
        answer:
          "An open response addressing the question in the learner’s own words. Accept relevant personal details and multiple valid answers.",
        criteria: requirements,
        explanation:
          "Answer the question in your own words. Repeating the excerpt is not required.",
        source,
      },
      ...(news
        ? [
            {
              id: `open-${excerpt.id}-speak-v2`,
              track: "speaking" as const,
              kind: "response" as const,
              level: excerpt.level,
              topic: excerpt.topic,
              title: `${excerpt.topic} · discussion`,
              instruction: speakingInstruction(excerpt.level, "response"),
              audioText: `${excerpt.text}\n\n${excerpt.question} ${speakingRequirements(excerpt.level, "response").join(" ")}`,
              answer:
                "A reasoned response to the question. Accept relevant personal perspectives, alternative solutions and qualified disagreement.",
              criteria: speakingRequirements(excerpt.level, "response"),
              explanation:
                "Answer the question in your own words, with the stated reasoning. The article is context, not a script to repeat.",
              source,
            },
          ]
        : []),
      ...(excerpt.gapTemplate
        ? [
            makeGapFill(
              excerpt.gapTemplate,
              `open-${excerpt.id}-gap-v1`,
              excerpt.level,
              excerpt.topic,
              source,
            ),
          ]
        : []),
    ];
  },
);
