"use client";
import { useEffect, useRef, useState } from "react";
import { speechAudioSchema, speechInputSchema } from "@contextspeak/contracts";
import { AudioPlayer } from "../../../packages/audio/player";
import { splitSpeechText } from "../../../packages/audio/text";
import { api, ApiError, type WebSession } from "./api";
const player = new AudioPlayer();
let cancelPrevious: (() => void) | undefined;
export function useAiSpeech(
  token: string,
  text: string,
  language: string | null,
  continuous = false,
) {
  const [phase, setPhase] = useState<"idle" | "preparing" | "playing">("idle");
  const [error, setError] = useState("");
  const active = useRef(true);
  const current = useRef<{
    id: string;
    controller: AbortController;
    cancel(): void;
  } | null>(null);
  function stop() {
    current.current?.cancel();
  }
  useEffect(() => {
    active.current = true;
    setPhase("idle");
    setError("");
    const hide = () => {
      if (document.visibilityState === "hidden") stop();
    };
    document.addEventListener("visibilitychange", hide);
    return () => {
      active.current = false;
      stop();
      document.removeEventListener("visibilitychange", hide);
    };
  }, [token, text, language, continuous]);
  const texts = continuous ? splitSpeechText(text) : [text];
  const inputs = texts.map((text) =>
    speechInputSchema.safeParse({ text, language }),
  );
  const available = inputs.length > 0 && inputs.every((input) => input.success);
  async function play(): Promise<boolean> {
    if (!available || !token || current.current) return false;
    cancelPrevious?.();
    const controller = new AbortController();
    const id = crypto.randomUUID();
    const attempt = {
      id,
      controller,
      cancel() {
        controller.abort();
        player.stop(id);
        if (current.current === attempt) current.current = null;
        if (cancelPrevious === attempt.cancel) cancelPrevious = undefined;
        if (active.current) setPhase("idle");
      },
    };
    current.current = attempt;
    cancelPrevious = attempt.cancel;
    setPhase("preparing");
    setError("");
    try {
      // A passage counts as one play only when every chunk finishes. Existing
      // per-request limits and cached audio are shared with short utterances.
      for (const input of inputs) {
        if (
          !input.success ||
          controller.signal.aborted ||
          current.current !== attempt
        )
          return false;
        setPhase("preparing");
        const response = await api<unknown>(
          token,
          "/v1/speech",
          "POST",
          input.data,
          { signal: controller.signal, timeoutMs: 180000 },
        );
        const audio = speechAudioSchema.parse(response);
        const session = await api<WebSession>(
          "",
          "/auth/session",
          "GET",
          undefined,
          { signal: controller.signal },
        );
        if (session.csrfToken !== token)
          throw new ApiError(403, "Session changed");
        if (controller.signal.aborted || current.current !== attempt)
          return false;
        setPhase("playing");
        const result = await player.play(id, audio);
        if (result.cancelled || !active.current || current.current !== attempt)
          return false;
      }
      return true;
    } catch (error) {
      if (
        !controller.signal.aborted &&
        active.current &&
        current.current === attempt
      )
        setError(
          error instanceof ApiError && error.status === 429
            ? "AI voice quota reached or busy. Try again shortly; cached audio remains available."
            : "AI voice is unavailable. Check your connection and sign-in, then retry.",
        );
      return false;
    } finally {
      if (current.current === attempt) {
        current.current = null;
        if (cancelPrevious === attempt.cancel) cancelPrevious = undefined;
        if (active.current) setPhase("idle");
      }
    }
  }
  return {
    busy: phase !== "idle",
    phase,
    error,
    available: !!token && available,
    play,
  };
}
