"use client";
import { useLocalSpeech } from "../practice/useLocalSpeech";
export function SpeakerButton({
  text,
  language,
}: {
  text: string;
  language: string | null;
}) {
  const speech = useLocalSpeech(language, Infinity);
  return (
    <>
      <button
        className="word-speaker"
        type="button"
        aria-label="Listen to selected text"
        title={
          speech.available
            ? speech.status || "Listen to selected text"
            : "No matching on-device voice. Install a device voice to listen."
        }
        disabled={!speech.available || speech.playing}
        onClick={() => speech.play(text)}
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
      <span className="speaker-status" role="status">
        {speech.status}
      </span>
    </>
  );
}
