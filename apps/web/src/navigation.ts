// Return links must stay inside the library, even when a URL is edited manually.
export function libraryReturn(value: unknown, fallback = "/library") {
  if (typeof value !== "string") return fallback;
  try {
    const url = new URL(value, "http://contextspeak.invalid");
    return url.origin === "http://contextspeak.invalid" &&
      url.pathname === "/library"
      ? url.pathname + url.search
      : fallback;
  } catch {
    return fallback;
  }
}
export function withLibraryReturn(href: string, returnTo: string) {
  const target = learningReturn(returnTo);
  return target === "/library"
    ? href
    : `${href}${href.includes("?") ? "&" : "?"}returnTo=${encodeURIComponent(target)}`;
}

export function practiceReturn(value: unknown) {
  const url = new URL(libraryReturn(value), "http://contextspeak.invalid");
  url.searchParams.set("view", "practice");
  return url.pathname + url.search;
}

// Dashboard is the only extra return destination; never accept a URL prefix match.
export function learningReturn(value: unknown, fallback = "/library") {
  return value === "/progress" ? "/progress" : libraryReturn(value, fallback);
}
export function practiceMode(
  value: unknown,
  hasScenario = false,
): "listening" | "speaking" | "conversation" {
  if (value === "speaking") return "speaking";
  return value === "conversation" && hasScenario ? "conversation" : "listening";
}
