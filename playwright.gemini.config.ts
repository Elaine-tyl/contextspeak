import { defineConfig } from "@playwright/test";
import aiConfig from "./playwright.ai.config";

// Opt-in live test: real Gemini key stays in the server's ignored .env.local.
export default defineConfig({
  ...aiConfig,
  webServer: {
    ...aiConfig.webServer,
    env: { CONTEXTSPEAK_EPHEMERAL: "1", TRANSLATION_PROVIDER: "gemini" },
  },
});
