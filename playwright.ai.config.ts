import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "tests/e2e",
  testMatch: "ai-translation.spec.ts",
  workers: 1,
  timeout: 180000,
  expect: { timeout: 120000 },
  reporter: "list",
  webServer: {
    command: "pnpm dev:local",
    url: "http://127.0.0.1:3000/library",
    reuseExistingServer: false,
    gracefulShutdown: { signal: "SIGTERM", timeout: 10000 },
    timeout: 120000,
    env: { CONTEXTSPEAK_EPHEMERAL: "1", TRANSLATION_PROVIDER: "ollama" },
  },
});
