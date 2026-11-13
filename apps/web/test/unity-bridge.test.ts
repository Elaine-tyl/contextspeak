import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { expect, it } from "vitest";

it("Unity's semantic form renders model text literally and preserves recording callback generations", () => {
  const elements = new Map<string, any>();
  const get = (id: string) => {
    if (!elements.has(id))
      elements.set(id, {
        textContent: "",
        value: "",
        disabled: false,
        addEventListener() {},
      });
    return elements.get(id);
  };
  let done: (bytes: Uint8Array) => void = () => {};
  const calls: unknown[] = [];
  const listeners = new Map<string, (event: any) => void>();
  const classes = new Set<string>();
  const window: any = {
    addEventListener(type: string, listener: (event: any) => void) {
      listeners.set(type, listener);
    },
    contextSpeakMicrophone: {
      cancel() {},
      start(_ready: unknown, _meter: unknown, finish: typeof done) {
        done = finish;
      },
    },
  };
  runInNewContext(
    readFileSync(
      "games/unity/Assets/WebGLTemplates/ContextSpeak/cafe.js",
      "utf8",
    ),
    {
      window,
      document: {
        getElementById: get,
        addEventListener() {},
        body: {
          classList: {
            toggle(name: string, enabled: boolean) {
              if (enabled) classes.add(name);
              else classes.delete(name);
            },
          },
        },
      },
      location: { href: "http://127.0.0.1:4320/unity/?world=cafe" },
      history: { replaceState() {} },
      URL,
      btoa: (value: string) => Buffer.from(value, "binary").toString("base64"),
    },
  );
  window.ContextSpeakUnity.instance = {
    SendMessage: (...args: unknown[]) => calls.push(args),
  };
  window.ContextSpeakUnity.state(
    JSON.stringify({
      line: "<img src=x onerror=alert(1)>",
      recording: "recording",
      busy: false,
    }),
  );
  expect(get("line").textContent).toBe("<img src=x onerror=alert(1)>");
  expect(get("reply").disabled).toBe(true);
  expect(get("record").textContent).toBe("Stop recording");
  window.ContextSpeakUnity.record("CafeRuntime", 7);
  done(new Uint8Array([1, 2, 3]));
  let stopped = 0;
  let prevented = 0;
  listeners.get("keydown")!({
    target: { tagName: "TEXTAREA" },
    key: "a",
    stopImmediatePropagation() {
      stopped++;
    },
    preventDefault() {
      prevented++;
    },
  });
  expect(stopped).toBe(1);
  expect(prevented).toBe(0);
  listeners.get("keydown")!({
    target: { tagName: "CANVAS" },
    key: "w",
    stopImmediatePropagation() {
      stopped++;
    },
  });
  expect(stopped).toBe(1);
  expect(calls).toEqual([
    [
      "CafeRuntime",
      "OnMicData",
      JSON.stringify({ generation: 7, value: "AQID" }),
    ],
  ]);
  window.ContextSpeakUnity.state(
    JSON.stringify({ mode: "slice", line: "Native UI owns this line" }),
  );
  expect(classes.has("forest-slice")).toBe(true);
  expect(window.ContextSpeakUnity.owner).toBe("ForestCafeSlice");
  expect(get("line").textContent).toBe("<img src=x onerror=alert(1)>");
  expect(JSON.parse(get("unity-status").textContent).mode).toBe("slice");
});
