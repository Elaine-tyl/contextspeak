"use client";
import { useEffect, useRef, useState } from "react";
export function RecordingPlayback({
  url,
  onReplayed,
}: {
  url: string;
  onReplayed(value: boolean): void;
}) {
  const audio = useRef<HTMLAudioElement>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    const element = audio.current;
    if (element) element.src = url;
    return () => {
      // Revoking a blob URL alone does not stop an already decoded recording.
      element?.pause();
      element?.removeAttribute("src");
      element?.load();
    };
  }, [url]);
  return (
    <>
      <audio
        ref={audio}
        controls
        preload="none"
        aria-label="Your recording"
        onEnded={() => {
          setFailed(false);
          onReplayed(true);
        }}
        onError={() => {
          setFailed(true);
          onReplayed(false);
        }}
      />
      {failed && (
        <p role="alert">Recording playback failed. Please record again.</p>
      )}
    </>
  );
}
