import { beforeAll, afterAll, describe, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startIssuer } from "../../../tests/helpers/issuer";
import { startDatabase } from "../../../tests/helpers/database";
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let db: Awaited<ReturnType<typeof startDatabase>>;
let app: Awaited<ReturnType<typeof createApp>>;
const base = {
  text: "available",
  context: "Are you available for a quick call?",
  sourceUrl: "https://example.com/article?private=secret#fragment",
  sourceLanguage: "en",
  targetLanguage: "en",
};
async function call(
  method: "get" | "post" | "put",
  path: string,
  user: string,
  body?: Record<string, unknown>,
) {
  const c = request(app.getHttpServer())
    [method](path)
    .set("authorization", `Bearer ${await issuer.token(user)}`);
  return body === undefined ? c : c.send(body);
}
async function enable(user: string) {
  expect(
    (
      await call("put", "/v1/settings", user, {
        enabled: true,
        excludedOrigins: [],
      })
    ).status,
  ).toBe(200);
}
async function capture(user: string, input: Record<string, unknown> = {}) {
  return call("post", "/v1/lookups", user, {
    ...base,
    eventId: randomUUID(),
    ...input,
  });
}
async function result(user: string, id: string) {
  for (let i = 0; i < 50; i++) {
    const r = await call("get", "/v1/lookups?limit=100", user);
    const item = r.body.items?.find((x: any) => x.id === id);
    if (item?.translationStatus !== "pending") return item;
    await new Promise((r) => setTimeout(r, 30));
  }
  throw new Error("Translation did not finish");
}
beforeAll(async () => {
  issuer = await startIssuer();
  db = await startDatabase();
  app = await createApp({
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
    prisma: db.prisma,
  } as any);
});
afterAll(async () => {
  await app?.close();
  await db?.close();
  await issuer?.close();
});
describe("durable owned library", () => {
  it("replay_is_idempotent", async () => {
    await enable("replay");
    const eventId = randomUUID();
    const a = await capture("replay", { eventId });
    const b = await capture("replay", { eventId });
    expect(a.status).toBe(201);
    expect(b.body.lookupId).toBe(a.body.lookupId);
    expect(
      (await call("get", "/v1/lookups", "replay")).body.items,
    ).toHaveLength(1);
    const row = await result("replay", a.body.lookupId);
    expect(row.sourceUrl).toBe("https://example.com/article");
    expect(row.meaning).toBe("Free to take part or ready to be used.");
  });
  it("changed_payload_conflicts", async () => {
    await enable("conflict");
    const eventId = randomUUID();
    await capture("conflict", { eventId });
    expect(
      (await capture("conflict", { eventId, text: "reschedule" })).status,
    ).toBe(409);
  });
  it("same_id_different_owner_is_isolated", async () => {
    await enable("owner-a");
    await enable("owner-b");
    const eventId = randomUUID();
    const a = await capture("owner-a", { eventId });
    const b = await capture("owner-b", { eventId });
    expect(a.body.lookupId).not.toBe(b.body.lookupId);
    expect(
      (await call("get", "/v1/lookups", "owner-a")).body.items,
    ).toHaveLength(1);
    expect(
      (await call("post", `/v1/lookups/${a.body.lookupId}/retry`, "owner-b"))
        .status,
    ).toBe(404);
  });
  it("real_repeat_adds_encounter", async () => {
    await enable("repeat");
    const a = await capture("repeat");
    const b = await capture("repeat");
    expect(a.body.candidateId).toBe(b.body.candidateId);
    expect(
      (await call("get", "/v1/lookups", "repeat")).body.items,
    ).toHaveLength(2);
    expect(
      (await call("get", "/v1/learning-items", "repeat")).body.items[0]
        .encounterCount,
    ).toBe(2);
  });
  it("unresolved_sense_is_preserved", async () => {
    await enable("sense");
    await capture("sense", { text: "bank", context: "The bank of the river." });
    await capture("sense", { text: "bank", context: "I visited the bank." });
    const items = (await call("get", "/v1/learning-items", "sense")).body.items;
    expect(items).toHaveLength(1);
    expect(items[0].senseStatus).toBe("unresolved");
    expect(items[0].status).toBe("candidate");
  });
  it("collection_off_does_not_write", async () => {
    const before = await db.prisma.lookupEvent.count();
    expect((await capture("off")).status).toBe(403);
    const t = await call("post", "/v1/translations", "off", base);
    expect(t.status).toBe(201);
    expect(t.body.meaning).toBe("Free to take part or ready to be used.");
    expect(await db.prisma.lookupEvent.count()).toBe(before);
  });
  it("translation_failure_keeps_lookup", async () => {
    await enable("failure");
    const a = await capture("failure", { text: "unknown-fixture" });
    expect(a.status).toBe(201);
    const item = await result("failure", a.body.lookupId);
    expect(item.translationStatus).toBe("failed");
    expect(item.errorCode).toBe("FIXTURE_NOT_FOUND");
  });
  it("rejects_invalid_input_and_excluded_sites", async () => {
    await enable("validation");
    for (const input of [
      { text: " " },
      { text: "x".repeat(201) },
      { context: "x".repeat(1001) },
      { sourceUrl: "javascript:alert(1)" },
      { userId: "forged" },
    ])
      expect((await capture("validation", input)).status).toBe(400);
    await call("put", "/v1/settings", "validation", {
      enabled: true,
      excludedOrigins: ["https://example.com"],
    });
    expect((await capture("validation")).status).toBe(403);
  });
  it("handles_concurrent_duplicate_delivery", async () => {
    await enable("concurrent");
    const eventId = randomUUID();
    const results = await Promise.all(
      Array.from({ length: 5 }, () => capture("concurrent", { eventId })),
    );
    expect(results.map((x) => x.status)).toEqual([201, 201, 201, 201, 201]);
    expect(new Set(results.map((x) => x.body.lookupId)).size).toBe(1);
  });
  it("paginates_without_exposing_other_owners", async () => {
    await enable("pages");
    await capture("pages");
    await capture("pages");
    const first = await call("get", "/v1/lookups?limit=1", "pages");
    expect(first.body.items).toHaveLength(1);
    expect(first.body.nextCursor).toBeTruthy();
    const second = await call(
      "get",
      `/v1/lookups?limit=1&cursor=${first.body.nextCursor}`,
      "pages",
    );
    expect(second.body.items).toHaveLength(1);
    expect(second.body.items[0].id).not.toBe(first.body.items[0].id);
    expect((await call("get", "/v1/lookups?limit=-1", "pages")).status).toBe(
      400,
    );
  });
});
