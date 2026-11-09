import "reflect-metadata";
import { expect, it } from "vitest";
import { assertProductionConfig } from "../src/operations/production-config";
import { OperationsService } from "../src/operations/operations.service";

const production = {
  nodeEnv: "production",
  oidc: {
    issuer: "https://accounts.google.com",
    apiOrigin: "https://api.contextspeak.example",
    webOrigin: "https://contextspeak.example",
    allowLoopbackHttp: false,
  },
  providers: {
    translation: { name: "gemini" },
    speech: { cacheKey: "gemini:voice:v1" },
    speaking: { constructor: { name: "GeminiSpeakingProvider" } },
  },
};

it("preserves development defaults but refuses missing or simulated production providers", () => {
  expect(() => assertProductionConfig({})).not.toThrow();
  expect(() => assertProductionConfig(production)).not.toThrow();
  for (const providers of [
    {},
    { ...production.providers, translation: { name: "fixture" } },
    {
      ...production.providers,
      scenario: { constructor: { name: "FixtureScenarioProvider" } },
    },
    { ...production.providers, speech: { cacheKey: "fixture-speech-v1" } },
    {
      ...production.providers,
      speaking: { constructor: { name: "FixtureSpeakingProvider" } },
    },
  ])
    expect(() =>
      assertProductionConfig({ ...production, providers }),
    ).toThrow();
  expect(() =>
    assertProductionConfig({ ...production, testAuth: true }),
  ).toThrow();
  expect(() =>
    assertProductionConfig({ ...production, oidc: undefined }),
  ).toThrow();
});

it("rejects unsafe production origins, credentials and insecure OIDC overrides", () => {
  for (const webOrigin of [
    "http://contextspeak.example",
    "https://localhost",
    "https://127.1",
    "https://[::1]",
    "https://10.1.2.3",
    "https://api.localhost",
    "https://api.localhost.",
    "https://*.example.com",
    "https://user:secret@contextspeak.example",
    "https://contextspeak.example/path",
    "*",
  ])
    expect(() =>
      assertProductionConfig({
        ...production,
        oidc: { ...production.oidc, webOrigin },
      }),
    ).toThrow();
  expect(() =>
    assertProductionConfig({
      ...production,
      oidc: { ...production.oidc, allowLoopbackHttp: true },
    }),
  ).toThrow();
  expect(() =>
    assertProductionConfig({ ...production, origins: ["*"] }),
  ).toThrow();
  expect(() =>
    assertProductionConfig({
      ...production,
      origins: [
        production.oidc.webOrigin,
        "chrome-extension://abcdefghijklmnopabcdefghijklmnop",
      ],
    }),
  ).not.toThrow();
});

it("coalesces overlapping readiness probes including a dependency that remains stuck", async () => {
  let calls = 0;
  const operations = new OperationsService(() => {
    calls++;
    return new Promise(() => {});
  }, 5);
  await Promise.all([operations.readiness(), operations.readiness()]);
  await operations.readiness();
  expect(calls).toBe(1);
});

it("reports a failed or stuck dependency as unready without disclosing errors", async () => {
  const failed = new OperationsService(async () => {
    throw new Error("secret-user@example.test token=secret");
  });
  expect(await failed.readiness()).toEqual({ status: "unavailable" });
  const stuck = new OperationsService(() => new Promise(() => {}), 5);
  expect(await stuck.readiness()).toEqual({ status: "unavailable" });
  const healthy = new OperationsService(async () => {});
  expect(await healthy.readiness()).toEqual({ status: "ready" });
  healthy.recordRequest(200);
  healthy.recordRequest(400);
  healthy.recordRequest(503);
  expect(healthy.snapshot()).toMatchObject({ requests: 3, serverErrors: 1 });
  expect(Object.keys(healthy.snapshot()).sort()).toEqual([
    "requests",
    "serverErrors",
    "uptimeSeconds",
  ]);
});
