import { beforeAll, afterAll, expect, it } from "vitest";
import request from "supertest";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startOidcIssuer } from "../../../tests/helpers/oidc-issuer";
let db: Awaited<ReturnType<typeof startDatabase>>;
let issuer: Awaited<ReturnType<typeof startOidcIssuer>>;
let app: Awaited<ReturnType<typeof createApp>>;
const web = "http://127.0.0.1:3000";
const apiOrigin = "http://127.0.0.1:4318";
function agent() {
  return request.agent(app.getHttpServer());
}
async function login(client = agent(), subject = "learner-a") {
  const start = await client.get("/auth/login").expect(302);
  const authorize = new URL(start.headers.location);
  authorize.searchParams.set("test_user", subject);
  const response = await fetch(authorize, { redirect: "manual" });
  const callback = new URL(response.headers.get("location")!);
  const done = await client
    .get(callback.pathname + callback.search)
    .expect(302);
  expect(done.headers.location).toBe(web + "/library");
  const session = (await client.get("/auth/session").expect(200)).body;
  return { client, csrf: session.csrfToken, callback, done };
}
beforeAll(async () => {
  issuer = await startOidcIssuer();
  db = await startDatabase();
  app = await createApp({
    prisma: db.prisma,
    oidc: { ...issuer, webOrigin: web, apiOrigin, allowLoopbackHttp: true },
  });
});
afterAll(async () => {
  await app?.close();
  await issuer?.close();
  await db?.close();
});
it("signs in through code + PKCE and keeps the credential HttpOnly, hashed and revocable", async () => {
  const { client, csrf, done } = await login();
  expect(String(done.headers["set-cookie"])).toContain("HttpOnly");
  expect(String(done.headers["set-cookie"])).toContain("SameSite=Lax");
  expect((await client.get("/v1/identity").expect(200)).body.subject).toBe(
    "learner-a",
  );
  await client
    .put("/v1/settings")
    .send({ enabled: true, excludedOrigins: [] })
    .expect(403);
  await client
    .put("/v1/settings")
    .set("Origin", "https://evil.invalid")
    .set("X-CSRF-Token", csrf)
    .send({ enabled: true, excludedOrigins: [] })
    .expect(403);
  await client
    .put("/v1/settings")
    .set("Origin", web)
    .set("X-CSRF-Token", csrf)
    .send({ enabled: true, excludedOrigins: [] })
    .expect(200);
  const rows = await db.prisma.authSession.findMany();
  expect(rows.some((x) => x.tokenHash.length === 64)).toBe(true);
  await client
    .post("/auth/logout")
    .set("Origin", web)
    .set("X-CSRF-Token", csrf)
    .expect(200);
  await client.get("/v1/identity").expect(401);
});
it("rejects callbacks without the initiating browser cookie and consumes valid callbacks once", async () => {
  const { client, callback } = await login();
  await client.get(callback.pathname + callback.search).expect(401);
  const start = await client.get("/auth/login").expect(302);
  const auth = new URL(start.headers.location);
  auth.searchParams.set("test_user", "learner-a");
  const r = await fetch(auth, { redirect: "manual" });
  const cb = new URL(r.headers.get("location")!);
  await agent()
    .get(cb.pathname + cb.search)
    .expect(401);
  await client
    .get(cb.pathname + cb.search.replace(/state=[^&]+/, "state=wrong"))
    .expect(401);
});
it("requires approval and private device proof, exchanges once, isolates accounts and revokes devices", async () => {
  const a = await login();
  const b = await login(agent(), "learner-b");
  const pair = (await agent().post("/auth/pairings").send({}).expect(201)).body;
  expect(pair.verificationUrl).toBe(web + "/pair?code=" + pair.userCode);
  await agent()
    .post("/auth/pairings/poll")
    .send({ deviceCode: pair.deviceCode })
    .expect(202);
  await agent()
    .post("/auth/pairings/approve")
    .send({ userCode: pair.userCode })
    .expect(401);
  await a.client
    .post("/auth/pairings/approve")
    .send({ userCode: pair.userCode })
    .expect(403);
  await a.client
    .post("/auth/pairings/approve")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .send({ userCode: pair.userCode })
    .expect(200);
  await b.client
    .post("/auth/pairings/approve")
    .set("Origin", web)
    .set("X-CSRF-Token", b.csrf)
    .send({ userCode: pair.userCode })
    .expect(409);
  await agent()
    .post("/auth/pairings/poll")
    .send({ deviceCode: pair.userCode })
    .expect(400);
  // Clear throttle in the DB rather than making the test wait five seconds.
  await db.prisma.extensionPairing.updateMany({ data: { lastPolledAt: null } });
  const exchanges = await Promise.all([
    agent().post("/auth/pairings/poll").send({ deviceCode: pair.deviceCode }),
    agent().post("/auth/pairings/poll").send({ deviceCode: pair.deviceCode }),
  ]);
  expect(exchanges.filter((x) => x.status === 200)).toHaveLength(1);
  const token = exchanges.find((x) => x.status === 200)!.body.token;
  expect(
    (
      await agent()
        .get("/v1/identity")
        .set("Authorization", `Bearer ${token}`)
        .expect(200)
    ).body.subject,
  ).toBe("learner-a");
  await b.client
    .post("/auth/extensions/revoke")
    .set("Origin", web)
    .set("X-CSRF-Token", b.csrf)
    .expect(200);
  await agent()
    .get("/v1/identity")
    .set("Authorization", `Bearer ${token}`)
    .expect(200);
  await a.client
    .post("/auth/extensions/revoke")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .expect(200);
  await agent()
    .get("/v1/identity")
    .set("Authorization", `Bearer ${token}`)
    .expect(401);
});
it("rejects expired pairings and sessions", async () => {
  const a = await login();
  const pair = (await agent().post("/auth/pairings").send({}).expect(201)).body;
  await db.prisma.extensionPairing.updateMany({
    data: { expiresAt: new Date(0) },
  });
  await a.client
    .post("/auth/pairings/approve")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .send({ userCode: pair.userCode })
    .expect(409);
  await agent()
    .post("/auth/pairings/poll")
    .send({ deviceCode: pair.deviceCode })
    .expect(401);
  await db.prisma.authSession.updateMany({ data: { expiresAt: new Date(0) } });
  await a.client.get("/v1/identity").expect(401);
});

