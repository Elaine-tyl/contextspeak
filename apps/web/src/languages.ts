import { aiLanguages } from "@contextspeak/contracts";

// Show readable language names instead of codes such as "zh-Hans".
export function languageName(code: string | null | undefined) {
  if (!code || code === "auto") return "Auto-detect";
  return aiLanguages.find((language) => language.code === code)?.name ?? code;
}

export function languagePair(
  source: string | null | undefined,
  target: string,
) {
  return `${languageName(source)} → ${languageName(target)}`;
}

export function shortDate(value: string) {
  return new Date(value).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
  });
}

export function hostname(url: string | null) {
  if (!url) return "Typed manually";
  try {
    return new URL(url).hostname.replace(/^www\./, "");
  } catch {
    return "Web page";
  }
}
