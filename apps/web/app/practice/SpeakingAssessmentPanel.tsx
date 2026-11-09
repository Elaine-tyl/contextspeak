"use client";
import { useEffect, useRef, useState } from "react";
import type {
  PracticeTask,
  PracticeReceipt,
  SpeakingUpload,
  SpeakingAssessment,
} from "@contextspeak/contracts";
import { speakingUploadSchema } from "@contextspeak/contracts";
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
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<SpeakingAssessment | null>(null);
  const attempt = useRef<SpeakingUpload | null>(null);
  const active = useRef(true);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
      controller.current?.abort();
    };
  }, []);
  async function submit() {
    if (!consent || !replayed || busy || result || disabled) return;
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
          lookupId: task.lookupId,
          ...(task.scenario ? { scenario: task.scenario.id } : {}),
          consent: true,
          replayed: true,
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
      <h3>Optional AI speaking assessment</h3>
      <p>
        Submit your recording and exercise prompt to Google Gemini. ContextSpeak
        saves the transcript and AI feedback, not the recording. Google
        processes uploads under its service terms and may use unpaid-service
        data for product improvement.
      </p>
      <label className="assessment-consent">
        <input
          type="checkbox"
          checked={consent}
          disabled={frozen || disabled || !!result}
          onChange={(event) => setConsent(event.target.checked)}
        />{" "}
        I agree to send this recording and exercise prompt to Google for AI
        assessment.
      </label>
      {!result && (
        <button
          className="primary"
          disabled={
            !consent || !replayed || busy || disabled || task.language !== "en"
          }
          onClick={() => void submit()}
        >
          Submit for AI assessment
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
        <div className="practice-answer" aria-label="Speaking feedback">
          <h3>
            {result.overall === null
              ? "No reliable speaking score"
              : `AI speaking score: ${result.overall}/100`}
          </h3>
          <p>
            AI-estimated practice feedback. This is not an exam grade or a
            validated pronunciation assessment.
          </p>
          {result.scores && (
            <dl className="speaking-scores">
              {(
                [
                  ["clarity", "Clarity"],
                  ["fluency", "Fluency"],
                  ["grammar", "Grammar"],
                  ["vocabulary", "Vocabulary"],
                  ["taskResponse", "Task response"],
                ] as const
              ).map(([key, label]) => (
                <div key={key}>
                  <dt>{label}</dt>
                  <dd>{result.scores![key]}/100</dd>
                </div>
              ))}
            </dl>
          )}
          {result.transcript && (
            <>
              <h4>Your transcript</h4>
              <p>{result.transcript}</p>
            </>
          )}
          {!!result.strengths.length && (
            <>
              <h4>What went well</h4>
              <ul>
                {result.strengths.map((value, index) => (
                  <li key={index}>{value}</li>
                ))}
              </ul>
            </>
          )}
          <h4>What to improve</h4>
          <ul>
            {result.improvements.map((value, index) => (
              <li key={index}>{value}</li>
            ))}
          </ul>
          {result.suggestedResponse && (
            <>
              <h4>Suggested response</h4>
              <p>{result.suggestedResponse}</p>
            </>
          )}
          <button className="secondary" onClick={onDiscard}>
            Record another response
          </button>
        </div>
      )}
    </section>
  );
}
