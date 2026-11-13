import { expect, it } from "vitest";
import { speechInputSchema } from "@contextspeak/contracts";
import { activeChallenges } from "../../api/src/games/catalog";
import { splitSpeechText } from "../../../packages/audio/text";

it("makes every recall passage speakable without truncation or raising the per-request limit", () => {
  const readings = activeChallenges.filter((round) => round.recall);
  expect(readings.some((round) => [...round.audioText].length > 600)).toBe(
    true,
  );
  for (const round of readings) {
    const chunks = splitSpeechText(round.audioText);
    expect(chunks.length, round.id).toBeGreaterThan(0);
    expect(
      chunks.every(
        (text) => speechInputSchema.safeParse({ text, language: "en" }).success,
      ),
      round.id,
    ).toBe(true);
    expect(chunks.join(" ").replace(/\s+/gu, " ")).toBe(
      round.audioText.replace(/\s+/gu, " "),
    );
  }
});
it("splits overlong sentences on word boundaries and respects Unicode codepoints", () => {
  const text = "A café visitor speaks clearly. ".repeat(35).trim();
  const chunks = splitSpeechText(text);
  expect(chunks.length).toBeGreaterThan(1);
  expect(chunks.every((chunk) => [...chunk].length <= 600)).toBe(true);
  expect(chunks.join(" ")).toBe(text);
  const longSentence = `${"meaningful ".repeat(85).trim()}.`;
  expect(splitSpeechText(longSentence).join(" ")).toBe(longSentence);
  const unbroken = "🌿".repeat(700);
  expect(splitSpeechText(unbroken).map((part) => [...part].length)).toEqual([
    600, 100,
  ]);
  expect(splitSpeechText(" ")).toEqual([]);
  expect(splitSpeechText("a".repeat(2001))).toEqual([]);
});
