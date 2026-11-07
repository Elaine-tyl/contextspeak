import {
  localLanguages,
  readTranslationOptions,
  RELOAD_EXTENSION,
} from "../src/translation-ui";
import { observeLookup } from "../src/observe-lookup";
import { selectionExcerpt } from "../src/selection";
import { defineContentScript } from "wxt/utils/define-content-script";
import { browser } from "wxt/browser";
import {
  defaultTranslationPreferences,
  type TranslationPreferences,
  type TranslationLanguage,
} from "@contextspeak/contracts";
export default defineContentScript({
  matches: ["http://*/*", "https://*/*"],
  main() {
    let host: HTMLDivElement | undefined;
    // Some web apps stop bubbling mouse events; observe selections before that.
    document.addEventListener(
      "mouseup",
      async (event) => {
        if (host?.contains(event.target as Node)) return;
        host?.remove();
        host = undefined;
        const excerpt = selectionExcerpt(window.getSelection());
        if (!excerpt) return;
        const { text, context } = excerpt;
        const panel = document.createElement("div");
        host = panel;
        panel.setAttribute("data-contextspeak", "panel");
        panel.style.cssText =
          "position:fixed;right:24px;bottom:24px;z-index:2147483647";
        const shadow = panel.attachShadow({ mode: "open" });
        shadow.innerHTML =
          '<style>:host{font-family:system-ui}section{background:#fff;color:#173449;border:1px solid #a2c7e8;border-radius:16px;box-shadow:0 8px 36px #0002;padding:20px;width:min(320px,80vw);max-height:70vh;overflow:auto}button{background:#176b60;color:white;border:0;border-radius:8px;padding:10px 14px;cursor:pointer}p{font-size:14px;line-height:1.5;white-space:pre-wrap;overflow-wrap:anywhere}small{color:#526879}label{display:block;margin:8px 0}select{max-width:100%;margin-left:8px}</style><section><small>CONTEXTSPEAK</small><p></p><label>Source language<select aria-label="Source language"></select></label><label>Target language<select aria-label="Target language"></select></label><button type="button" disabled>Translate selection</button><p role="status">Loading translation settings…</p></section>';
        const paragraphs = shadow.querySelectorAll("p");
        paragraphs[0].textContent = text;
        const button = shadow.querySelector("button")!;
        const [source, target] = shadow.querySelectorAll("select");
        function showLanguages(
          languages: TranslationLanguage[],
          preferences: TranslationPreferences,
        ) {
          source.replaceChildren(new Option("Detect automatically", "auto"));
          target.replaceChildren();
          for (const language of languages) {
            source.add(new Option(language.name, language.code));
            target.add(new Option(language.name, language.code));
          }
          source.value = preferences.sourceLanguage;
          target.value = preferences.targetLanguage;
        }
        showLanguages(localLanguages, defaultTranslationPreferences);
        source.disabled = true;
        target.disabled = true;
        document.documentElement.append(panel);
        let automaticTimer: ReturnType<typeof setTimeout> | undefined;
        async function translate() {
          if (!panel.isConnected || button.disabled) return;
          clearTimeout(automaticTimer); // A manual click replaces the pending automatic request.
          automaticTimer = undefined;
          button.disabled = true;
          source.disabled = true;
          target.disabled = true;
          paragraphs[1].textContent = "Translating…";
          try {
            // Snapshot languages now: later preference changes must not rewrite this lookup.
            const result = await browser.runtime.sendMessage({
              type: "translate",
              input: {
                eventId: crypto.randomUUID(),
                text,
                context,
                sourceLanguage: source.value,
                targetLanguage: target.value,
              },
            });
            await observeLookup(result, {
              active: () => panel.isConnected,
              render: (reply) => {
                paragraphs[1].textContent =
                  reply.ok && reply.status === "succeeded"
                    ? (reply.meaning ?? "")
                    : (reply.error ?? "Saved locally. Waiting for sync…");
              },
              wait: () => new Promise((resolve) => setTimeout(resolve, 300)),
              read: (eventId) =>
                browser.runtime.sendMessage({ type: "lookup-result", eventId }),
              limit: 90,
            });
          } catch {
            paragraphs[1].textContent =
              "Extension disconnected. Reload the page.";
          } finally {
            button.disabled = false;
            source.disabled = false;
            target.disabled = false;
          }
        }
        button.addEventListener("click", () => {
          void translate();
        });
        try {
          const options = readTranslationOptions(
            await browser.runtime.sendMessage({ type: "translation-options" }),
          );
          if (!panel.isConnected) return;
          showLanguages(options.languages, options.preferences);
          source.disabled = false;
          target.disabled = false;
          shadow.querySelector("small")!.textContent =
            options.provider === "fixture"
              ? "CONTEXTSPEAK · FIXTURE MODE"
              : "CONTEXTSPEAK · LOCAL TRANSLATION";
          button.disabled = false;
          paragraphs[1].textContent = "";
          if (options.preferences.autoTranslate) {
            // Debounce selection changes; removed panels never start a translation.
            automaticTimer = setTimeout(() => {
              void translate();
            }, 250);
          }
        } catch (error) {
          paragraphs[1].textContent =
            error instanceof Error ? error.message : RELOAD_EXTENSION;
        }
      },
      { capture: true },
    );
  },
});
