import corpus from "../content/corpus.json";
import { validateCorpus } from "../content/validation";
import type { Challenge } from "./catalog";
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
    return [
      {
        id: `open-${excerpt.id}-listen-v1`,
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
    ];
  },
);
