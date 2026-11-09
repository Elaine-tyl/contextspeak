import { expect, it, vi } from "vitest";
import { deleteAccount, downloadAccount } from "../src/account";
it("requires exact typed DELETE before making any deletion request", async () => {
  const fetcher = vi.fn();
  vi.stubGlobal("fetch", fetcher);
  try {
    for (const confirmation of ["", "delete", " DELETE "])
      await expect(deleteAccount("csrf-a", confirmation)).rejects.toThrow(
        "Type DELETE",
      );
    expect(fetcher).not.toHaveBeenCalled();
  } finally {
    vi.unstubAllGlobals();
  }
});
it("pins export and deletion to the signed-in tab with CSRF and credentials", async () => {
  const fetcher = vi
    .fn()
    .mockResolvedValueOnce(
      new Response('{"schemaVersion":1}', {
        headers: { "Content-Type": "application/json" },
      }),
    )
    .mockResolvedValueOnce(
      new Response(JSON.stringify({ deleted: true, cleanupPending: true }), {
        headers: { "Content-Type": "application/json" },
      }),
    );
  vi.stubGlobal("fetch", fetcher);
  try {
    expect((await downloadAccount("csrf-a")).type).toBe("application/json");
    expect(await deleteAccount("csrf-a", "DELETE")).toEqual({
      deleted: true,
      cleanupPending: true,
    });
    for (const call of fetcher.mock.calls) {
      expect(call[1]).toMatchObject({
        credentials: "include",
        cache: "no-store",
        headers: { "X-CSRF-Token": "csrf-a" },
      });
    }
    expect(fetcher.mock.calls[1][1]).toMatchObject({
      method: "POST",
      body: JSON.stringify({ confirmation: "DELETE" }),
    });
  } finally {
    vi.unstubAllGlobals();
  }
});
it("does not download a stale tab's export after the account cookie changes", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(new Response("Unauthorized", { status: 401 })),
  );
  try {
    await expect(downloadAccount("old-csrf")).rejects.toThrow(
      "session has expired or changed",
    );
  } finally {
    vi.unstubAllGlobals();
  }
});
