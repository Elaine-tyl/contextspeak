import { expect, it } from "vitest";
import { completePairing } from "../src/pairing";
it("revokes_a_delivered_credential_if_disconnect_wins_during_local_connection_write", async () => {
  let current = true,
    active = "",
    revoked = "";
  let release!: () => void, started!: () => void;
  const blocked = new Promise<void>((r) => {
    release = r;
  });
  const writing = new Promise<void>((r) => {
    started = r;
  });
  const completing = completePairing("issued-token", {
    current: async () => current,
    connect: async (token) => {
      started();
      await blocked;
      active = token;
    },
    revoke: async (token) => {
      revoked = token;
      active = "";
    },
  });
  await writing;
  current = false;
  release();
  expect(await completing).toBe(false);
  expect(active).toBe("");
  expect(revoked).toBe("issued-token");
});
it("revokes_credentials_when_local_connection_validation_fails", async () => {
  let revoked = "";
  await expect(
    completePairing("issued-token", {
      current: async () => true,
      connect: async () => {
        throw new Error("offline");
      },
      revoke: async (value) => {
        revoked = value;
      },
    }),
  ).rejects.toThrow("offline");
  expect(revoked).toBe("issued-token");
});
