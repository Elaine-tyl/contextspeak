import { expect, it, vi } from "vitest";
import type { PrismaClient } from "@prisma/client";
import type { LibraryService } from "../src/library/library.service";
import { ExpansionService } from "../src/expansion/expansion.service";
it("keeps generation shared until the owner-checked cache publication finishes", async () => {
  let release!: () => void;
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const user = { id: "owned-user", consentVersion: 0 };
  const tx = {
    $queryRaw: async () => [],
    user: { findUnique: async () => user },
  };
  const transaction = vi.fn(async (work: (tx: unknown) => Promise<unknown>) => {
    await gate;
    return work(tx);
  });
  const expand = vi.fn(async () => ({
    model: "fixture",
    synonyms: [],
    lookalikes: [],
  }));
  const service = new ExpansionService(
    { $transaction: transaction } as unknown as PrismaClient,
    { actor: async () => user } as unknown as LibraryService,
    { expand },
  );
  const identity = { issuer: "fixture", subject: "learner" };
  const request = {
    input: { text: "form", sourceLanguage: "en", targetLanguage: "en" },
    meaning: "A document",
  };
  const first = service.get(identity, request);
  await vi.waitFor(() => expect(transaction).toHaveBeenCalledTimes(1));
  const second = service.get(identity, request);
  try {
    await vi.waitFor(() => expect(transaction).toHaveBeenCalledTimes(2));
    expect(expand).toHaveBeenCalledTimes(1);
  } finally {
    release();
    await Promise.all([first, second]);
  }
  await service.get(identity, request);
  expect(expand).toHaveBeenCalledTimes(1);
});
