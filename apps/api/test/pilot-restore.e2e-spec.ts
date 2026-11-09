import { expect, it } from "vitest";
import { runSyntheticRestore } from "../../../scripts/pilot-check";

it("restores a synthetic pre-deletion snapshot then replays a later independent tombstone ledger", async () => {
  expect(await runSyntheticRestore()).toEqual({
    snapshotUsers: 2,
    restoredUsers: 1,
    restoredItems: 1,
    restoredLookups: 1,
    restoredSessions: 1,
    replayedDeletions: 1,
  });
});
