interface Point {
  x: number;
  y: number;
}
interface Size {
  width: number;
  height: number;
}
interface Anchor {
  left: number;
  right: number;
  top: number;
  bottom: number;
}
const margin = 8;
const gap = 12;
export function clampPosition(point: Point, size: Size, viewport: Size): Point {
  return {
    x: Math.max(
      margin,
      Math.min(point.x, viewport.width - size.width - margin),
    ),
    y: Math.max(
      margin,
      Math.min(point.y, viewport.height - size.height - margin),
    ),
  };
}
export function nearSelection(
  anchor: Anchor,
  size: Size,
  viewport: Size,
): Point {
  let x = anchor.right + gap;
  let y = anchor.top;
  if (x + size.width > viewport.width - margin) {
    if (anchor.left - size.width - gap >= margin)
      x = anchor.left - size.width - gap;
    else {
      x = anchor.left;
      y = anchor.bottom + gap;
      if (y + size.height > viewport.height - margin)
        y = anchor.top - size.height - gap;
    }
  }
  return clampPosition({ x, y }, size, viewport);
}
export function mountPanelPosition(
  panel: HTMLElement,
  handle: HTMLElement,
  range: Range | undefined,
  pointer: Point,
) {
  const events = new AbortController();
  const options = { signal: events.signal };
  let manual = false;
  let point = { x: 0, y: 0 };
  let drag: { id: number; pointer: Point; origin: Point } | undefined;
  function viewport(): Size {
    return { width: window.innerWidth, height: window.innerHeight };
  }
  function anchor(): Anchor {
    const rects = range?.getClientRects();
    const rect = rects?.length ? rects[rects.length - 1] : undefined;
    return rect && (rect.width || rect.height)
      ? rect
      : {
          left: pointer.x,
          right: pointer.x,
          top: pointer.y,
          bottom: pointer.y,
        };
  }
  function apply() {
    panel.style.left = `${point.x}px`;
    panel.style.top = `${point.y}px`;
  }
  function place() {
    if (!panel.isConnected) return;
    const size = panel.getBoundingClientRect();
    point = manual
      ? clampPosition(point, size, viewport())
      : nearSelection(anchor(), size, viewport());
    apply();
  }
  handle.addEventListener(
    "pointerdown",
    (event) => {
      if (
        event.button !== 0 ||
        (event.target as Element).closest("button,a,select,input")
      )
        return;
      event.preventDefault();
      manual = true;
      const bounds = panel.getBoundingClientRect();
      drag = {
        id: event.pointerId,
        pointer: { x: event.clientX, y: event.clientY },
        origin: { x: bounds.left, y: bounds.top },
      };
      handle.setPointerCapture(event.pointerId);
      handle.dataset.dragging = "true";
    },
    options,
  );
  handle.addEventListener(
    "pointermove",
    (event) => {
      if (!drag || drag.id !== event.pointerId) return;
      point = clampPosition(
        {
          x: drag.origin.x + event.clientX - drag.pointer.x,
          y: drag.origin.y + event.clientY - drag.pointer.y,
        },
        panel.getBoundingClientRect(),
        viewport(),
      );
      apply();
    },
    options,
  );
  function finish(event: PointerEvent) {
    if (!drag || drag.id !== event.pointerId) return;
    drag = undefined;
    delete handle.dataset.dragging;
    if (handle.hasPointerCapture(event.pointerId))
      handle.releasePointerCapture(event.pointerId);
  }
  handle.addEventListener("pointerup", finish, options);
  handle.addEventListener("pointercancel", finish, options);
  handle.addEventListener("lostpointercapture", finish, options);
  handle.addEventListener(
    "keydown",
    (event) => {
      if (event.target !== handle) return;
      const moves: Record<string, Point> = {
        ArrowLeft: { x: -16, y: 0 },
        ArrowRight: { x: 16, y: 0 },
        ArrowUp: { x: 0, y: -16 },
        ArrowDown: { x: 0, y: 16 },
      };
      const move = moves[event.key];
      if (!move) return;
      event.preventDefault();
      manual = true;
      point = clampPosition(
        { x: point.x + move.x, y: point.y + move.y },
        panel.getBoundingClientRect(),
        viewport(),
      );
      apply();
    },
    options,
  );
  // Growing explanations and window resizes must not push the card offscreen.
  const observer = new ResizeObserver(place);
  observer.observe(panel);
  window.addEventListener("resize", place, options);
  place();
  return () => {
    events.abort();
    observer.disconnect();
    if (drag && handle.hasPointerCapture(drag.id))
      handle.releasePointerCapture(drag.id);
  };
}
