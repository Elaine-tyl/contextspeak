"use client";
import { useAiSpeech } from "../../src/useAiSpeech";
export function SpeakerButton({
  text,
  language,
  token,
  label = "Listen to selected text",
}: {
  text: string;
  language: string | null;
  token: string;
  label?: string;
}) {
  const speech = useAiSpeech(token, text, language);
  return (
    <>
      <button
        className="word-speaker"
        type="button"
        aria-label={label}
        title={
          speech.available
            ? speech.busy
              ? "Preparing AI voice…"
              : "Listen with AI voice"
            : "A detected source language and sign-in are required."
        }
        disabled={!speech.available || speech.busy}
        onClick={() => void speech.play()}
      >
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          aria-hidden="true"
        >
          <path d="M11 5 6 9H3v6h3l5 4V5Z M15 8a6 6 0 0 1 0 8 M18 5a10 10 0 0 1 0 14" />
        </svg>
      </button>
      {speech.error && (
        <small className="speech-error" role="alert">
          {speech.error}
        </small>
      )}
    </>
  );
}
