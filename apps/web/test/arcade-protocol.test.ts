import { describe, expect, it } from "vitest";
import type { GameRound } from "@contextspeak/contracts";
import { readArcadeAction } from "../app/games/arcadeProtocol";

const frame = {};
const origin = "http://127.0.0.1:3000";
const round: GameRound = {
  id: "dictation-a",
  kind: "dictation",
  track: "listening",
  level: 1,
  title: "Listen",
  instruction: "Type what you hear.",
  audioText: "Please wait here.",
};
function read(
  data: unknown,
  game = round,
  source: unknown = frame,
  sender = origin,
) {
  return readArcadeAction(
    { data, source, origin: sender },
    origin,
    frame,
    game,
  );
}
function message(action: string, answer?: unknown) {
  return {
    channel: "contextspeak-arcade",
    action,
    roundId: round.id,
    ...(answer ? { answer } : {}),
  };
}
describe("arcade host boundary", () => {
  it("accepts only its exact frame, origin, channel and current round", () => {
    expect(read(message("play"))?.action).toBe("play");
    expect(read(message("play"), round, {})).toBeNull();
    expect(
      read(message("play"), round, frame, "https://evil.example"),
    ).toBeNull();
    expect(read({ ...message("play"), channel: "other" })).toBeNull();
    expect(read({ ...message("play"), roundId: "previous" })).toBeNull();
    expect(
      readArcadeAction(
        { data: message("play"), source: null, origin },
        origin,
        null,
        round,
      ),
    ).toBeNull();
    expect(
      read({ channel: "contextspeak-arcade", action: "ready" })?.action,
    ).toBe("ready");
  });
  it("rejects grading, identity, event IDs and arbitrary extra fields", () => {
    for (const key of ["score", "token", "eventId", "completedPlays", "url"]) {
      expect(read({ ...message("play"), [key]: "injected" })).toBeNull();
    }
    expect(
      read(message("submit", { response: "Hi", eventId: "x" })),
    ).toBeNull();
    expect(read(message("evaluate"))).toBeNull();
  });
  it("bounds text and refuses cross-kind or empty answers", () => {
    expect(
      read(message("submit", { response: " Please wait here. " })),
    ).toMatchObject({ answer: { response: "Please wait here." } });
    for (const response of ["", "   ", "!", "x".repeat(2001)]) {
      expect(read(message("submit", { response }))).toBeNull();
    }
    expect(read(message("submit", { choice: "a" }))).toBeNull();
    expect(read(message("submit", { gapAnswers: ["hi"] }))).toBeNull();
  });
  it("accepts only the current choices and exact number of bounded gaps", () => {
    const picture = {
      ...round,
      kind: "picture",
      choices: [{ id: "bridge", label: "Bridge", picture: "bridge" }],
    } as GameRound;
    expect(read(message("submit", { choice: "bridge" }), picture)?.action).toBe(
      "submit",
    );
    expect(read(message("submit", { choice: "book" }), picture)).toBeNull();
    const gaps = {
      ...round,
      kind: "gap-fill",
      gapText: ["The ", " is ", "."],
    } as GameRound;
    expect(
      read(message("submit", { gapAnswers: ["proposal", "ready"] }), gaps)
        ?.action,
    ).toBe("submit");
    for (const gapAnswers of [
      ["one"],
      ["one", ""],
      ["x".repeat(81), "two"],
      Array(7).fill("x"),
    ]) {
      expect(read(message("submit", { gapAnswers }), gaps)).toBeNull();
    }
    expect(read(message("submit", { response: "proposal" }), gaps)).toBeNull();
    expect(
      read(message("submit", { response: "Hi" }), {
        ...round,
        track: "speaking",
        kind: "response",
      }),
    ).toBeNull();
  });
});
