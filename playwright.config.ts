import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "tests/e2e",
  workers: 1,
  timeout: 60000,
  expect: { timeout: 10000 },
  reporter: "list",
  use: { trace: "retain-on-failure" },
  webServer: {
    command: "pnpm dev:test",
    url: "http://127.0.0.1:3000/library",
    reuseExistingServer: false,
    timeout: 120000,
    env: { CONTEXTSPEAK_EPHEMERAL: "1" },
  },
});
