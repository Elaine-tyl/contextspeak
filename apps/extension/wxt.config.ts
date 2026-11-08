import { defineConfig } from "wxt";
export default defineConfig({
  modules: ["@wxt-dev/module-react"],
  manifest: {
    version: "0.7.2",
    minimum_chrome_version: "116",
    name: "ContextSpeak",
    description:
      "Automatic word and sentence translation with AI explanations and a personal vocabulary library.",
    permissions: ["storage", "alarms", "offscreen"],
    host_permissions: [
      (process.env.WXT_API_ORIGIN ?? "http://127.0.0.1:4318") + "/*",
    ],
    action: { default_title: "ContextSpeak settings" },
  },
});
