"use client";
import { useEffect, useRef, useState } from "react";
import type {
  PracticeTask,
  PracticeReceipt,
  SpeakingUpload,
  SpeakingAssessment,
} from "@contextspeak/contracts";
import { speakingUploadSchema } from "@contextspeak/contracts";
import { SpeakingFeedback } from "./SpeakingFeedback";
import { api, ApiError } from "../../src/api";
async function encode(blob: Blob) {
  const bytes = new Uint8Array(await blob.arrayBuffer());
  // A full recording can exceed the argument limit; encode in bounded chunks.
  const chunks: string[] = [];
  for (let offset = 0; offset < bytes.length; offset += 32768)
    chunks.push(String.fromCharCode(...bytes.subarray(offset, offset + 32768)));
  return btoa(chunks.join(""));
}
export function SpeakingAssessmentPanel({
  blob,
  conversation,
  task,
  token,
  durationMs,
  replayed,
  revealed,
  disabled,
  onLocked,
  onSaved,
  onExpired,
  onDiscard,
}: {
  blob: Blob;
  conversation?: { previousTurnId?: string };
  task: PracticeTask;
  token: string;
  durationMs: number;
  replayed: boolean;
  revealed: boolean;
  disabled: boolean;
  onLocked(value: boolean): void;
  onSaved(receipt: PracticeReceipt): void;
  onExpired(): void;
  onDiscard(): void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<SpeakingAssessment | null>(null);
  const attempt = useRef<SpeakingUpload | null>(null);
  const active = useRef(true);
  const started = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    if (disabled || task.language !== "en" || started.current) return;
    // Defer until React's development cleanup has run; each recording submits once.
    const timer = setTimeout(() => {
      started.current = true;
      void submit();
    }, 0);
    return () => clearTimeout(timer);
  }, [disabled, task.language]);
  async function submit() {
    if (busy || result || disabled) return;
    if (blob.size > 2 * 1024 * 1024) {
      setError(
        "This recording exceeds 2 MB. Record a shorter response for AI assessment.",
      );
      return;
    }
    setBusy(true);
    setError("");
    onLocked(true);
    const request = new AbortController();
    controller.current = request;
    try {
      if (!attempt.current) {
        const audio = await encode(blob);
        if (!active.current || request.signal.aborted) return;
        const parsed = speakingUploadSchema.safeParse({
          eventId: crypto.randomUUID(),
          ...(conversation ? { conversation: true, ...conversation } : {}),
          lookupId: task.lookupId,
          ...(task.scenario ? { scenario: task.scenario.id } : {}),
          replayed,
          revealed,
          durationMs,
          mimeType: blob.type.split(";")[0],
          audio,
        });
        if (!parsed.success) {
          onLocked(false);
          setError(
            "This recording format cannot be assessed. Try a shorter recording in a supported browser.",
          );
          return;
        }
        attempt.current = parsed.data;
      }
      const receipt = await api<PracticeReceipt>(
        token,
        `/v1/practice/${task.itemId}/speaking-assessment`,
        "POST",
        attempt.current,
        { signal: request.signal, timeoutMs: 65000 },
      );
      if (!active.current || request.signal.aborted) return;
      if (!receipt.speakingAssessment) throw new Error("Missing assessment");
      setResult(receipt.speakingAssessment);
      attempt.current = null;
      onLocked(false);
      onSaved(receipt);
    } catch (error) {
      if (!active.current || request.signal.aborted) return;
      if (!attempt.current) onLocked(false);
      if (error instanceof ApiError && [401, 403].includes(error.status))
        onExpired();
      else
        setError(
          error instanceof ApiError && error.status === 429
            ? "AI assessment quota reached or busy. Result not confirmed; retry later with this recording."
            : "AI assessment not confirmed. Retry the same recording; it stays in this tab.",
        );
    } finally {
      if (active.current) setBusy(false);
    }
  }
  const frozen = !!attempt.current || busy;
  return (
    <section
      className="speaking-assessment"
      aria-label="AI speaking assessment"
    >
      <h3>AI speaking feedback</h3>
      <small>
        Your completed recording and exercise context are sent automatically to
        Google Gemini.
        {conversation && " Earlier dialogue text is included."} ContextSpeak
        saves feedback, not the recording.
      </small>
      {!!error && !result && (
        <button
          className="primary"
          disabled={busy || disabled || task.language !== "en"}
          onClick={() => void submit()}
        >
          Retry AI feedback
        </button>
      )}
      {task.language !== "en" && (
        <p>AI speaking assessment currently supports English exercises.</p>
      )}
      {busy && <p role="status">Assessing your recording…</p>}
      {error && (
        <>
          <p role="alert">{error}</p>
          {frozen && (
            <>
              <p>
                The result may already be saved. Retry with the same recording
                or discard this attempt.
              </p>
              <button
                className="secondary"
                disabled={busy}
                onClick={() => {
                  onLocked(false);
                  onDiscard();
                }}
              >
                Discard assessment and start again
              </button>
            </>
          )}
        </>
      )}
      {result && (
        <>
          <SpeakingFeedback result={result} />
          <button className="secondary" onClick={onDiscard}>
            Record another response
          </button>
        </>
      )}
    </section>
  );
}
