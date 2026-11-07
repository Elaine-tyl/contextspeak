import { it, expect } from "vitest";
import { observeLookup } from "../src/observe-lookup";
import type { LookupReply } from "../src/lookup-flow";
const queued: LookupReply = { ok: true, status: "queued", eventId: "event" };
it("renders_the_final_allowed_poll_response", async () => {
  const rendered: LookupReply[] = [];
  let reads = 0;
  await observeLookup(queued, {
    render: (reply) => rendered.push(reply),
    active: () => true,
    read: async () =>
      ++reads === 2
        ? { ok: true, status: "succeeded", meaning: "Shared result" }
        : queued,
    wait: async () => {},
    limit: 2,
  });
  expect(rendered.at(-1)).toMatchObject({
    status: "succeeded",
    meaning: "Shared result",
  });
});
it("does_not_read_or_render_after_the_initiating_panel_is_removed", async () => {
  let active = true;
  let reads = 0;
  let rendered = 0;
  await observeLookup(queued, {
    render: () => rendered++,
    active: () => active,
    read: async () => {
      reads++;
      return queued;
    },
    wait: async () => {
      active = false;
    },
    limit: 2,
  });
  expect(reads).toBe(0);
  expect(rendered).toBe(1);
});
