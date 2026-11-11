import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "games/unity/tests",
  outputDir: ".local/unity-test-results",
  workers: 1,
  timeout: 120000,
  expect: { timeout: 20000 },
  use: {
    viewport: { width: 1360, height: 900 },
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    permissions: ["microphone"],
    launchOptions: {
      args: [
        "--use-fake-ui-for-media-stream",
        "--use-fake-device-for-media-stream",
      ],
    },
  },
  webServer: {
    command: "pnpm world:dev",
    url: "http://127.0.0.1:4320/health",
    reuseExistingServer: true,
  },
});
