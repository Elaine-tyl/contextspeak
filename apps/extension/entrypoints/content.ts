import { defineContentScript } from "wxt/utils/define-content-script";
import { browser } from "wxt/browser";
export default defineContentScript({
  matches: ["http://*/*", "https://*/*"],
  main() {
    let host: HTMLDivElement | undefined;
    document.addEventListener("mouseup", (event) => {
      if (host?.contains(event.target as Node)) return;
      host?.remove();
      host = undefined;
      const selection = window.getSelection();
      const text = selection?.toString().trim();
      const node = selection?.anchorNode?.parentElement;
      if (
        !text ||
        !node ||
        Array.from(text).length > 200 ||
        node.closest('input,textarea,select,[contenteditable],[role="textbox"]')
      )
        return;
      if (
        (event.target as Element)?.closest?.(
          'input,textarea,[contenteditable],[role="textbox"]',
        )
      )
        return;
      const context = node.textContent?.trim().slice(0, 1000) || undefined;
      host = document.createElement("div");
      host.setAttribute("data-contextspeak", "panel");
      host.style.cssText =
        "position:fixed;right:24px;bottom:24px;z-index:2147483647";
      const shadow = host.attachShadow({ mode: "open" });
      shadow.innerHTML =
        '<style>:host{font-family:system-ui}section{background:#fff;color:#173449;border:1px solid #a2c7e8;border-radius:16px;box-shadow:0 8px 36px #0002;padding:20px;width:300px}button{background:#176b60;color:white;border:0;border-radius:8px;padding:10px 14px;cursor:pointer}p{font-size:14px;line-height:1.5}small{color:#526879}</style><section><small>CONTEXTSPEAK · ORIGINAL FIXTURES</small><p></p><button type="button">Translate selection</button><p role="status"></p></section>';
      const paragraphs = shadow.querySelectorAll("p");
      paragraphs[0].textContent = text;
      const button = shadow.querySelector("button")!;
      button.addEventListener("click", async () => {
        button.disabled = true;
        paragraphs[1].textContent = "Looking up…";
        try {
          const result = await browser.runtime.sendMessage({
            type: "translate",
            input: {
              eventId: crypto.randomUUID(),
              text,
              context,
              sourceLanguage: "en",
              targetLanguage: "en",
            },
          });
          paragraphs[1].textContent = result.ok ? result.meaning : result.error;
        } catch {
          paragraphs[1].textContent =
            "Extension disconnected. Reload the page.";
        } finally {
          button.disabled = false;
        }
      });
      document.documentElement.append(host);
    });
  },
});
