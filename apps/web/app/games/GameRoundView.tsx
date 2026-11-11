"use client";
import { useEffect, useRef, useState } from "react";
import {
  gameSubmissionSchema,
  type GameRound,
  type GameSubmission,
  type GameReceipt,
} from "@contextspeak/contracts";
import { api, ApiError } from "../../src/api";
import { encodeRecording } from "../../src/recording";
import { usePracticeSpeech } from "../practice/usePracticeSpeech";
import { useLocalRecording } from "../practice/useLocalRecording";
import { RecordingPlayback } from "../practice/RecordingPlayback";
import { SpeakingFeedback } from "../practice/SpeakingFeedback";
import { DictationResult } from "../practice/DictationResult";
import { EditionNotice, GameSourceCredit } from "./GameContext";
import { Picture } from "./Picture";
import { GapFillSentence, GapFillResult } from "./GapFill";
export function GameRoundView({
  round,
  token,
  onSaved,
  onLocked,
  onExpired,
  onLeave,
  onContinue,
  levelSessionId,
}: {
  round: GameRound;
  token: string;
  onSaved(): void | Promise<void>;
  onLocked(value: boolean): void;
  onExpired(): void;
  onLeave(): void;
  onContinue(): Promise<void>;
  levelSessionId: string;
}) {
  const speech = usePracticeSpeech(token, round.audioText, "en");
  const recording = useLocalRecording();
  const [response, setResponse] = useState("");
  const [choice, setChoice] = useState("");
  const [gapAnswers, setGapAnswers] = useState(() =>
    Array<string>(Math.max(0, (round.gapText?.length ?? 1) - 1)).fill(""),
  );
  const [busy, setBusy] = useState(false);
  const [advancing, setAdvancing] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<GameReceipt | null>(null);
  const attempt = useRef<GameSubmission | null>(null);
  const active = useRef(true);
  const controller = useRef<AbortController | null>(null);
  const started = useRef(false);
  const speaking = round.track === "speaking";
  const gapFill = round.kind === "gap-fill";
  const gapsComplete =
    gapAnswers.length > 0 && gapAnswers.every((word) => word.trim());
  const recordingBusy =
    recording.state === "recording" || recording.state === "requesting";
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    if (!recording.blob || started.current) return;
    const timer = setTimeout(() => {
      started.current = true;
      void submit(recording.blob!);
    }, 0);
    return () => clearTimeout(timer);
  }, [recording.url]);
  async function submit(blob?: Blob) {
    if (busy || result || !speech.completedPlays) return;
    if (gapFill && !gapsComplete) return;
    if (blob && blob.size > 2 * 1024 * 1024) {
      setError("Record a shorter answer: AI uploads are limited to 2 MB.");
      return;
    }
    setBusy(true);
    setError("");
    onLocked(true);
    const request = new AbortController();
    controller.current = request;
    try {
      if (!attempt.current) {
        const audio = blob ? await encodeRecording(blob) : undefined;
        if (!active.current || request.signal.aborted) return;
        attempt.current = gameSubmissionSchema.parse({
          eventId: crypto.randomUUID(),
          levelSessionId,
          roundId: round.id,
          completedPlays: speech.completedPlays,
          ...(speaking
            ? {
                audio,
                mimeType: blob?.type.split(";")[0],
                durationMs: recording.durationMs,
              }
            : gapFill
              ? { gapAnswers }
              : round.kind === "dictation"
                ? { response }
                : { choice }),
        });
      }
      const value = await api<GameReceipt>(
        token,
        "/v1/games/attempts",
        "POST",
        attempt.current,
        { signal: request.signal, timeoutMs: 65000 },
      );
      if (!active.current || request.signal.aborted) return;
      setResult(value);
      attempt.current = null;
      await onSaved();
      if (active.current) onLocked(false);
    } catch (error) {
      if (!active.current || request.signal.aborted) return;
      if (!attempt.current) onLocked(false);
      if (error instanceof ApiError && [401, 403].includes(error.status))
        onExpired();
      else
        setError(
          error instanceof ApiError && error.status === 429
            ? "AI quota reached or busy. Retry this recording later."
            : "Result not confirmed; it may already be saved. Retry the same answer or leave this round.",
        );
    } finally {
      if (active.current) setBusy(false);
    }
  }
  async function nextQuestion() {
    setAdvancing(true);
    try {
      await onContinue();
    } finally {
      if (active.current) setAdvancing(false);
    }
  }
  const frozen = busy || advancing || !!attempt.current || !!result;
  return (
    <article className="game-round">
      <p className="eyebrow">
        Level {round.level} · {round.track} · {round.genre ?? round.kind}
      </p>
      <h2>{round.title}</h2>
      <EditionNotice archived={round.archived} />
      <GameSourceCredit source={round.source} />
      <p>{round.instruction}</p>
      {!!round.criteria?.length && (
        <section aria-label="Answer requirements">
          <h3>Answer requirements</h3>
          <ul>
            {round.criteria.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </section>
      )}
      {round.opponentScore !== undefined && (
        <p className="game-opponent">
          Echo · simulated AI opponent · fixed training target:{" "}
          {round.opponentScore}/100. No other player is connected.
        </p>
      )}
      <button
        className="primary"
        disabled={
          speech.playing ||
          recordingBusy ||
          busy ||
          advancing ||
          speech.plays >= 20
        }
        onClick={() => void speech.play()}
      >
        Play challenge
      </button>
      {speech.status && <p role="status">{speech.status}</p>}
      {speech.error && <p role="alert">{speech.error}</p>}
      {speaking ? (
        <>
          <p className="game-prompt">
            <strong>Challenge:</strong> {round.audioText}
          </p>
          <small>
            Completed recordings go automatically to Google Gemini for feedback.
            ContextSpeak saves the transcript and feedback, not raw audio.
          </small>
          {recording.state === "requesting" ? (
            <button className="secondary" onClick={recording.cancel}>
              Cancel microphone request
            </button>
          ) : recording.state === "recording" ? (
            <button className="primary" onClick={recording.stop}>
              Stop recording
            </button>
          ) : (
            <button
              className="primary"
              disabled={!speech.completedPlays || speech.playing || frozen}
              onClick={() => {
                started.current = false;
                void recording.start();
              }}
            >
              Start recording
            </button>
          )}
          {recording.url && (
            <RecordingPlayback
              key={recording.url}
              url={recording.url}
              onReplayed={() => {}}
            />
          )}
          {recording.error && <p role="alert">{recording.error}</p>}
        </>
      ) : gapFill ? (
        <GapFillSentence
          parts={round.gapText!}
          answers={gapAnswers}
          disabled={frozen || !speech.completedPlays}
          onChange={setGapAnswers}
          onSubmit={() => void submit()}
        />
      ) : round.kind === "dictation" ? (
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
        <div className="game-choices" aria-label="Answer choices">
          {round.choices?.map((item, index) => (
            <button
              key={item.id}
              className={
                choice === item.id ? "game-choice selected" : "game-choice"
              }
              disabled={frozen || !speech.completedPlays}
              aria-pressed={choice === item.id}
              aria-label={
                item.picture
                  ? `Choose ${item.label.toLowerCase()} picture`
                  : item.label
              }
              onClick={() => setChoice(item.id)}
            >
              {item.picture ? (
                <>
                  <Picture kind={item.picture} />
                  <span>Picture {index + 1}</span>
                </>
              ) : (
                item.label
              )}
            </button>
          ))}
        </div>
      )}
      {!speaking && !result && (
        <button
          className="primary"
          disabled={
            frozen ||
            !speech.completedPlays ||
            (gapFill
              ? !gapsComplete
              : round.kind === "dictation"
                ? !/[\p{L}\p{N}]/u.test(response)
                : !choice)
          }
          onClick={() => void submit()}
        >
          Check answer
        </button>
      )}
      {busy && (
        <p role="status">
          {speaking ? "Assessing your recording…" : "Checking answer…"}
        </p>
      )}
      {error && (
        <>
          <p role="alert">{error}</p>
          <button
            className="secondary"
            disabled={busy}
            onClick={() => void submit(recording.blob ?? undefined)}
          >
            Retry answer
          </button>
          <button className="secondary" disabled={busy} onClick={onLeave}>
            Discard pending answer
          </button>
        </>
      )}
      {result && (
        <section aria-label="Game result">
          <h3>
            {result.score === null
              ? "More audio evidence needed"
              : "Answer checked"}
          </h3>
          {result.feedback.opponentScore !== undefined &&
            result.score !== null && (
              <p className="game-match-result">
                {result.score > result.feedback.opponentScore
                  ? "You win"
                  : result.score === result.feedback.opponentScore
                    ? "Draw"
                    : "Echo wins this round"}{" "}
                · You {result.score} : Echo {result.feedback.opponentScore}
              </p>
            )}
          {result.feedback.dictation && (
            <DictationResult
              feedback={result.feedback.dictation}
              response={response}
              language="en"
            />
          )}
          {result.feedback.gapFill && (
            <GapFillResult feedback={result.feedback.gapFill} />
          )}
          {result.feedback.speaking && (
            <SpeakingFeedback result={result.feedback.speaking} />
          )}
          {result.feedback.correctAnswer && (
            <p>
              Correct answer: <strong>{result.feedback.correctAnswer}</strong>
            </p>
          )}
          <p>{result.feedback.explanation}</p>
          <p>
            Your answer is saved. Your level score is shown when all questions
            are complete.
          </p>
          <button
            className="primary"
            disabled={busy || advancing}
            onClick={() => void nextQuestion()}
          >
            {advancing ? "Loading question…" : "Next question"}
          </button>
        </section>
      )}
      <p className="section-copy">
        Game progress is separate from vocabulary recall and CEFR levels.
      </p>
    </article>
  );
}
