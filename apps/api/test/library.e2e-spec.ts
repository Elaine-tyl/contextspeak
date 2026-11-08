import { learningFixture } from "../../../tests/fixtures/learning";
import { FixtureTranslationProvider } from "../src/library/translation.provider";
import { beforeAll, afterAll, describe, expect, it } from "vitest";
import request from "supertest";
import { randomUUID } from "node:crypto";
import { createApp } from "../src/app";
import { startIssuer } from "../../../tests/helpers/issuer";
import { startDatabase } from "../../../tests/helpers/database";
let issuer: Awaited<ReturnType<typeof startIssuer>>;
let db: Awaited<ReturnType<typeof startDatabase>>;
const fixtureProvider = new FixtureTranslationProvider();
const providerCalls = new Map<string, number>();
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
    provider: {
      async translate(input: any) {
        const key = input.context ?? "";
        providerCalls.set(key, (providerCalls.get(key) ?? 0) + 1);
        const result = await fixtureProvider.translate(input);
        return key === "shared-rich"
          ? { ...result, learningDetails: learningFixture }
          : result;
      },
    },
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
      { text: "x".repeat(2001) },
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

it("shared_translation_reads_and_replays_do_not_call_provider_again", async () => {
  const context = "Shared result regression";
  await enable("shared");
  const eventId = randomUUID();
  const captured = await capture("shared", { eventId, context });
  const id = captured.body.lookupId;
  await result("shared", id);
  for (let i = 0; i < 3; i++) {
    const shared = await call("get", `/v1/lookups/${id}/translation`, "shared");
    expect(shared.status).toBe(200);
    expect(shared.body.meaning).toBe("Free to take part or ready to be used.");
    expect(shared.body.translationStatus).toBe("succeeded");
  }
  const replay = await capture("shared", { eventId, context });
  expect(replay.body.lookupId).toBe(id);
  expect(providerCalls.get(context)).toBe(1);
  expect(
    (await call("get", `/v1/lookups/${id}/translation`, "not-owner")).status,
  ).toBe(404);
  const history = (await call("get", "/v1/lookups", "shared")).body.items[0];
  expect(history.meaning).toBe("Free to take part or ready to be used.");
});

it("worker_translates_with_the_saved_language_pair_instead_of_hardcoded_English", async () => {
  await enable("language-snapshot");
  const receipt = await capture("language-snapshot", {
    sourceLanguage: "auto",
    targetLanguage: "zh",
  });
  expect(receipt.status).toBe(201);
  const translated = await result("language-snapshot", receipt.body.lookupId);
  expect(translated).toMatchObject({
    sourceLanguage: "auto",
    targetLanguage: "zh",
    translationStatus: "failed",
    errorCode: "UNSUPPORTED_PAIR",
  });
});
it("stores_translation_preferences_per_account_and_rejects_invalid_languages", async () => {
  const value = {
    sourceLanguage: "auto",
    targetLanguage: "en",
    autoTranslate: false,
  };
  expect(
    (await call("put", "/v1/translation-preferences", "preferences-a", value))
      .status,
  ).toBe(200);
  expect(
    (await call("get", "/v1/translation-preferences", "preferences-a")).body,
  ).toEqual(value);
  expect(
    (await call("get", "/v1/translation-preferences", "preferences-b")).body
      .autoTranslate,
  ).toBe(true);
  expect(
    (
      await call("put", "/v1/translation-preferences", "preferences-a", {
        ...value,
        targetLanguage: "auto",
      })
    ).status,
  ).toBe(400);
});

it("persists_one_rich_result_for_history_and_owned_result_without_regeneration", async () => {
  await enable("rich-owner");
  const receipt = await capture("rich-owner", { context: "shared-rich" });
  await result("rich-owner", receipt.body.lookupId);
  const shared = (
    await call(
      "get",
      `/v1/lookups/${receipt.body.lookupId}/translation`,
      "rich-owner",
    )
  ).body;
  const history = (await call("get", "/v1/lookups", "rich-owner")).body
    .items[0];
  expect(shared.learningDetails).toEqual(learningFixture);
  expect(history.learningDetails).toEqual(shared.learningDetails);
  expect(providerCalls.get("shared-rich")).toBe(1);
  expect(
    (
      await call(
        "get",
        `/v1/lookups/${receipt.body.lookupId}/translation`,
        "other-owner",
      )
    ).status,
  ).toBe(404);
});

