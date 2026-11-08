import { mountPronunciation } from "../src/pronunciation";
import { mountPanelPosition } from "../src/panel-position";
import { renderLearningCard } from "../src/learning-card";
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
    let disposeSpeech: (() => void) | undefined;
    let disposePosition: (() => void) | undefined;
    function dismiss() {
      disposeSpeech?.();
      disposeSpeech = undefined;
      disposePosition?.();
      disposePosition = undefined;
      host?.remove();
      host = undefined;
    }
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && host) {
        dismiss();
      }
    });
    // Some web apps stop bubbling mouse events; observe selections before that.
    document.addEventListener(
      "mouseup",
      async (event) => {
        if (host?.contains(event.target as Node)) return;
        dismiss();
        const selection = window.getSelection();
        const excerpt = selectionExcerpt(selection);
        const range = selection?.rangeCount
          ? selection.getRangeAt(0).cloneRange()
          : undefined;
        if (!excerpt) return;
        const { text, context } = excerpt;
        const panel = document.createElement("div");
        host = panel;
        panel.setAttribute("data-contextspeak", "panel");
        panel.style.cssText = "position:fixed;left:0;top:0;z-index:2147483647";
        const shadow = panel.attachShadow({ mode: "open" });
        shadow.innerHTML =
          '<style>:host{font-family:system-ui;font-size:14px;color-scheme:dark}section{box-sizing:border-box;background:#292d2e;color:#f5f7f5;border:1px solid #68736e;border-radius:14px;box-shadow:0 8px 36px #0002;padding:14px;width:min(360px,calc(100vw - 32px));max-height:70vh;overflow:auto;scrollbar-width:thin;scrollbar-color:#7aa390 #292d2e}.top{display:flex;align-items:center;justify-content:space-between;gap:12px;cursor:grab;touch-action:none;user-select:none}.top[data-dragging]{cursor:grabbing}.top:focus-visible{outline:2px solid #64dfc9;outline-offset:3px;border-radius:4px}.top small{font-size:10px}button{background:#176b60;color:white;border:0;border-radius:8px;padding:8px 10px;cursor:pointer}button.close{background:transparent;color:#d8e6df;font-size:22px;line-height:1;width:28px;height:28px;padding:0}p{margin:8px 0;font-size:14px;line-height:1.5;white-space:pre-wrap;overflow-wrap:anywhere}.word{display:flex;align-items:center;gap:8px}.pronounce{display:grid;place-items:center;background:transparent;color:#b6cfc2;padding:4px;width:28px;height:28px;flex:none}.pronounce:disabled{opacity:.45;cursor:default}.speech-status{font-size:11px}p.selection{font-weight:600;max-height:64px;overflow:auto}small{color:#b6cfc2}h3{font-size:14px;margin:16px 0 6px}.secondary{color:#b3d3c6;margin-top:0}.languages{display:flex;gap:8px;flex-wrap:wrap}label{font-size:11px;color:#b6cfc2;flex:1;min-width:125px}select{box-sizing:border-box;display:block;background:#3b4140;color:#fff;border:1px solid #76827b;border-radius:8px;padding:5px;width:100%;font-size:12px;margin-top:3px}button[hidden],[hidden]{display:none!important}summary{cursor:pointer;color:#b3d3c6;padding:8px 0;font-size:13px}button:focus-visible,select:focus-visible,summary:focus-visible{outline:2px solid #64dfc9;outline-offset:2px}[data-save-state]{display:block;font-size:12px;margin:8px 0}details{border-top:1px solid #53635a;margin-top:8px}</style><section role="region" aria-label="ContextSpeak translation"><div class="top" role="group" tabindex="0" aria-label="Move translation card (arrow keys)" title="Drag to move. Arrow keys move the card."><small>CONTEXTSPEAK</small><button class="close" type="button" aria-label="Close translation" title="Close"><span aria-hidden="true">×</span></button></div><div class="word"><p class="selection"></p><button class="pronounce" type="button" aria-label="Listen to selected text" disabled><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M11 5 6 9H3v6h3l5 4V5Z M15 8a6 6 0 0 1 0 8 M18 5a10 10 0 0 1 0 14"/></svg></button></div><small class="speech-status" aria-live="polite"></small><div class="languages"><label>Source language<select aria-label="Source language"></select></label><label>Target language<select aria-label="Target language"></select></label></div><button class="translate" type="button" disabled>Translate selection</button><p role="status">Loading translation settings…</p><small data-save-state aria-live="polite"></small><details data-explanation hidden><summary>Show explanation</summary><div data-learning-card></div></details></section>';
        const paragraphs = shadow.querySelectorAll("p");
        paragraphs[0].textContent = text;
        const button = shadow.querySelector<HTMLButtonElement>(".translate")!;
        shadow.querySelector(".close")!.addEventListener("click", () => {
          clearTimeout(automaticTimer);
          if (host === panel) dismiss();
        });
        const explanation =
          shadow.querySelector<HTMLDetailsElement>("[data-explanation]")!;
        const saved = shadow.querySelector<HTMLElement>("[data-save-state]")!;
        let synced = false;
        explanation.addEventListener("toggle", () => {
          explanation.querySelector("summary")!.textContent = explanation.open
            ? "Hide explanation"
            : "Show explanation";
        });
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
        let detectedLanguage: string | undefined;
        const speechStatus =
          shadow.querySelector<HTMLElement>(".speech-status")!;
        const speech = mountPronunciation(
          shadow.querySelector<HTMLButtonElement>(".pronounce")!,
          text,
          () => detectedLanguage ?? source.value,
          (message) => {
            speechStatus.textContent = message;
          },
        );
        disposeSpeech = speech.dispose;
        showLanguages(localLanguages, defaultTranslationPreferences);
        speech.update();
        source.disabled = true;
        target.disabled = true;
        document.documentElement.append(panel);
        disposePosition = mountPanelPosition(
          panel,
          shadow.querySelector<HTMLElement>(".top")!,
          range,
          { x: event.clientX, y: event.clientY },
        );
        let retryEventId: string | undefined;
        let automatic = false;
        let ai = false;
        const learning = shadow.querySelector<HTMLElement>(
          "[data-learning-card]",
        )!;
        let automaticTimer: ReturnType<typeof setTimeout> | undefined;
        async function translate() {
          if (!panel.isConnected || button.disabled) return;
          clearTimeout(automaticTimer); // A manual click replaces the pending automatic request.
          automaticTimer = undefined;
          const retrying =
            !!retryEventId && button.textContent === "Retry translation";
          if (!retrying) {
            retryEventId = undefined;
            synced = false;
          }
          button.disabled = true;
          if (ai) button.hidden = true;
          learning.replaceChildren();
          explanation.hidden = true;
          explanation.open = false;
          saved.textContent = retrying
            ? synced
              ? "Saved to library"
              : "Saved locally"
            : "";
          source.disabled = true;
          target.disabled = true;
          paragraphs[1].textContent = "Translating…";
          try {
            // Snapshot languages now: later preference changes must not rewrite this lookup.
            const result = retrying
              ? await browser.runtime.sendMessage({
                  type: "lookup-retry",
                  eventId: retryEventId,
                })
              : await browser.runtime.sendMessage({
                  type: "translate",
                  input: {
                    eventId: crypto.randomUUID(),
                    text,
                    context,
                    sourceLanguage: source.value,
                    targetLanguage: target.value,
                  },
                });
            if (result.ok && result.eventId) retryEventId = result.eventId;
            await observeLookup(result, {
              active: () => panel.isConnected,
              render: (reply) => {
                if (reply.ok && reply.status === "succeeded") {
                  detectedLanguage = reply.detectedSourceLanguage;
                  speech.update();
                  renderLearningCard(learning, reply.learningDetails);
                  explanation.hidden = !learning.childElementCount;
                }
                // A queued event is durable locally; a completed captured lookup is synced.
                if (reply.ok && retryEventId) {
                  if (reply.status !== "queued") synced = true;
                  saved.textContent = synced
                    ? "Saved to library"
                    : "Saved locally · waiting for sync";
                } else if (reply.ok && reply.status === "succeeded") {
                  saved.textContent =
                    "Not saved · collection off or site excluded";
                } else if (!reply.ok && reply.saved) {
                  if (reply.saved === "library") synced = true;
                  saved.textContent =
                    reply.saved === "library"
                      ? "Saved to library"
                      : "Saved locally";
                } else if (!reply.ok && reply.status === "cancelled") {
                  saved.textContent = "Not saved";
                }
                if (!reply.ok && ai) {
                  button.hidden = false;
                  button.textContent = "Retry translation";
                }
                paragraphs[1].textContent =
                  reply.ok && reply.status === "succeeded"
                    ? (reply.meaning ?? "")
                    : (reply.error ?? "Saved locally. Waiting for sync…");
              },
              wait: () => new Promise((resolve) => setTimeout(resolve, 500)),
              read: (eventId) =>
                browser.runtime.sendMessage({ type: "lookup-result", eventId }),
              limit: 240,
            });
          } catch {
            if (ai) {
              button.hidden = false;
              button.textContent = "Retry translation";
            }
            paragraphs[1].textContent =
              "Extension disconnected. Reload the page.";
          } finally {
            button.disabled = false;
            source.disabled = false;
            target.disabled = false;
          }
        }
        for (const select of [source, target])
          select.addEventListener("change", () => {
            retryEventId = undefined;
            detectedLanguage = undefined;
            speech.update();
            synced = false;
            if (automatic) void translate();
          });
        button.addEventListener("click", () => {
          void translate();
        });
        try {
          const options = readTranslationOptions(
            await browser.runtime.sendMessage({ type: "translation-options" }),
          );
          if (!panel.isConnected) return;
          showLanguages(options.languages, options.preferences);
          speech.update();
          source.disabled = false;
          target.disabled = false;
          ai =
            options.provider === "ollama-local" ||
            options.provider === "gemini";
          automatic = options.preferences.autoTranslate;
          button.hidden = ai;
          if (
            ai &&
            Array.from(text).length > (options.maxSelectionCharacters ?? 600)
          ) {
            paragraphs[1].textContent =
              "Select a shorter passage: AI learning cards support up to 600 characters per selection.";
            return;
          }
          button.disabled = false;
          paragraphs[1].textContent = "";
          if (ai && !automatic)
            paragraphs[1].textContent =
              "Automatic translation is paused. Enable it in ContextSpeak settings.";
          if (automatic) {
            // Debounce selection changes; removed panels never start a translation.
            automaticTimer = setTimeout(() => {
              void translate();
            }, 300);
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
