import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "tests/e2e",
  testMatch: "real-translation.spec.ts",
  workers: 1,
  timeout: 90000,
  expect: { timeout: 30000 },
  reporter: "list",
  use: { trace: "retain-on-failure" },
  webServer: {
    command: "pnpm dev:local",
    url: "http://127.0.0.1:3000/library",
    reuseExistingServer: false,
    // Let the embedded database release its native resources before exiting.
    gracefulShutdown: { signal: "SIGTERM", timeout: 10000 },
    timeout: 300000,
    env: {
      CONTEXTSPEAK_EPHEMERAL: "1",
      TRANSLATION_PROVIDER: "libretranslate",
    },
  },
});