it("filters_owned_history_before_pagination_and_searches_saved_meanings", async () => {
  await enable("filter-owner");
  await enable("filter-other");
  const ids: string[] = [];
  for (let i = 0; i < 3; i++) {
    const row = await capture("filter-owner", {
      text: "available",
      context: "filter needle " + i,
    });
    ids.push(row.body.lookupId);
    await result("filter-owner", row.body.lookupId);
  }
  await capture("filter-owner", {
    text: "reschedule",
    context: "different context",
  });
  await capture("filter-other", { context: "filter needle private" });
  const first = await call(
    "get",
    "/v1/lookups?q=NEEDLE&sourceLanguage=en&targetLanguage=en&limit=1",
    "filter-owner",
  );
  expect(first.body.items).toHaveLength(1);
  expect(ids).toContain(first.body.items[0].id);
  expect(first.body.nextCursor).toBeTruthy();
  const second = await call(
    "get",
    `/v1/lookups?q=needle&limit=1&cursor=${first.body.nextCursor}`,
    "filter-owner",
  );
  expect(second.body.items[0].id).not.toBe(first.body.items[0].id);
  expect(ids).toContain(second.body.items[0].id);
  expect(
    (await call("get", "/v1/lookups?q=take%20part", "filter-owner")).body.items,
  ).toHaveLength(3);
  expect(
    (await call("get", "/v1/lookups?targetLanguage=fr", "filter-owner")).body
      .items,
  ).toHaveLength(0);
  expect(
    (
      await call(
        "get",
        "/v1/learning-items?q=AVAILABLE&targetLanguage=en",
        "filter-owner",
      )
    ).body.items,
  ).toHaveLength(1);
  expect(
    (await call("get", "/v1/learning-items?targetLanguage=fr", "filter-owner"))
      .body.items,
  ).toHaveLength(0);
  expect(
    (await call("get", "/v1/lookups?sourceLanguage=invalid!!", "filter-owner"))
      .status,
  ).toBe(400);
  expect(
    (await call("get", "/v1/lookups?q=" + "x".repeat(201), "filter-owner"))
      .status,
  ).toBe(400);
});

it("matches_detected_source_language_in_both_library_views", async () => {
  await enable("detected-filter");
  const receipt = await capture("detected-filter", { sourceLanguage: "auto" });
  await result("detected-filter", receipt.body.lookupId);
  await db.prisma.lookupEvent.update({
    where: { id: receipt.body.lookupId },
    data: { detectedSourceLanguage: "en" },
  });
  expect(
    (await call("get", "/v1/lookups?sourceLanguage=en", "detected-filter")).body
      .items,
  ).toHaveLength(1);
  expect(
    (
      await call(
        "get",
        "/v1/learning-items?sourceLanguage=en",
        "detected-filter",
      )
    ).body.items,
  ).toHaveLength(1);
  expect(
    (await call("get", "/v1/lookups?sourceLanguage=fr", "detected-filter")).body
      .items,
  ).toHaveLength(0);
});

it("opens_only_owned_vocabulary_and_checks_the_selected_encounter", async () => {
  await enable("detail-owner");
  await enable("detail-other");
  const saved = await capture("detail-owner");
  await result("detail-owner", saved.body.lookupId);
  const path = `/v1/learning-items/${saved.body.candidateId}`;
  const detail = await call(
    "get",
    path + `?lookup=${saved.body.lookupId}`,
    "detail-owner",
  );
  expect(detail.status).toBe(200);
  expect(detail.body.item.id).toBe(saved.body.candidateId);
  expect(detail.body.lookup).toMatchObject({
    id: saved.body.lookupId,
    text: "available",
    translationStatus: "succeeded",
  });
  expect(detail.body.lookup.userId).toBeUndefined();
  expect(detail.body.lookup.payloadHash).toBeUndefined();
  expect((await call("get", path, "detail-other")).status).toBe(404);
  const other = await capture("detail-owner", { text: "reschedule" });
  expect(
    (await call("get", path + `?lookup=${other.body.lookupId}`, "detail-owner"))
      .status,
  ).toBe(404);
});
