import type { GameKind } from "@contextspeak/contracts";

/** Visual themes never contain question text, expected answers or grading rules. */
export const studioThemes = {
  "scene-words": {
    name: "The living dictionary",
    accent: "#8758d9",
    soft: "#f1eaff",
    glyph: "⌂",
    hint: "Explore the room, play the AI sentence and type the missing word. Press Enter to check. Your word notebook opens after grading; scenes and effects never affect the mark.",
  },
  dictation: {
    name: "The listening desk",
    accent: "#26705e",
    soft: "#e6f1e9",
    glyph: "✎",
    hint: "Replay when needed. Type the complete sentence before checking.",
  },
  "gap-fill": {
    name: "The crossword garden",
    accent: "#7250a1",
    soft: "#f0eafa",
    glyph: "▦",
    hint: "One word per blank. Complete a word to move forward; Enter checks the last word.",
  },
  picture: {
    name: "The picture gallery",
    accent: "#376fba",
    soft: "#e5effb",
    glyph: "▧",
    hint: "Listen for the details, select one picture, then check your choice.",
  },
  comprehension: {
    name: "The story room",
    accent: "#a05a35",
    soft: "#fbefdf",
    glyph: "☷",
    hint: "Listen for the main idea and details. Select the answer supported by the audio.",
  },
  response: {
    name: "The conversation studio",
    accent: "#26715c",
    soft: "#e4f2eb",
    glyph: "◉",
    hint: "Listen to the question, record your own reply, then stop. Feedback arrives automatically.",
  },
  riddle: {
    name: "The mystery desk",
    accent: "#785198",
    soft: "#f1e9f6",
    glyph: "?",
    hint: "Listen to the clue. Record your answer and explain your reasoning.",
  },
  puzzle: {
    name: "The solution workshop",
    accent: "#a66024",
    soft: "#fff1dd",
    glyph: "◇",
    hint: "Listen to the problem. Explain a practical solution aloud, using the requirements below.",
  },
  pk: {
    name: "The speaking arena",
    accent: "#b45744",
    soft: "#fbe9e4",
    glyph: "★",
    hint: "Record your response to the challenge. Echo is a fixed training target, not a live player.",
  },
  spelling: {
    name: "The letter factory",
    accent: "#9b631a",
    soft: "#fff4d9",
    glyph: "Aa",
    hint: "Listen to the word. Tap letter tiles in order; tap a placed tile to remove it.",
  },
  "word-order": {
    name: "The sentence railway",
    accent: "#267985",
    soft: "#e0f2f3",
    glyph: "↔",
    hint: "Listen to the sentence. Assemble the word tiles in order; Clear starts your answer again.",
  },
  "minimal-pairs": {
    name: "The sound lab",
    accent: "#4d61b1",
    soft: "#e9edfc",
    glyph: "≈",
    hint: "Listen carefully to the sound difference. Select the word you heard, then check.",
  },
  "true-false": {
    name: "The quick-fire console",
    accent: "#9d494e",
    soft: "#fbe8eb",
    glyph: "ϟ",
    hint: "Listen to the statement about the picture. The timer starts after playback; select true or false.",
  },
  "word-meaning": {
    name: "The word collection",
    accent: "#ac6427",
    soft: "#fff0df",
    glyph: "Aa",
    hint: "Read the word, compare the definitions and choose the closest meaning. No audio is needed.",
  },
  "word-context": {
    name: "The context workshop",
    accent: "#947020",
    soft: "#fbf3d9",
    glyph: "◇",
    hint: "Use the clues around the blank to choose a word. Check your answer to see the explanation.",
  },
  "reading-question": {
    name: "The reading room",
    accent: "#3e7491",
    soft: "#e7f2f7",
    glyph: "☷",
    hint: "Read the passage, then select the answer supported by its details or overall argument. No audio is needed.",
  },
  "reading-order": {
    name: "The story arranger",
    accent: "#6f61a2",
    soft: "#efeaf9",
    glyph: "↔",
    hint: "Tap paragraphs in a logical order. Tap a placed paragraph to undo it, or Clear to start again. Use every paragraph before checking.",
  },
  "word-scramble": {
    name: "The letter vault",
    accent: "#a76228",
    soft: "#fff0dd",
    glyph: "Aa",
    hint: "Solve the definition clue by placing every letter. Tap a placed letter to undo it, or Clear to restart. Hyphens are omitted.",
  },
  "mini-crossword": {
    name: "The crossing garden",
    accent: "#786198",
    soft: "#f0e9f8",
    glyph: "▦",
    hint: "Type a word for each clue. Click a grid square to focus its clue. Shared letters should agree; a red crossing marks a conflict, not which word is correct.",
  },
  "evidence-hunt": {
    name: "The clue trail",
    accent: "#347965",
    soft: "#e5f3ec",
    glyph: "⌕",
    hint: "Read the complete passage first. Under Choose the evidence, select the sentence that best supports your answer, then check it.",
  },
  "sentence-puzzle": {
    name: "The story workshop",
    accent: "#4e729b",
    soft: "#e8f0fa",
    glyph: "↔",
    hint: "Read the passage first, then use every word tile to build the reference summary. Tap a placed tile to undo it. The mark checks this supplied sentence, not all possible summaries.",
  },
} satisfies Record<
  GameKind,
  { name: string; accent: string; soft: string; glyph: string; hint: string }
>;

export type StudioActivity =
  "ready" | "listening" | "answering" | "recording" | "checking" | "checked";
export const activityLabels: Record<StudioActivity, string> = {
  ready: "Listen first",
  listening: "Audio playing",
  answering: "Your turn",
  recording: "Recording",
  checking: "Checking",
  checked: "Answer saved",
};
