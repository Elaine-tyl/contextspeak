"use client";
import { useEffect, useRef, useState } from "react";
import {
  gameSubmissionSchema,
  gameRequiresAudio,
  type GameRound,
  type GameSubmission,
  type GameReceipt,
} from "@contextspeak/contracts";
import { api, ApiError } from "../../src/api";
import { encodeRecording } from "../../src/recording";
import { usePracticeSpeech } from "../practice/usePracticeSpeech";
import { useLocalRecording } from "../practice/useLocalRecording";
import { readRoundAnswer, type RoundAnswer } from "./arcadeProtocol";
export type GameRoundProps = {
  round: GameRound;
  token: string;
  onSaved(): void | Promise<void>;
  onLocked(value: boolean): void;
  onExpired(): void;
  onLeave(): void;
  onContinue(): Promise<void>;
  levelSessionId: string;
};

export function useGameRound({
  round,
  token,
  onSaved,
  onLocked,
  onExpired,
  onContinue,
  levelSessionId,
}: GameRoundProps) {
  const speech = usePracticeSpeech(
    token,
    round.audioText,
    "en",
    !!round.recall,
  );
  const recording = useLocalRecording();
  const [response, setResponse] = useState("");
  const [choice, setChoice] = useState("");
  const [gapAnswers, setGapAnswers] = useState(() =>
    Array<string>(
      round.crossword?.length ?? Math.max(0, (round.gapText?.length ?? 1) - 1),
    ).fill(""),
  );
  const [placed, setPlaced] = useState<number[]>([]);
  const [quickStart, setQuickStart] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [advancing, setAdvancing] = useState(false);
  const [error, setError] = useState("");
  const [result, setResult] = useState<GameReceipt | null>(null);
  const attempt = useRef<GameSubmission | null>(null);
  const active = useRef(true);
  const controller = useRef<AbortController | null>(null);
  const started = useRef(false);
  const speaking = round.track === "speaking";
  const requiresAudio = gameRequiresAudio(round);
  const canAnswer =
    (!requiresAudio || speech.completedPlays > 0) &&
    (!round.recall || !speech.playing);
  const gapFill =
    round.kind === "gap-fill" ||
    round.kind === "scene-words" ||
    round.kind === "mini-crossword" ||
    !!round.recall;
  const tileKind =
    round.kind === "spelling" ||
    round.kind === "word-order" ||
    round.kind === "reading-order" ||
    round.kind === "word-scramble" ||
    round.kind === "sentence-puzzle";
  const quickFire = round.kind === "true-false";
  const tileAnswer = placed
    .map((index) => round.tiles?.[index] ?? "")
    .join(
      round.kind === "spelling" || round.kind === "word-scramble" ? "" : " ",
    );
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
    if (quickFire && speech.completedPlays > 0 && quickStart === null)
      setQuickStart(Date.now());
  }, [quickFire, speech.completedPlays, quickStart]);
  useEffect(() => {
    if (!recording.blob || started.current) return;
    const timer = setTimeout(() => {
      started.current = true;
      void submit(recording.blob!);
    }, 0);
    return () => clearTimeout(timer);
  }, [recording.url]);
  async function submit(blob?: Blob, override?: RoundAnswer) {
    if (busy || result || !canAnswer) return;
    const answer = readRoundAnswer(
      override ??
        (gapFill
          ? { gapAnswers }
          : round.kind === "dictation"
            ? { response }
            : tileKind
              ? { response: tileAnswer }
              : quickFire && quickStart !== null
                ? {
                    choice,
                    elapsedMs: Math.min(600000, Date.now() - quickStart),
                  }
                : { choice }),
      round,
    );
    if (!speaking && !attempt.current && !answer) return;
    if (blob && blob.size > 2 * 1024 * 1024) {
      setError("Record a shorter answer: AI uploads are limited to 2 MB.");
      return;
    }
    if (!attempt.current && answer) {
      if ("response" in answer) setResponse(answer.response);
      if ("elapsedMs" in answer && answer.elapsedMs !== undefined)
        setQuickStart((start) => start ?? Date.now() - answer.elapsedMs!);
      if ("choice" in answer) setChoice(answer.choice);
      if ("gapAnswers" in answer) setGapAnswers(answer.gapAnswers);
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
            : answer),
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
  function startRecording() {
    if (
      !speaking ||
      !speech.completedPlays ||
      speech.playing ||
      recordingBusy ||
      frozen
    )
      return;
    started.current = false;
    void recording.start();
  }
  return {
    speech,
    requiresAudio,
    canAnswer,
    recording,
    response,
    setResponse,
    choice,
    setChoice,
    gapAnswers,
    setGapAnswers,
    busy,
    advancing,
    error,
    result,
    speaking,
    gapFill,
    gapsComplete,
    tileKind,
    quickFire,
    placed,
    setPlaced,
    tileAnswer,
    quickStart,
    recordingBusy,
    submit,
    nextQuestion,
    frozen,
    startRecording,
    pending: !!attempt.current,
  };
}
