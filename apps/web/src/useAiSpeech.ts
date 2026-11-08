"use client";
import { useEffect, useRef, useState } from "react";
import { speechAudioSchema, speechInputSchema } from "@contextspeak/contracts";
import { AudioPlayer } from "../../../packages/audio/player";
import { api, ApiError, type WebSession } from "./api";
const player = new AudioPlayer();
let cancelPrevious: (() => void) | undefined;
export function useAiSpeech(
  token: string,
  text: string,
  language: string | null,
) {
  const [busy, setBusy] = useState(false);
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
    setBusy(false);
    setError("");
    return () => {
      active.current = false;
      stop();
    };
  }, [token, text, language]);
  const input = speechInputSchema.safeParse({ text, language });
  async function play() {
    if (!input.success || !token || current.current) return;
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
        if (active.current) setBusy(false);
      },
    };
    current.current = attempt;
    cancelPrevious = attempt.cancel;
    setBusy(true);
    setError("");
    try {
      const response = await api<unknown>(
        token,
        "/v1/speech",
        "POST",
        input.data,
        { signal: controller.signal, timeoutMs: 35000 },
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
      if (controller.signal.aborted || current.current !== attempt) return;
      await player.play(id, audio);
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
    } finally {
      if (current.current === attempt) {
        current.current = null;
        if (cancelPrevious === attempt.cancel) cancelPrevious = undefined;
        if (active.current) setBusy(false);
      }
    }
  }
  return { busy, error, available: !!token && input.success, play };
}
