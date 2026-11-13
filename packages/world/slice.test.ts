import { afterEach, expect, it } from "vitest";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import type { AddressInfo } from "node:net";
import type { Server } from "node:http";
import { createWorldServer } from "./server";
import {
  reviewSlice,
  SLICE_BANDS,
  sliceInput,
  sliceInstruction,
  type SliceInput,
} from "./slice";

const native = { "X-ContextSpeak-Client": "native" };
let server: Server | undefined;
let directory = "";

async function start(sliceReview?: (input: unknown) => Promise<unknown>) {
  directory = await mkdtemp(join(tmpdir(), "cs-slice-test-"));
  await writeFile(join(directory, "index.html"), "Forest Café");
  server = createWorldServer({
    webRoot: directory,
    transcribe: async () => "Could I get an iced latte?",
    interpret: async () => ({ action: "confirm" }),
    converse: async () => ({
      accepted: true,
      acknowledgement: "Nice.",
      feedback: "Clear.",
    }),
    sliceReview,
    speak: async () => Buffer.alloc(0),
  });
  await new Promise<void>((resolve) => server!.listen(0, "127.0.0.1", resolve));
  return `http://127.0.0.1:${(server!.address() as AddressInfo).port}`;
}

afterEach(async () => {
  if (server)
    await new Promise<void>((resolve) => server!.close(() => resolve()));
  server = undefined;
  if (directory) await rm(directory, { recursive: true, force: true });
});

const input: SliceInput = {
  band: "IELTS 6.0–6.5",
  npc: "Rowan, a waiter at a forest café",
  task: "ordering at a café",
  npcLine: "What can I get you?",
  text: "Can I has a latte?",
};

it("accepts the complete 3–9 training range without breaking old clients", () => {
  for (const band of ["IELTS 3", "IELTS 4", "IELTS 9", "IELTS 6.0–6.5"]) {
    expect(sliceInput.safeParse({ ...input, band }).success).toBe(true);
  }
  expect(SLICE_BANDS).toContain("IELTS 3");
  expect(sliceInput.safeParse({ ...input, band: "IELTS 10" }).success).toBe(
    false,
  );
});

it("passes an explicit scene goal to the reviewer without requiring connector words", () => {
  const request = {
    ...input,
    goal: "Explain a choice with a relevant reason.",
  };
  expect(sliceInput.safeParse(request).success).toBe(true);
  expect(sliceInstruction(request)).toContain(
    "Explain a choice with a relevant reason.",
  );
  const result = reviewSlice(request, {
    relevant: true,
    correction: "",
    explanation: "",
    lexicalTip: "",
    acknowledgement: "Fair point.",
    assessedGoal: true,
    goalMet: true,
  });
  expect(result.goalMet).toBe(true);
  expect(result.assessedGoal).toBe(true);
  expect(
    reviewSlice(request, {
      ...result,
      relevant: false,
      assessedGoal: false,
      goalMet: false,
    }).assessedGoal,
  ).toBe(true);
});

it("accepts only IELTS bands and short replies", () => {
  expect(sliceInput.safeParse(input).success).toBe(true);
  expect(sliceInput.safeParse({ ...input, band: "B2" }).success).toBe(false);
  expect(
    sliceInput.safeParse({ ...input, text: "x".repeat(401) }).success,
  ).toBe(false);
  expect(sliceInput.safeParse({ ...input, unlock: true }).success).toBe(false);
});

it("never returns pronunciation claims or echoes a valid answer as a correction", () => {
  const echoed = reviewSlice(
    { ...input, text: "Could I get a latte, please?" },
    {
      relevant: true,
      correction: "could I get a latte please",
      explanation: "Good.",
      lexicalTip: "",
      acknowledgement: "Sure!",
    },
  );
  expect(echoed.correction).toBe("");
  expect(echoed.explanation).toBe("");
  const acoustic = reviewSlice(input, {
    relevant: true,
    correction: "Can I have a latte?",
    explanation: "Your pronunciation of 'latte' was unclear.",
    lexicalTip: "Work on your accent.",
    acknowledgement: "Coming right up!",
  });
  expect(acoustic.correction).toBe("Can I have a latte?");
  expect(acoustic.explanation).toBe("");
  expect(acoustic.lexicalTip).toBe("");
  expect(sliceInstruction(input)).toContain("IELTS 6.0–6.5");
  expect(sliceInstruction(input)).toContain("never comment on pronunciation");
});

it("keeps ordinary grammar corrections that contain sounds like", () => {
  const result = reviewSlice(
    { ...input, text: "It sound like a good idea." },
    {
      relevant: true,
      correction: "It sounds like a good idea.",
      explanation: "Use sounds with it.",
      lexicalTip: "",
      acknowledgement: "Sure!",
    },
  );
  expect(result.correction).toBe("It sounds like a good idea.");
  expect(
    reviewSlice(
      { ...input, text: "Your accent sound familiar." },
      {
        relevant: true,
        correction: "Your accent sounds familiar.",
        explanation: "Use sounds.",
        lexicalTip: "",
        acknowledgement: "OK",
      },
    ).correction,
  ).toBe("Your accent sounds familiar.");
});

it("protects the slice review endpoint and rejects unsafe model output", async () => {
  let calls = 0;
  const url = await start(async () => {
    calls++;
    return calls === 1
      ? {
          relevant: true,
          correction: "Can I have a latte?",
          explanation: "Use have after can.",
          lexicalTip: "",
          acknowledgement: "Sure!",
        }
      : {
          relevant: true,
          correction: "",
          explanation: "",
          lexicalTip: "",
          acknowledgement: "Sure!",
          balance: 99999,
        };
  });
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const send = (value: unknown, authorized = true) =>
    fetch(url + "/slice/review", {
      method: "POST",
      headers: {
        ...native,
        ...(authorized ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(value),
    });
  expect((await send(input, false)).status).toBe(401);
  expect((await send({ ...input, band: "C1" })).status).toBe(400);
  expect(calls).toBe(0);
  const ok = await send(input);
  expect(ok.status).toBe(200);
  expect((await ok.json()).correction).toBe("Can I have a latte?");
  expect((await send(input)).status).toBe(502);
});

it("reports a clear error when the companion was started without slice feedback", async () => {
  const url = await start(undefined);
  const { token } = await (
    await fetch(url + "/session", { headers: native })
  ).json();
  const response = await fetch(url + "/slice/review", {
    method: "POST",
    headers: { ...native, Authorization: `Bearer ${token}` },
    body: JSON.stringify(input),
  });
  expect(response.status).toBe(503);
});
