import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { expect, it } from "vitest";

it("caches the last host snapshot until Godot subscribes and rejects foreign or stale actions", () => {
  let receive: (event: unknown) => void = () => {};
  const parent = {
    postMessage: (data: unknown, origin: string) => sent.push({ data, origin }),
  };
  const sent: unknown[] = [];
  const status = { textContent: "" };
  const window = {
    parent,
    location: { origin: "https://learn.example" },
    addEventListener: (_: string, listener: typeof receive) => {
      receive = listener;
    },
  } as unknown as {
    ContextSpeakArcade: {
      subscribe(callback: (value: string) => void): void;
      emit(action: string, answer: string, roundId: string): void;
    };
  };
  runInNewContext(readFileSync("games/arcade/web/bridge.js", "utf8"), {
    window,
    document: { getElementById: () => status },
  });
  const data = {
    channel: "contextspeak-arcade",
    action: "state",
    state: { round: { id: "round-a" } },
  };
  receive({ data, source: parent, origin: "https://learn.example" });
  const values: string[] = [];
  window.ContextSpeakArcade.subscribe((value) => values.push(value));
  expect(JSON.parse(values[0])).toEqual(data.state);
  receive({
    data: { ...data, state: { round: { id: "evil" } } },
    source: {},
    origin: "https://learn.example",
  });
  receive({ data, source: parent, origin: "https://evil.example" });
  expect(values).toHaveLength(1);
  window.ContextSpeakArcade.emit(
    "submit",
    JSON.stringify({ response: "Hello" }),
    "round-a",
  );
  window.ContextSpeakArcade.emit(
    "submit",
    JSON.stringify({ response: "stale answer" }),
    "old-round",
  );
  expect(sent).toEqual([
    {
      origin: "https://learn.example",
      data: {
        channel: "contextspeak-arcade",
        action: "submit",
        roundId: "round-a",
        answer: { response: "Hello" },
      },
    },
  ]);
});
