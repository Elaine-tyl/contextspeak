"use client";
import { useEffect, useRef, useState } from "react";
export function useLocalSpeech(language: string | null) {
  const [voice, setVoice] = useState<SpeechSynthesisVoice | null>(null);
  const [status, setStatus] = useState("");
  const [playing, setPlaying] = useState(false);
  const [plays, setPlays] = useState(0);
  const [completedPlays, setCompletedPlays] = useState(0);
  const current = useRef<SpeechSynthesisUtterance | null>(null);
  const deadline = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  function stop() {
    const owned = current.current;
    current.current = null;
    clearTimeout(deadline.current);
    if (owned) window.speechSynthesis.cancel();
  }
  useEffect(() => {
    if (!("speechSynthesis" in window)) return;
    function voices() {
      const code = language?.split("-")[0].toLowerCase();
      setVoice(
        window.speechSynthesis
          .getVoices()
          .find(
            (candidate) =>
              candidate.localService &&
              candidate.lang.split("-")[0].toLowerCase() === code,
          ) ?? null,
      );
    }
    voices();
    window.speechSynthesis.addEventListener("voiceschanged", voices);
    return () => {
      stop();
      window.speechSynthesis.removeEventListener("voiceschanged", voices);
    };
  }, [language]);
  function play(text: string) {
    if (!voice || current.current || plays >= 20) return;
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.voice = voice;
    utterance.lang = voice.lang;
    current.current = utterance;
    setPlaying(true);
    setStatus("Starting audio…");
    let started = false;
    function finish(message: string, completed: boolean) {
      if (current.current !== utterance) return;
      current.current = null;
      clearTimeout(deadline.current);
      setPlaying(false);
      setStatus(message);
      if (completed && started) setCompletedPlays((count) => count + 1);
    }
    utterance.onstart = () => {
      if (current.current !== utterance) return;
      started = true;
      setPlays((count) => count + 1);
      setStatus("Playing audio…");
    };
    utterance.onend = () => finish("Playback complete.", true);
    utterance.onerror = () =>
      finish("Audio was interrupted. Try playing it again.", false);
    deadline.current = setTimeout(() => {
      finish("Audio did not finish. Check your device voice and retry.", false);
      window.speechSynthesis.cancel();
    }, 120000);
    try {
      window.speechSynthesis.speak(utterance);
    } catch {
      finish(
        "Audio could not start. Check your device voice and retry.",
        false,
      );
    }
  }
  return { available: !!voice, status, playing, plays, completedPlays, play };
}
