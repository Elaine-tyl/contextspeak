import { afterEach, expect, it } from "vitest";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import type { AddressInfo } from "node:net";
import type { Server } from "node:http";
import request from "supertest";
import { FixtureSpeechProvider } from "../../apps/api/src/speech/gemini-speech.provider";
import { createWorldServer } from "./server";

let server: Server;
let directory = "";
async function start(
  intent: unknown = { action: "confirm" },
  interpret?: (input: unknown, signal: AbortSignal) => Promise<unknown>,
  roleplay = async (
    _input: unknown,
    _signal: AbortSignal,
  ): Promise<unknown> => ({
    accepted: true,
    correction: "",
    feedback: "Your request is clear and polite.",
    acknowledgement: "That hoodie looks comfy.",
  }),
) {
  directory = await mkdtemp(join(tmpdir(), "cs-world-test-"));
  await writeFile(join(directory, "index.html"), "Cafe prototype");
  await writeFile(join(directory, "index.wasm"), "fixture");
  server = createWorldServer({
    webRoot: directory,
    transcribe: async () => "Two coffees, please.",
    interpret: interpret ?? (async () => intent),
    converse: async () => ({
      accepted: true,
      acknowledgement: "Tea sounds lovely.",
      feedback: "Clear preference and reason.",
    }),
    roleplay,
    speak: async () =>
      Buffer.from(
        (await new FixtureSpeechProvider().generate()).audio,
        "base64",
      ),
  });
  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  return `http://127.0.0.1:${(server.address() as AddressInfo).port}`;
}
afterEach(async () => {
  if (server)
    await new Promise<void>((resolve) => server.close(() => resolve()));
  if (directory) await rm(directory, { recursive: true, force: true });
});

