"use client";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import type {
  PracticeReceipt,
  PracticeSubmission,
  PracticeTask,
  DictationFeedback,
} from "@contextspeak/contracts";
import { api, ApiError } from "../../src/api";
import { usePracticeSpeech } from "./usePracticeSpeech";
import { DictationResult } from "./DictationResult";
import { SpeakingAssessmentPanel } from "./SpeakingAssessmentPanel";
import { RecordingPlayback } from "./RecordingPlayback";
import { useLocalRecording } from "./useLocalRecording";
export function PracticeSession({
  task,
  conversation,
  nextLibrary = "/library?view=practice",
  token,
  mode,
  onSaved,
  onLocked,
  onExpired,
  onDiscard,
}: {
  task: PracticeTask;
  conversation?: { previousTurnId?: string };
  nextLibrary?: string;
  token: string;
  mode: PracticeSubmission["mode"];
  onSaved(receipt: PracticeReceipt): void;
  onLocked(locked: boolean): void;
  onExpired(): void;
  onDiscard(): void;
}) {
  const question = !task.scenario ? task.speakingQuestion : undefined;
  const speech = usePracticeSpeech(
    token,
    mode === "speaking" ? (question ?? task.sourceText) : task.sourceText,
    task.language,
  );
  const [feedback, setFeedback] = useState<DictationFeedback | null>(null);
  const recording = useLocalRecording();
  const [response, setResponse] = useState("");
  const [revealed, setRevealed] = useState(false);
  const [replayed, setReplayed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [assessing, setAssessing] = useState(false);
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
    ? speech.completedPlays > 0 && /[\p{L}\p{N}]/u.test(response)
    : !!recording.url && replayed;
  async function save(selfCheck?: PracticeSubmission["selfCheck"]) {
    if (
      !ready ||
      assessing ||
      busy ||
      saved ||
      (attempt.current && attempt.current.selfCheck !== selfCheck)
    )
      return;
    attempt.current ??= {
      eventId: crypto.randomUUID(),
      lookupId: task.lookupId,
      mode,
      ...(task.scenario ? { scenario: task.scenario.id } : {}),
      ...(listening ? { activity: "dictation" as const } : { selfCheck }),
      plays: speech.plays,
      completedPlays: speech.completedPlays,
      revealed: listening ? false : revealed,
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
      if (listening && !receipt.feedback)
        throw new Error("Missing dictation result");
      setFeedback(receipt.feedback ?? null);
      setSaved(true);
      onLocked(false);
      onSaved(receipt);
    } catch (error) {
      if (!active.current) return;
      if (error instanceof ApiError && [401, 403].includes(error.status))
        onExpired();
      else
        setError(
          listening
            ? "Answer not confirmed. Retry Submit answer; your text is kept."
            : "Practice not confirmed. Retry the same self-check; your response is kept.",
        );
    } finally {
      if (active.current) setBusy(false);
    }
  }
  const frozen = !!attempt.current || assessing;
  return (
    <article className="lookup-card practice-card">
      <h2>{listening ? "Listen first" : "Speak in your own words"}</h2>
      <p className="section-copy">
        {listening
          ? "Listen to the AI voice and type exactly what you hear. Submit to see your mark and corrections."
          : task.scenario
            ? "Listen to your partner, record your reply using the saved expression. Feedback appears automatically when you finish recording. Replay is optional."
            : "Answer the question using the expression. Feedback appears automatically when recording finishes. Replay is optional; local self-checks remain available after replay."}
      </p>
      {(!listening || task.scenario) && (
        <p>
          Use this expression: <strong>{task.expression}</strong>
        </p>
      )}
      {!listening && question && (
        <p className="practice-question">
          <strong>Question:</strong> {question}
        </p>
      )}
      {!listening && (
        <small>
          Your completed recording is automatically sent to Google Gemini for
          feedback.
        </small>
      )}
      <>
        <button
          className="primary"
          disabled={
            !speech.available ||
            speech.playing ||
            recordingBusy ||
            (frozen && !saved) ||
            speech.plays >= 20
          }
          onClick={() => void speech.play()}
        >
          Play audio
        </button>
        {!speech.available && (
          <p>
            AI audio needs a supported, known source language. Choose another
            saved translation to enable playback.
          </p>
        )}
        {speech.error ? (
          <p role="alert">{speech.error}</p>
        ) : (
          speech.status && <p role="status">{speech.status}</p>
        )}
        <p className="section-copy">
          {speech.plays} plays · {speech.completedPlays} completed
        </p>
      </>
      {listening ? (
        <label className="practice-response">
          Type what you hear
          <textarea
            value={response}
            maxLength={2000}
            disabled={frozen}
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
              disabled={
                frozen ||
                speech.playing ||
                (!!conversation && !speech.completedPlays)
              }
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
                {Math.ceil(recording.durationMs / 1000)} seconds recorded.
                Completed English recordings are automatically sent for AI
                feedback. ContextSpeak saves feedback, not the recording.
              </p>
            </>
          )}
          {recording.error && <p role="alert">{recording.error}</p>}
        </div>
      )}
      {!listening && recording.blob && (
        <SpeakingAssessmentPanel
          key={recording.url}
          blob={recording.blob}
          task={task}
          token={token}
          conversation={conversation}
          durationMs={recording.durationMs}
          replayed={replayed}
          revealed={revealed}
          disabled={
            recordingBusy ||
            speech.playing ||
            busy ||
            (!!attempt.current && !saved)
          }
          onLocked={(value) => {
            setAssessing(value);
            onLocked(value);
          }}
          onSaved={onSaved}
          onExpired={onExpired}
          onDiscard={onDiscard}
        />
      )}
      {!listening && !revealed && (
        <button
          className="secondary"
          disabled={frozen || recordingBusy}
          onClick={() => setRevealed(true)}
        >
          {task.scenario ? "Show partner's turn" : "Show example"}
        </button>
      )}
      {!listening && revealed && (
        <div className="practice-answer">
          <p>{task.sourceText}</p>
          {task.scenario && <small>English meaning guide</small>}
          <p className="meaning">{task.translation}</p>
          <small>
            {task.scenario
              ? "Partner's turn · text shown during this attempt"
              : "Saved learning material · text shown during this attempt"}
          </small>
        </div>
      )}
      {listening ? (
        <>
          <button
            className="primary"
            disabled={!ready || speech.playing || busy || saved}
            onClick={() => void save()}
          >
            {busy ? "Checking answer…" : "Submit answer"}
          </button>
          {feedback && (
            <DictationResult
              feedback={feedback}
              response={response}
              language={task.language}
            >
              <div className="review-actions" aria-label="Continue practice">
                <button
                  className="secondary"
                  disabled={
                    !speech.available || speech.playing || speech.plays >= 20
                  }
                  onClick={() => void speech.play()}
                >
                  Replay audio
                </button>
                <button className="primary" onClick={onDiscard}>
                  Try again
                </button>
                <Link className="secondary" href={nextLibrary}>
                  Choose next expression
                </Link>
              </div>
            </DictationResult>
          )}
        </>
      ) : !conversation ? (
        <div className="review-actions" aria-label="Self-check practice">
          {(["needs-practice", "comfortable"] as const).map((value) => (
            <button
              key={value}
              className="secondary"
              disabled={
                !ready ||
                assessing ||
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
      ) : null}
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
        Dictation marks measure typed word accuracy. Speaking self-checks and AI
        estimates do not prove mastery. Neither changes your vocabulary review
        schedule.
      </p>
    </article>
  );
}
