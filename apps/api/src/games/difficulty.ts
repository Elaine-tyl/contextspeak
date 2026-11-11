import { gameDifficulty, type GameKind } from "@contextspeak/contracts";

// Match the instructions to the evidence requested by the speaking rubric.
export function speakingRequirements(level: number, kind: GameKind) {
  if (kind === "riddle") {
    return [
      "Give a plausible answer; a different answer is valid if it fits the clues.",
      "Explain how at least two clues support your answer.",
      ...(level >= 8
        ? ["Explain why one alternative answer fits less well."]
        : []),
    ];
  }
  const band = gameDifficulty(level);
  return [
    "Address all parts of the task in a connected answer.",
    "Support your answer with a relevant reason or example.",
    ...(band !== "Intermediate"
      ? ["Consider one alternative or trade-off."]
      : []),
    ...(band === "Advanced" ? ["Qualify one assumption or limitation."] : []),
  ];
}

export function speakingInstruction(level: number, kind: GameKind) {
  return `${speakingRequirements(level, kind).join(" ")} Feedback is automatic after recording.`;
}
