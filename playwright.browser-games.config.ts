import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "tests/e2e",
  testMatch: [
    "arcade.spec.ts",
    "vocabulary-detail.spec.ts",
    "lobby.spec.ts",
    "game-studios.spec.ts",
    "game-setup.spec.ts",
    "literacy-games.spec.ts",
    "reading-recall.spec.ts",
    "scene-words.spec.ts",
  ],
  workers: 1,
  timeout: 60000,
  use: { viewport: { width: 1440, height: 1000 }, trace: "retain-on-failure" },
  webServer: {
    command: "pnpm dev",
    url: "http://127.0.0.1:3000/library",
    reuseExistingServer: true,
    timeout: 120000,
  },
});
