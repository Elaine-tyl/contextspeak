import { it, expect } from "vitest";
import { toggleCollection } from "../src/collection";
for (const action of ["switch", "disconnect"] as const)
  it(`does_not_mutate_a_different_session_after_${action}`, async () => {
    let session = { token: "A", generation: 1 };
    const remote = new Map([
      ["A", { enabled: true, excludedOrigins: ["https://a.example"] }],
      ["B", { enabled: false, excludedOrigins: ["https://b.example"] }],
    ]);
    let release!: () => void;
    let entered!: () => void;
    const gate = new Promise<void>((r) => {
      release = r;
    });
    const started = new Promise<void>((r) => {
      entered = r;
    });
    let applied = 0;
    const task = toggleCollection({
      session: () => session,
      get: async (token) => {
        entered();
        await gate;
        return remote.get(token)!;
      },
      put: async (token, value) => {
        remote.set(token, value);
        return value;
      },
      apply: () => {
        applied++;
      },
    });
    await started;
    session = { token: action === "switch" ? "B" : "", generation: 2 };
    release();
    await task;
    expect(remote.get("B")).toEqual({
      enabled: false,
      excludedOrigins: ["https://b.example"],
    });
    expect(remote.has("")).toBe(false);
    expect(applied).toBe(0);
  });
