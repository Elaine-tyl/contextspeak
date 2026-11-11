"use client";
import { useGameRound, type GameRoundProps } from "./useGameRound";
import { RecordingPlayback } from "../practice/RecordingPlayback";
import { SpeakingFeedback } from "../practice/SpeakingFeedback";
import { DictationResult } from "../practice/DictationResult";
import { EditionNotice, GameSourceCredit } from "./GameContext";
import { Picture } from "./Picture";
import { GapFillSentence, GapFillResult } from "./GapFill";
export function GameRoundView(props: GameRoundProps) {
  const { round, onLeave } = props;
  const {
    speech,
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
    recordingBusy,
    submit,
    nextQuestion,
    frozen,
    startRecording,
  } = useGameRound(props);
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
              onClick={startRecording}
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
