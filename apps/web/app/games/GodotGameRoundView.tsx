"use client";
import { useEffect, useRef, useState } from "react";
import { readArcadeAction } from "./arcadeProtocol";
import { useGameRound, type GameRoundProps } from "./useGameRound";
import { EditionNotice, GameSourceCredit } from "./GameContext";
import { DictationResult } from "../practice/DictationResult";
import { SpeakingFeedback } from "../practice/SpeakingFeedback";
import { RecordingPlayback } from "../practice/RecordingPlayback";
import { GapFillResult } from "./GapFill";

export function GodotGameRoundView(props: GameRoundProps) {
  const game = useGameRound(props);
  const frame = useRef<HTMLIFrameElement>(null);
  const [available, setAvailable] = useState<boolean | null>(null);
  const [ready, setReady] = useState(false);
  const playDisabled =
    game.speech.playing ||
    game.recordingBusy ||
    game.busy ||
    game.advancing ||
    game.speech.plays >= 20;
  const status = game.busy
    ? game.speaking
      ? "Assessing your recording…"
      : "Checking answer…"
    : game.speech.status;
  const error = game.error || game.speech.error || game.recording.error;
  const snapshot = {
    round: props.round,
    result: game.result,
    scored: game.result?.score !== null,
    played: game.speech.completedPlays > 0,
    playDisabled,
    speechBusy: game.speech.playing,
    frozen: game.frozen,
    busy: game.busy,
    advancing: game.advancing,
    pending: game.pending,
    recording: game.recording.state,
    status,
    error,
  };
  function sendState() {
    frame.current?.contentWindow?.postMessage(
      { channel: "contextspeak-arcade", action: "state", state: snapshot },
      window.location.origin,
    );
  }
  useEffect(() => {
    const controller = new AbortController();
    void fetch("/arcade-engine/index.html", {
      method: "HEAD",
      signal: controller.signal,
    })
      .then((response) => setAvailable(response.ok))
      .catch(() => {
        if (!controller.signal.aborted) setAvailable(false);
      });
    return () => controller.abort();
  }, []);
  useEffect(() => {
    function receive(event: MessageEvent) {
      const action = readArcadeAction(
        event,
        window.location.origin,
        frame.current?.contentWindow,
        props.round,
      );
      if (!action) return;
      switch (action.action) {
        case "ready":
          setReady(true);
          sendState();
          break;
        case "play":
          if (!playDisabled) void game.speech.play();
          break;
        case "record":
          game.startRecording();
          break;
        case "stop":
          if (game.recording.state === "recording") game.recording.stop();
          break;
        case "cancel":
          if (game.recordingBusy) game.recording.cancel();
          break;
        case "submit":
          if (!game.frozen) void game.submit(undefined, action.answer);
          break;
        case "retry":
          if (game.pending) void game.submit(game.recording.blob ?? undefined);
          break;
        case "next":
          if (game.result && !game.advancing) void game.nextQuestion();
          break;
        case "leave":
          if (!game.busy && !game.advancing) props.onLeave();
      }
    }
    window.addEventListener("message", receive);
    sendState();
    return () => window.removeEventListener("message", receive);
  });
  return (
    <article className="arcade-host" aria-label="Godot 2D game">
      <EditionNotice archived={props.round.archived} />
      <GameSourceCredit source={props.round.source} />
      {available === null && <p role="status">Checking game engine…</p>}
      {available === false && (
        <p role="alert">
          Godot export is missing. Run <code>pnpm arcade:build</code>, then
          refresh, or{" "}
          <a href={"/games/web?run=" + props.levelSessionId}>
            continue with web controls
          </a>
          .
        </p>
      )}
      {available && (
        <>
          {!ready && <p role="status">Loading Godot 2D engine…</p>}
          <iframe
            ref={frame}
            title="ContextSpeak Godot Arcade"
            src="/arcade-engine/index.html"
            className="arcade-frame"
            onLoad={sendState}
            allow="autoplay"
          />
        </>
      )}
      <p className="sr-only" role="status">
        {status}
      </p>
      {error && <p role="alert">{error}</p>}
      {game.recording.url && (
        <RecordingPlayback
          key={game.recording.url}
          url={game.recording.url}
          onReplayed={() => {}}
        />
      )}
      {game.result && (
        <section
          aria-label="Detailed game feedback"
          className="arcade-feedback"
        >
          <h3>
            {game.result.score === null
              ? "More audio evidence needed"
              : `Answer checked: ${game.result.score}/100`}
          </h3>
          {game.result.feedback.dictation && (
            <DictationResult
              feedback={game.result.feedback.dictation}
              response={game.response}
              language="en"
            />
          )}
          {game.result.feedback.gapFill && (
            <GapFillResult feedback={game.result.feedback.gapFill} />
          )}
          {game.result.feedback.speaking && (
            <SpeakingFeedback result={game.result.feedback.speaking} />
          )}
          <p>{game.result.feedback.explanation}</p>
        </section>
      )}
      <p className="section-copy">
        Game progress is saved to your account, separate from vocabulary recall
        and CEFR levels.{" "}
        <a href={"/games/web?run=" + props.levelSessionId}>
          Use accessible web controls
        </a>
        .
      </p>
    </article>
  );
}
