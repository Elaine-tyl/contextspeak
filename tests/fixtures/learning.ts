import type { LearningDetails } from "@contextspeak/contracts";
export const learningFixture: LearningDetails = {
  version: 1,
  kind: "word",
  definition: "Ready to take part.",
  partOfSpeech: "adjective",
  notes: null,
  collocations: [{ source: "available time", translation: "free time" }],
  examples: [{ source: "Are you available?", translation: "Are you free?" }],
  model: "test-model",
  modelDigest: "test-digest",
  promptVersion: 1,
};
