"use client";
import { useEffect, useRef, useState } from "react";
import { useAiSpeech } from "../../src/useAiSpeech";
export function usePracticeSpeech(
  token: string,
  text: string,
  language: string | null,
) {
  const speech = useAiSpeech(token, text, language);
  const [completedPlays, setCompletedPlays] = useState(0);
  const active = useRef(true);
  const pending = useRef(false);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
    };
  }, []);
  async function play() {
    if (pending.current || completedPlays >= 20 || !speech.available) return;
    pending.current = true;
    const completed = await speech.play();
    if (active.current && completed) setCompletedPlays((value) => value + 1);
    pending.current = false;
  }
  return {
    ...speech,
    // Failed/cancelled requests must not exhaust the successful-play allowance.
    plays: completedPlays,
    completedPlays,
    play,
    playing: speech.busy,
    status:
      speech.phase === "preparing"
        ? "Preparing AI audio…"
        : speech.phase === "playing"
          ? "AI audio in progress."
          : completedPlays
            ? "Playback complete."
            : "",
  };
}