it("revocation_also_invalidates_approved_pairings_not_yet_redeemed", async () => {
  const a = await login();
  const pair = (await agent().post("/auth/pairings").send({}).expect(201)).body;
  await a.client
    .post("/auth/pairings/approve")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .send({ userCode: pair.userCode })
    .expect(200);
  await a.client
    .post("/auth/extensions/revoke")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .expect(200);
  await agent()
    .post("/auth/pairings/poll")
    .send({ deviceCode: pair.deviceCode })
    .expect(401);
});
it("rejects_a_stale_tabs_csrf_after_another_login_changes_the_cookie", async () => {
  const a = await login();
  const b = await login(a.client, "learner-b");
  await b.client.get("/v1/lookups").set("X-CSRF-Token", a.csrf).expect(401);
  await b.client
    .put("/v1/settings")
    .set("Origin", web)
    .set("X-CSRF-Token", a.csrf)
    .send({ enabled: true, excludedOrigins: [] })
    .expect(401);
  expect(
    (await b.client.get("/v1/identity").set("X-CSRF-Token", b.csrf).expect(200))
      .body.subject,
  ).toBe("learner-b");
});

it("a_concurrent_revoke_wins_over_credential_issuance", async () => {
  const a = await login();
  for (let i = 0; i < 3; i++) {
    const pair = (await agent().post("/auth/pairings").send({}).expect(201))
      .body;
    await a.client
      .post("/auth/pairings/approve")
      .set("Origin", web)
      .set("X-CSRF-Token", a.csrf)
      .send({ userCode: pair.userCode })
      .expect(200);
    const [exchange] = await Promise.all([
      agent().post("/auth/pairings/poll").send({ deviceCode: pair.deviceCode }),
      a.client
        .post("/auth/extensions/revoke")
        .set("Origin", web)
        .set("X-CSRF-Token", a.csrf)
        .expect(200),
    ]);
    expect([200, 401]).toContain(exchange.status);
    if (exchange.status === 200)
      await agent()
        .get("/v1/identity")
        .set("Authorization", `Bearer ${exchange.body.token}`)
        .expect(401);
  }
});
it("rejects_expired_login_and_invalid_PKCE_or_nonce_without_issuing_a_session", async () => {
  for (const change of [
    { expiresAt: new Date(0) },
    { verifier: "wrong-verifier" },
    { nonce: "wrong-nonce" },
  ]) {
    const client = agent();
    const start = await client.get("/auth/login").expect(302);
    const authorize = new URL(start.headers.location);
    authorize.searchParams.set("test_user", "learner-a");
    const response = await fetch(authorize, { redirect: "manual" });
    const callback = new URL(response.headers.get("location")!);
    await db.prisma.loginAttempt.updateMany({ data: change });
    await client.get(callback.pathname + callback.search).expect(401);
    await client.get("/auth/session").expect(401);
  }
});

it("returns to the learning dashboard after login but rejects external destinations", async () => {
  for (const [destination, expected] of [
    ["/progress", "/progress"],
    ["/games/web", "/games/web"],
    ["https://evil.invalid", "/library"],
  ]) {
    const client = agent();
    const start = await client
      .get("/auth/login")
      .query({ returnTo: destination })
      .expect(302);
    const authorize = new URL(start.headers.location);
    authorize.searchParams.set("test_user", "progress-login");
    const response = await fetch(authorize, { redirect: "manual" });
    const callback = new URL(response.headers.get("location")!);
    const done = await client
      .get(callback.pathname + callback.search)
      .expect(302);
    expect(done.headers.location).toBe(web + expected);
  }
});
