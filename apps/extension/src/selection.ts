import { MAX_SELECTION_CHARACTERS } from "@contextspeak/contracts";
// A read-only contenteditable="false" wrapper is ordinary page text, not an editor.
const restricted =
  'form,input,textarea,select,[contenteditable]:not([contenteditable="false" i]),[role="textbox"],script,style,noscript,[hidden],[aria-hidden="true"]';
function blocked(node: Node) {
  const element =
    node.nodeType === Node.ELEMENT_NODE
      ? (node as Element)
      : node.parentElement;
  return !!element?.closest(restricted);
}
export function selectionExcerpt(
  selection: Selection | null,
): { text: string; context: string } | undefined {
  if (!selection || selection.isCollapsed || !selection.rangeCount) return;
  const range = selection.getRangeAt(0);
  if (blocked(range.startContainer) || blocked(range.endContainer)) return;
  const text = selection.toString().trim();
  if (!text || Array.from(text).length > MAX_SELECTION_CHARACTERS) return;
  for (const node of document.querySelectorAll(restricted)) {
    if (range.intersectsNode(node)) return;
  }
  const root =
    range.commonAncestorContainer.nodeType === Node.TEXT_NODE
      ? range.commonAncestorContainer.parentElement
      : (range.commonAncestorContainer as Element);
  if (!root) return;
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
    acceptNode: (node) =>
      blocked(node) ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_ACCEPT,
  });
  const parts: string[] = [];
  let node: Node | null;
  while ((node = walker.nextNode())) parts.push(node.textContent ?? "");
  const safe = parts.join(" ").replace(/\s+/g, " ").trim();
  const index = safe.indexOf(text);
  const start = Math.max(0, index - 300);
  const context = Array.from(safe)
    .slice(start, start + 1000)
    .join("");
  return { text, context };
}
