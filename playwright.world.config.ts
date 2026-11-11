import { resolve } from "node:path";
import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "games/cafe/tests",
  testMatch: "*.spec.ts",
  workers: 1,
  timeout: 60000,
  expect: { timeout: 15000 },
  use: {
    screenshot: "only-on-failure",
    viewport: { width: 1280, height: 800 },
    permissions: ["microphone"],
    launchOptions: {
      args: [
        "--use-fake-ui-for-media-stream",
        "--use-fake-device-for-media-stream",
        ...(process.env.CONTEXTSPEAK_WORLD_VOICE_TEST
          ? [
              "--use-file-for-fake-audio-capture=" +
                resolve(".local/world-test-audio.wav"),
            ]
          : []),
      ],
    },
  },
  webServer: {
    command: "pnpm world:dev",
    url: "http://127.0.0.1:4320/health",
    reuseExistingServer: true,
  },
});
