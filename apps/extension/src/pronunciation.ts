import { browser } from "wxt/browser";
import { speechInputSchema } from "@contextspeak/contracts";
export const speakerIcon =
  '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true"><path d="M11 5 6 9H3v6h3l5 4V5Z M15 8a6 6 0 0 1 0 8 M18 5a10 10 0 0 1 0 14"/></svg>';
export function mountPronunciation(
  button: HTMLButtonElement,
  text: string,
  readLanguage: () => string,
  report: (message: string) => void,
) {
  let current: string | undefined;
  let language = "";
  let disposed = false;
  function stop() {
    if (current)
      void browser.runtime
        .sendMessage({ type: "speech-stop", requestId: current })
        .catch(() => {});
    current = undefined;
  }
  function update() {
    if (disposed) return;
    const next = readLanguage();
    if (next !== language) {
      stop();
      report("");
    }
    language = next;
    button.disabled =
      !!current || !speechInputSchema.safeParse({ text, language }).success;
    button.title = button.disabled
      ? "Wait for a supported source language, or select shorter text."
      : "Listen with AI voice";
  }
  async function play() {
    const input = speechInputSchema.safeParse({ text, language });
    if (disposed || current || !input.success) return;
    const id = crypto.randomUUID();
    current = id;
    button.disabled = true;
    button.title = "Preparing AI voice…";
    report("");
    try {
      const result = await browser.runtime.sendMessage({
        type: "speech-start",
        requestId: id,
        ...input.data,
      });
      if (!disposed && current === id && !result?.ok)
        report(result?.error ?? "AI voice is unavailable. Try again.");
    } catch {
      if (!disposed && current === id)
        report("AI voice is unavailable. Reload the extension and retry.");
    } finally {
      if (current === id) {
        current = undefined;
        update();
      }
    }
  }
  button.addEventListener("click", play);
  update();
  return {
    update,
    dispose() {
      disposed = true;
      stop();
      button.removeEventListener("click", play);
    },
  };
}
