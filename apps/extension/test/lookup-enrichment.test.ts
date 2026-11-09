import { expect, it } from "vitest";
import { observeLookup } from "../src/observe-lookup";
import type { LookupReply } from "../src/lookup-flow";
it("renders the base translation before optional enrichment and keeps it on failure", async () => {
  const base: LookupReply = {
    ok: true,
    status: "succeeded",
    meaning: "Available",
  };
  const seen: string[] = [];
  let enrichments = 0;
  await observeLookup(base, {
    active: () => true,
    render: (value) => {
      if (value.ok) seen.push(value.meaning ?? "");
    },
    read: async () => base,
    wait: async () => {},
    enrich: async () => {
      enrichments++;
      expect(seen).toEqual(["Available"]);
      throw new Error("quota");
    },
  });
  expect(seen).toEqual(["Available"]);
  expect(enrichments).toBe(1);
});
it("does not render late enrichment after the panel is removed", async () => {
  const base: LookupReply = {
    ok: true,
    status: "succeeded",
    meaning: "Available",
  };
  let active = true;
  let renders = 0;
  await observeLookup(base, {
    active: () => active,
    render: () => renders++,
    read: async () => base,
    wait: async () => {},
    enrich: async () => {
      active = false;
      return base;
    },
  });
  expect(renders).toBe(1);
  expect(active).toBe(false);
});
it("ignores an unavailable enrichment reply from an older background worker", async () => {
  const base: LookupReply = {
    ok: true,
    status: "succeeded",
    meaning: "Available",
  };
  const rendered: LookupReply[] = [];
  await observeLookup(base, {
    active: () => true,
    render: (value) => rendered.push(value),
    read: async () => base,
    wait: async () => {},
    enrich: async () => ({
      ok: false,
      status: "failed",
      error: "Not permitted",
    }),
  });
  expect(rendered).toEqual([base]);
});
it("keeps the saved base when a malformed enrichment changes its meaning", async () => {
  const base: LookupReply = {
    ok: true,
    status: "succeeded",
    meaning: "Available",
  };
  const seen: LookupReply[] = [];
  await observeLookup(base, {
    active: () => true,
    render: (value) => seen.push(value),
    read: async () => base,
    wait: async () => {},
    enrich: async () => ({ ...base, meaning: "Different" }),
  });
  expect(seen).toEqual([base]);
});
