"use client";
import { useEffect, useSyncExternalStore } from "react";
const changed = "contextspeak-query-change";
function subscribe(listener: () => void) {
  window.addEventListener("popstate", listener);
  window.addEventListener(changed, listener);
  return () => {
    window.removeEventListener("popstate", listener);
    window.removeEventListener(changed, listener);
  };
}
// Native history keeps filters bookmarkable without navigating on each keystroke.
export function usePageQuery() {
  const search = useSyncExternalStore(
    subscribe,
    () => window.location.search,
    () => "",
  );
  return { search, params: new URLSearchParams(search) };
}
export function updateQuery(values: Record<string, string>, push = false) {
  const url = new URL(window.location.href);
  for (const [key, value] of Object.entries(values)) {
    if (value) url.searchParams.set(key, value);
    else url.searchParams.delete(key);
  }
  window.history[push ? "pushState" : "replaceState"](null, "", url);
  window.dispatchEvent(new Event(changed));
}
export function libraryPosition(
  target: string,
): { top: number; pages: number } | null {
  try {
    const raw = sessionStorage.getItem("contextspeak-scroll:" + target);
    if (raw === null) return null;
    const value = JSON.parse(raw);
    const top = typeof value === "number" ? value : value.top;
    const pages = typeof value === "number" ? 1 : value.pages;
    return Number.isFinite(top) &&
      Number.isInteger(pages) &&
      pages >= 1 &&
      pages <= 100
      ? { top: Math.max(0, top), pages }
      : null;
  } catch {
    return null;
  }
}
export function rememberLibraryScroll(pages = 1) {
  try {
    sessionStorage.setItem(
      "contextspeak-scroll:" + location.pathname + location.search,
      JSON.stringify({ top: window.scrollY, pages: Math.min(pages, 100) }),
    );
  } catch {
    /* Storage can be disabled. */
  }
}
export function useRestoreLibraryScroll(target: string, ready: boolean) {
  useEffect(() => {
    if (!ready) return;
    const key = "contextspeak-scroll:" + target;
    const saved = libraryPosition(target);
    if (!saved) return;
    const frame = requestAnimationFrame(() => {
      window.scrollTo(0, saved.top);
      try {
        sessionStorage.removeItem(key);
      } catch {
        /* Best-effort restoration. */
      }
    });
    return () => cancelAnimationFrame(frame);
  }, [target, ready]);
}
