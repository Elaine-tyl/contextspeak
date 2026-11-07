import { it, expect } from "vitest";
import {
  ConnectionManager,
  type LocalConnection,
} from "../src/connection-manager";
function deferred() {
  let resolve!: () => void;
  return {
    promise: new Promise<void>((r) => {
      resolve = r;
    }),
    resolve: () => resolve(),
  };
}
const A = {
  token: "token-a",
  identity: "A",
  enabled: true,
  excludedOrigins: ["https://a.example"],
};
const B = {
  token: "token-b",
  identity: "B",
  enabled: false,
  excludedOrigins: ["https://b.example"],
};
for (const stage of ["write", "put"] as const)
  it(`new_connection_wins_when_settings_${stage}_is_pending`, async () => {
    let current: LocalConnection = structuredClone(A);
    const blocked = deferred();
    const started = deferred();
    const remote = new Map([
      ["token-a", structuredClone(A)],
      ["token-b", structuredClone(B)],
    ]);
    const manager = new ConnectionManager({
      read: async () => structuredClone(current),
      write: async (value) => {
        current = structuredClone(value);
        if (stage === "write" && value.identity === "A" && !value.enabled) {
          started.resolve();
          await blocked.promise;
        }
      },
      authenticate: async (token) =>
        structuredClone(token === "token-b" ? B : A),
      putSettings: async (value, token = current.token) => {
        if (stage === "put") {
          started.resolve();
          await blocked.promise;
        }
        remote.set(token, { ...remote.get(token)!, ...value });
      },
    });
    const saving = manager
      .saveSettings({
        enabled: false,
        excludedOrigins: ["https://new-a.example"],
      })
      .catch(() => {});
    await started.promise;
    const connecting = manager.connect("token-b");
    await Promise.resolve();
    await Promise.resolve();
    blocked.resolve();
    await Promise.all([saving, connecting]);
    expect(current).toEqual(B);
    expect(remote.get("token-b")).toEqual(B);
  });
