"use client";
import { useEffect, useRef, useState } from "react";
const maxBytes = 10 * 1024 * 1024;
export function useLocalRecording() {
  const [state, setState] = useState<
    "idle" | "requesting" | "recording" | "ready"
  >("idle");
  const [url, setUrl] = useState("");
  const [durationMs, setDurationMs] = useState(0);
  const [error, setError] = useState("");
  const generation = useRef(0);
  const stream = useRef<MediaStream | null>(null);
  const recorder = useRef<MediaRecorder | null>(null);
  const objectUrl = useRef("");
  const deadline = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const requesting = useRef(false);
  function releaseTracks() {
    stream.current?.getTracks().forEach((track) => track.stop());
    stream.current = null;
    clearTimeout(deadline.current);
  }
  function dispose() {
    generation.current++;
    requesting.current = false;
    const active = recorder.current;
    recorder.current = null;
    if (active) {
      active.ondataavailable = active.onstop = active.onerror = null;
      if (active.state !== "inactive") active.stop();
    }
    releaseTracks();
    if (objectUrl.current) URL.revokeObjectURL(objectUrl.current);
    objectUrl.current = "";
  }
  useEffect(() => {
    function hidden() {
      if (!document.hidden) return;
      if (requesting.current) cancel();
      else stop();
    }
    document.addEventListener("visibilitychange", hidden);
    window.addEventListener("pagehide", dispose);
    return () => {
      document.removeEventListener("visibilitychange", hidden);
      window.removeEventListener("pagehide", dispose);
      dispose();
    };
  }, []);
  function stop() {
    if (recorder.current?.state === "recording") recorder.current.stop();
    releaseTracks();
  }
  function cancel() {
    dispose();
    setState("idle");
    setUrl("");
    setDurationMs(0);
  }
  async function start() {
    if (requesting.current || recorder.current?.state === "recording") return;
    dispose();
    const current = generation.current;
    requesting.current = true;
    setUrl("");
    setDurationMs(0);
    setError("");
    setState("requesting");
    try {
      if (
        !window.isSecureContext ||
        !navigator.mediaDevices?.getUserMedia ||
        !("MediaRecorder" in window)
      )
        throw new Error("unsupported");
      const input = await navigator.mediaDevices.getUserMedia({ audio: true });
      // A permission dialog can resolve after the learner has left the page.
      if (current !== generation.current) {
        input.getTracks().forEach((track) => track.stop());
        return;
      }
      stream.current = input;
      const capture = new MediaRecorder(input);
      recorder.current = capture;
      const chunks: Blob[] = [];
      let bytes = 0;
      const began = performance.now();
      function fail(message: string) {
        if (current !== generation.current) return;
        dispose();
        setState("idle");
        setError(message);
      }
      capture.ondataavailable = (event) => {
        if (current !== generation.current) return;
        bytes += event.data.size;
        if (bytes > maxBytes) {
          fail("Recording exceeded 10 MB. Please record a shorter response.");
          return;
        }
        if (event.data.size) chunks.push(event.data);
      };
      capture.onerror = () =>
        fail("Recording failed. Check your microphone and retry.");
      capture.onstop = () => {
        if (current !== generation.current) return;
        releaseTracks();
        recorder.current = null;
        const blob = new Blob(chunks, {
          type: capture.mimeType || "audio/webm",
        });
        if (!blob.size) {
          fail("No audio was recorded. Check your microphone and retry.");
          return;
        }
        objectUrl.current = URL.createObjectURL(blob);
        setUrl(objectUrl.current);
        setDurationMs(
          Math.min(60000, Math.max(1, Math.round(performance.now() - began))),
        );
        setState("ready");
      };
      capture.start(1000);
      requesting.current = false;
      setState("recording");
      deadline.current = setTimeout(stop, 60000);
    } catch {
      if (current !== generation.current) return;
      dispose();
      setState("idle");
      setError(
        "Could not access your microphone. Allow microphone access on HTTPS or localhost, then retry.",
      );
    }
  }
  return { state, url, durationMs, error, start, stop, cancel };
}
