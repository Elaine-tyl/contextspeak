import { expect, it, vi } from "vitest";
import { SpeakingService } from "../src/practice/speaking.service";
import { FixtureSpeakingProvider } from "../src/practice/speaking.provider";
import { FixtureSpeechProvider } from "../src/speech/gemini-speech.provider";
it("reserves_an_event_before_asynchronous_task_lookup", async () => {
  let stored: any = null;
  let release: (() => void) | undefined;
  const task = {
    itemId: "item",
    lookupId: "lookup",
    version: 1,
    expression: "available",
    sourceText: "Are you available?",
    translation: "free",
    language: "en",
    recent: [],
  };
  const load = vi
    .fn()
    .mockResolvedValueOnce(task)
    .mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          release = () => resolve(task);
        }),
    );
  const findUnique = vi.fn(async () => stored);
  const tx = {
    $queryRaw: vi.fn(async () => []),
    lookupEvent: { findFirst: vi.fn(async () => ({ id: "lookup" })) },
    practiceAttempt: {
      findFirst: vi.fn(async () => null),
      findUnique,
      create: vi.fn(async ({ data }) => {
        stored = { ...data, id: "attempt", createdAt: new Date() };
        return stored;
      }),
    },
  };
  const prisma = {
    practiceAttempt: { findUnique },
    $transaction: async (work: (tx: unknown) => unknown) => work(tx),
  };
  const assess = vi.fn(() => new FixtureSpeakingProvider().assess());
  const service = new SpeakingService(
    prisma as any,
    { actor: async () => ({ id: "user" }) } as any,
    { task: load } as any,
    { assess },
  );
  const input = {
    eventId: "event",
    lookupId: "lookup",
    consent: true as const,
    replayed: true as const,
    revealed: false,
    durationMs: 2000,
    mimeType: "audio/wav" as const,
    audio: (await new FixtureSpeechProvider().generate()).audio,
  };
  const identity = { issuer: "issuer", subject: "subject" };
  const first = service.submit(identity, "item", input);
  const second = service.submit(identity, "item", input);
  const saved = await first;
  release?.();
  expect(await second).toEqual(saved);
  expect(assess).toHaveBeenCalledTimes(1);
});
