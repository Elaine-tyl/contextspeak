const messages: Record<string, string> = {
  GEMINI_NOT_CONFIGURED:
    "Gemini needs a server-side API key. Complete the Gemini setup guide.",
  GEMINI_QUOTA_EXCEEDED:
    "Gemini usage limit reached. Try later; no automatic paid fallback is used.",
  GEMINI_ACCESS_DENIED:
    "Gemini access was denied. Check the server-side key and project permissions.",
  GEMINI_UNAVAILABLE: "Gemini is temporarily unavailable. Please retry later.",
  AI_SELECTION_TOO_LONG:
    "Select a shorter passage: local AI supports up to 600 characters per selection.",
  MODEL_UNAVAILABLE: "Local AI is unavailable. Start Ollama and retry.",
  MODEL_MISMATCH:
    "The pinned AI model is missing or changed. See the local AI setup guide.",
  MODEL_INVALID_RESPONSE:
    "AI returned an incomplete or invalid learning card. Please retry.",
  MODEL_BUSY: "Local AI is busy. Please retry shortly.",
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
