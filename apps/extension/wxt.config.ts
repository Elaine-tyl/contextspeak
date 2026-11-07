import { defineConfig } from "wxt";
export default defineConfig({
  modules: ["@wxt-dev/module-react"],
  manifest: {
    version: "0.1.0",
    name: "ContextSpeak — local fixtures",
    description:
      "Consent-aware English lookups and vocabulary candidates. Local developer slice.",
    permissions: ["storage", "alarms"],
    host_permissions: [
      (process.env.WXT_API_ORIGIN ?? "http://127.0.0.1:4318") + "/*",
    ],
    action: { default_title: "ContextSpeak settings" },
  },
});
