"use client";
import { useEffect, useRef, useState } from "react";
import type {
  PracticeReceipt,
  PracticeSubmission,
  PracticeTask,
} from "@contextspeak/contracts";
import { api, ApiError } from "../../src/api";
import { useLocalSpeech } from "./useLocalSpeech";
import { RecordingPlayback } from "./RecordingPlayback";
import { useLocalRecording } from "./useLocalRecording";
export function PracticeSession({
  task,
  token,
  mode,
  onSaved,
  onLocked,
  onExpired,
  onDiscard,
}: {
  task: PracticeTask;
  token: string;
  mode: PracticeSubmission["mode"];
  onSaved(receipt: PracticeReceipt): void;
  onLocked(locked: boolean): void;
  onExpired(): void;
  onDiscard(): void;
}) {
  const speech = useLocalSpeech(task.language);
  const recording = useLocalRecording();
  const [response, setResponse] = useState("");
  const [revealed, setRevealed] = useState(false);
  const [replayed, setReplayed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState("");
  const attempt = useRef<PracticeSubmission | null>(null);
  const active = useRef(true);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
    };
  }, []);
  const listening = mode === "listening";
  const recordingBusy =
    recording.state === "requesting" || recording.state === "recording";
  const ready = listening
    ? speech.completedPlays > 0 && revealed && !!response.trim()
    : !!recording.url && replayed;
  async function save(selfCheck: PracticeSubmission["selfCheck"]) {
    if (
      !ready ||
      busy ||
      saved ||
      (attempt.current && attempt.current.selfCheck !== selfCheck)
    )
      return;
    attempt.current ??= {
      eventId: crypto.randomUUID(),
      lookupId: task.lookupId,
      mode,
      selfCheck,
      plays: speech.plays,
      completedPlays: speech.completedPlays,
      revealed,
      replayed,
      response,
      durationMs: listening ? 0 : recording.durationMs,
    };
    setBusy(true);
    setError("");
    onLocked(true);
    try {
      const receipt = await api<PracticeReceipt>(
        token,
        `/v1/practice/${task.itemId}/attempts`,
        "POST",
        attempt.current,
      );
      if (!active.current) return;
      setSaved(true);
      onLocked(false);
      onSaved(receipt);
    } catch (error) {
      if (!active.current) return;
      if (error instanceof ApiError && [401, 403].includes(error.status))
        onExpired();
      else
        setError(
          "Practice not confirmed. Retry the same self-check; your response is kept.",
        );
    } finally {
      if (active.current) setBusy(false);
    }
  }
  const frozen = !!attempt.current;
  return (
    <article className="lookup-card practice-card">
      <h2>{listening ? "Listen first" : "Speak in your own words"}</h2>
      <p className="section-copy">
        {listening
          ? "Listen, write the meaning, then reveal the text to compare."
          : "Record a sentence using the expression. Replay it and self-check before starting another attempt."}
      </p>
      {!listening && (
        <p>
          Use this expression: <strong>{task.expression}</strong>
        </p>
      )}
      {(listening || revealed) && (
        <>
          <button
            className="primary"
            disabled={
              !speech.available ||
              speech.playing ||
              recordingBusy ||
              frozen ||
              speech.plays >= 20
            }
            onClick={() => speech.play(task.sourceText)}
          >
            Play audio
          </button>
          {!speech.available && (
            <p>
              No on-device voice is available for{" "}
              {task.language ?? "this language"}. Install a matching device
              voice to enable listening. No remote voice is used.
            </p>
          )}
          {speech.status && <p role="status">{speech.status}</p>}
          <p className="section-copy">
            {speech.plays} plays · {speech.completedPlays} completed
          </p>
        </>
      )}
      {listening ? (
        <label className="practice-response">
          What did it mean?
          <textarea
            value={response}
            maxLength={2000}
            disabled={revealed || frozen}
            onChange={(event) => setResponse(event.target.value)}
          />
        </label>
      ) : (
        <div className="practice-recording">
          {recording.state === "requesting" ? (
            <button className="secondary" onClick={recording.cancel}>
              Cancel microphone request
            </button>
          ) : recording.state === "recording" ? (
            <>
              <p role="status">Recording… Maximum 60 seconds.</p>
              <button className="primary" onClick={recording.stop}>
                Stop recording
              </button>
            </>
          ) : (
            <button
              className="primary"
              disabled={frozen || speech.playing}
              onClick={() => {
                setReplayed(false);
                void recording.start();
              }}
            >
              Start recording
            </button>
          )}
          {recording.url && (
            <>
              <RecordingPlayback
                key={recording.url}
                url={recording.url}
                onReplayed={setReplayed}
              />
              {!replayed && (
                <p>
                  Replay your recording to the end before saving a self-check.
                </p>
              )}
              <p className="section-copy">
                {Math.ceil(recording.durationMs / 1000)} seconds recorded. Audio
                stays in this tab and is discarded when you leave.
              </p>
            </>
          )}
          {recording.error && <p role="alert">{recording.error}</p>}
        </div>
      )}
      {!revealed && (
        <button
          className="secondary"
          disabled={
            frozen ||
            recordingBusy ||
            (listening && (!speech.completedPlays || !response.trim()))
          }
          onClick={() => setRevealed(true)}
        >
          {listening ? "Reveal transcript and meaning" : "Show example"}
        </button>
      )}
      {revealed && (
        <div className="practice-answer">
          <p>{task.sourceText}</p>
          <p className="meaning">{task.translation}</p>
          <small>
            Saved learning material · text shown during this attempt
          </small>
        </div>
      )}
      <div className="review-actions" aria-label="Self-check practice">
        {(["needs-practice", "comfortable"] as const).map((value) => (
          <button
            key={value}
            className="secondary"
            disabled={
              !ready ||
              recordingBusy ||
              speech.playing ||
              busy ||
              saved ||
              (!!attempt.current && attempt.current.selfCheck !== value)
            }
            onClick={() => void save(value)}
          >
            {value === "comfortable" ? "Comfortable" : "Needs practice"}
          </button>
        ))}
      </div>
      {error && (
        <>
          <p role="alert">{error}</p>
          <p className="section-copy">
            The previous result may already be saved. Starting again discards
            this pending attempt.
          </p>
          <button className="secondary" disabled={busy} onClick={onDiscard}>
            Start a new attempt instead
          </button>
        </>
      )}
      {saved && <p role="status">Practice saved.</p>}
      <p className="section-copy">
        Guided practice and self-reported results. This does not assess
        pronunciation, prove mastery or change your vocabulary review schedule.
      </p>
    </article>
  );
}
