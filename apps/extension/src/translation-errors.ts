const messages: Record<string, string> = {
  FIXTURE_NOT_FOUND: "Translation is unavailable in fixture mode.",
  UNSUPPORTED_PAIR: "This language pair is not available in fixture mode.",
  UNSUPPORTED_LANGUAGE:
    "This language pair is not installed in the local translator.",
  TRANSLATOR_OFFLINE:
    "The local translator is offline. Start or restart pnpm dev.",
  TRANSLATION_TIMEOUT: "Translation took too long. Please retry.",
  TRANSLATOR_BUSY: "The translator is busy. Please retry shortly.",
  INVALID_TRANSLATION_RESPONSE:
    "The translator returned an invalid response. Please retry.",
};
export function translationErrorMessage(code?: string | null) {
  return (
    messages[code ?? ""] ??
    "Translation is temporarily unavailable. Please retry."
  );
}
