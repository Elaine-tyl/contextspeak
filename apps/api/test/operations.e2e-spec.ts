import { afterAll, beforeAll, expect, it } from "vitest";
import request from "supertest";
import { createApp } from "../src/app";
import { startDatabase } from "../../../tests/helpers/database";
import { startIssuer } from "../../../tests/helpers/issuer";
import { startOidcIssuer } from "../../../tests/helpers/oidc-issuer";

let db: Awaited<ReturnType<typeof startDatabase>>;
let jwtIssuer: Awaited<ReturnType<typeof startIssuer>>;
let oidcIssuer: Awaited<ReturnType<typeof startOidcIssuer>>;
let jwtApp: Awaited<ReturnType<typeof createApp>>;
let oidcApp: Awaited<ReturnType<typeof createApp>>;
let databaseClosed = false;

beforeAll(async () => {
  db = await startDatabase();
  jwtIssuer = await startIssuer();
  oidcIssuer = await startOidcIssuer();
  jwtApp = await createApp({ prisma: db.prisma, ...jwtIssuer });
  oidcApp = await createApp({
    prisma: db.prisma,
    oidc: {
      ...oidcIssuer,
      apiOrigin: "http://127.0.0.1:4318",
      webOrigin: "http://127.0.0.1:3000",
      allowLoopbackHttp: true,
    },
  });
});

afterAll(async () => {
  await jwtApp?.close();
  await oidcApp?.close();
  await jwtIssuer?.close();
  await oidcIssuer?.close();
  if (databaseClosed) await db?.prisma.$disconnect();
  else await db?.close();
});

for (const mode of ["jwt", "oidc"] as const) {
  it(`serves public health while retaining private route authentication with ${mode}`, async () => {
    const server = (mode === "jwt" ? jwtApp : oidcApp).getHttpServer();
    const live = await request(server).get("/health/live").expect(200);
    expect(live.body).toEqual({ status: "alive" });
    expect(live.headers["cache-control"]).toBe("no-store");
    expect(
      (await request(server).get("/health/ready").expect(200)).body,
    ).toEqual({ status: "ready" });
    await request(server).get("/v1/operations").expect(401);
    await request(server).get("/v1/lookups").expect(401);
    await request(server)
      .get("/v1/operations")
      .set("Authorization", "Bearer invalid")
      .expect(401);
  });

  it(`exposes only aggregate counters and excludes health polling with ${mode}`, async () => {
    const app = mode === "jwt" ? jwtApp : oidcApp;
    const client = request.agent(app.getHttpServer());
    let authorization: string | undefined;
    if (mode === "jwt") {
      authorization = `Bearer ${await jwtIssuer.token("synthetic-monitor")}`;
    } else {
      const start = await client.get("/auth/login").expect(302);
      const authorize = new URL(start.headers.location);
      authorize.searchParams.set("test_user", "synthetic-monitor");
      const redirect = await fetch(authorize, { redirect: "manual" });
      const callback = new URL(redirect.headers.get("location")!);
      await client.get(callback.pathname + callback.search).expect(302);
    }
    const metrics = () => {
      const call = client.get("/v1/operations");
      return authorization ? call.set("Authorization", authorization) : call;
    };
    const before = (await metrics().expect(200)).body;
    expect(Object.keys(before).sort()).toEqual([
      "requests",
      "serverErrors",
      "uptimeSeconds",
    ]);
    expect(before.serverErrors).toBe(0);
    await client.get("/health/live").expect(200);
    await client.get("/health/ready").expect(200);
    const after = (await metrics().expect(200)).body;
    expect(after.requests).toBe(before.requests + 1);
    expect(after.uptimeSeconds).toBeGreaterThanOrEqual(before.uptimeSeconds);
  });
}

it("reports and counts a database outage without exposing infrastructure or counting health polling", async () => {
  const authorization = `Bearer ${await jwtIssuer.token("synthetic-monitor")}`;
  const server = jwtApp.getHttpServer();
  const before = (
    await request(server)
      .get("/v1/operations")
      .set("Authorization", authorization)
      .expect(200)
  ).body;
  await db.close();
  databaseClosed = true;
  for (const app of [jwtApp, oidcApp]) {
    const server = app.getHttpServer();
    expect(
      (await request(server).get("/health/live").expect(200)).body,
    ).toEqual({ status: "alive" });
    const ready = await request(server).get("/health/ready").expect(503);
    expect(ready.body).toEqual({ status: "unavailable" });
    expect(ready.headers["cache-control"]).toBe("no-store");
  }
  await request(server)
    .get("/v1/lookups")
    .set("Authorization", authorization)
    .expect(500);
  const after = (
    await request(server)
      .get("/v1/operations")
      .set("Authorization", authorization)
      .expect(200)
  ).body;
  expect(after.requests).toBe(before.requests + 2);
  expect(after.serverErrors).toBe(before.serverErrors + 1);
});