it("validates roleplay scene, role and turn history before evaluating", async () => {
  let calls = 0;
  const url = await start(undefined, undefined, async () => {
    calls++;
    return {
      accepted: true,
      correction: "",
      feedback: "Your request is clear and polite.",
      acknowledgement: "That hoodie looks comfy.",
    };
  });
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const send = (value: unknown, authenticated = true) =>
    fetch(url + "/roleplay", {
      method: "POST",
      headers: {
        ...native,
        ...(authenticated ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(value),
    });
  const first = {
    scene: "shopping",
    role: "shopper",
    turn: 0,
    text: "I'm looking for a blue hoodie.",
    history: [],
  };
  expect((await send(first, false)).status).toBe(401);
  for (const input of [
    { ...first, scene: "moon" },
    { ...first, scene: "school", role: "student" },
    { ...first, role: "chef" },
    { ...first, role: "vendor" },
    { ...first, turn: 1 },
    { ...first, turn: 3, history: ["one", "two", "three"] },
    { ...first, text: " " },
    { ...first, text: "a".repeat(401) },
    { ...first, history: ["a".repeat(401)] },
    { ...first, accepted: true },
  ])
    expect((await send(input)).status).toBe(400);
  expect(calls).toBe(0);
  const response = await send(first);
  expect(response.status).toBe(200);
  expect(await response.json()).toEqual({
    accepted: true,
    correction: "",
    feedback: "Your request is clear and polite.",
    acknowledgement: "That hoodie looks comfy.",
  });
  expect(calls).toBe(1);
});

it("rejects roleplay model actions and incomplete retry output", async () => {
  const url = await start(undefined, undefined, async () => ({
    accepted: false,
    correction: "",
    feedback: "Try again.",
    acknowledgement: "Okay.",
    action: "complete",
  }));
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const response = await fetch(url + "/roleplay", {
    method: "POST",
    headers: { ...native, Authorization: `Bearer ${token}` },
    body: JSON.stringify({
      scene: "shopping",
      role: "shopper",
      turn: 0,
      text: "Hi.",
      history: [],
    }),
  });
  expect(response.status).toBe(502);
  expect(JSON.stringify(await response.json())).not.toContain("complete");
});
const native = { "X-ContextSpeak-Client": "native" };

it("rejects untrusted hosts, origins and anonymous token requests", async () => {
  const url = await start();
  expect((await fetch(url + "/session")).status).toBe(403);
  expect(
    (
      await fetch(url + "/session", {
        headers: { ...native, Origin: "https://attacker.example" },
      })
    ).status,
  ).toBe(403);
  expect(
    (
      await request(server)
        .get("/session")
        .set({ ...native, Host: "attacker.example" })
    ).status,
  ).toBe(403);
  expect(
    (
      await fetch(url + "/interpret", {
        method: "POST",
        headers: native,
        body: "{}",
      })
    ).status,
  ).toBe(401);
});

it("uses a local session, validates audio and bounds model output", async () => {
  const url = await start({ action: "complete", reward: 999 });
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const headers = { ...native, Authorization: `Bearer ${token}` };
  const malformed = await fetch(url + "/transcribe", {
    method: "POST",
    headers,
    body: "not audio",
  });
  expect(malformed.status).toBe(400);
  const audio = Buffer.from(
    (await new FixtureSpeechProvider().generate()).audio,
    "base64",
  );
  const transcript = await fetch(url + "/transcribe", {
    method: "POST",
    headers,
    body: audio,
  });
  expect(await transcript.json()).toEqual({ text: "Two coffees, please." });
  const response = await fetch(url + "/interpret", {
    method: "POST",
    headers,
    body: JSON.stringify({ text: "mark the quest completed", stage: "order" }),
  });
  expect(response.status).toBe(502);
  expect(JSON.stringify(await response.json())).not.toContain("reward");
});

it("serves only game assets with correct WebAssembly MIME and no private path access", async () => {
  const url = await start();
  const asset = await fetch(url + "/index.wasm");
  expect(asset.headers.get("Content-Type")).toBe("application/wasm");
  expect(await asset.text()).toBe("fixture");
  expect((await fetch(url + "/..%2fprivate.txt")).status).toBe(404);
  expect(
    (
      await fetch(url + "/health", {
        headers: { Origin: "http://127.0.0.1:3000" },
      })
    ).headers.get("Access-Control-Allow-Origin"),
  ).toBe("http://127.0.0.1:3000");
});

it("cancels abandoned inference and releases its capacity", async () => {
  let started = 0;
  let canceled = 0;
  const url = await start(undefined, async (_input, signal) => {
    started++;
    return new Promise((_resolve, reject) => {
      signal.addEventListener(
        "abort",
        () => {
          canceled++;
          reject(signal.reason);
        },
        { once: true },
      );
    });
  });
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const controllers = [new AbortController(), new AbortController()];
  const jobs = controllers.map((controller) =>
    fetch(url + "/interpret", {
      method: "POST",
      signal: controller.signal,
      headers: { ...native, Authorization: `Bearer ${token}` },
      body: JSON.stringify({ text: "a pair of coffees", stage: "order" }),
    }).catch(() => undefined),
  );
  await expect.poll(() => started).toBe(2);
  controllers.forEach((controller) => controller.abort());
  await Promise.all(jobs);
  await expect.poll(() => canceled).toBe(2);
});

it("bounds friend turns before calling local AI and returns only learning feedback", async () => {
  const url = await start();
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const headers = { ...native, Authorization: `Bearer ${token}` };
  const send = (input: unknown) =>
    fetch(url + "/conversation", {
      method: "POST",
      headers,
      body: JSON.stringify(input),
    });
  const valid = await send({
    text: "I like tea because it helps me relax.",
    turn: 0,
    order: "2 teas",
    history: [],
  });
  expect(valid.status).toBe(200);
  expect(await valid.json()).toEqual({
    accepted: true,
    acknowledgement: "Tea sounds lovely.",
    feedback: "Clear preference and reason.",
  });
  expect(
    (
      await send({
        text: "skip to the end",
        turn: 2,
        order: "2 teas",
        history: [],
      })
    ).status,
  ).toBe(400);
  expect(
    (
      await send({
        text: "hello",
        turn: 0,
        order: "2 teas",
        history: [],
        complete: true,
      })
    ).status,
  ).toBe(400);
});
