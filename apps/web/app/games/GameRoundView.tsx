"use client";
import "./text-puzzles.css";
import { useGameRound, type GameRoundProps } from "./useGameRound";
import { RecordingPlayback } from "../practice/RecordingPlayback";
import { SpeakingFeedback } from "../practice/SpeakingFeedback";
import { DictationResult } from "../practice/DictationResult";
import { EditionNotice, GameSourceCredit } from "./GameContext";
import { GameCelebration } from "./GameEffects";
import { GameStudio } from "./GameStudio";
import type { StudioActivity } from "./studioThemes";
import { Crossword } from "./Crossword";
import { EvidenceHunt } from "./EvidenceHunt";
import { ReadingRecall } from "./ReadingRecall";
import { SceneWords, SceneWordNotebook } from "./SceneWords";
import { Picture } from "./Picture";
import { GapFillSentence, GapFillResult } from "./GapFill";
import { QuickFireTimer, TileBuilder } from "./Tiles";
export function GameRoundView(props: GameRoundProps) {
  const { round, onLeave } = props;
  const {
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
  } = useGameRound(props);
  const activity: StudioActivity = result
    ? "checked"
    : busy
      ? "checking"
      : recordingBusy
        ? "recording"
        : speech.playing
          ? "listening"
          : canAnswer
            ? "answering"
            : "ready";
  const count = gapFill
    ? gapAnswers.filter((word) => word.trim()).length
    : tileKind
      ? placed.length
      : response.trim()
        ? response.trim().split(/\s+/u).length
        : choice
          ? 1
          : 0;
  const selectedWord = round.choices?.find((item) => item.id === choice)?.label;
  return (
    <GameStudio
      round={round}
      activity={activity}
      count={count}
      action={
        requiresAudio ? (
          <button
            className="primary studio-play"
            disabled={
              speech.playing ||
              recordingBusy ||
              busy ||
              advancing ||
              !speech.available ||
              speech.plays >= 20
            }
            onClick={() => void speech.play()}
          >
            <span aria-hidden="true">{speech.playing ? "◖))" : "▶"}</span>{" "}
            {round.recall
              ? speech.completedPlays
                ? "Replay audio"
                : "Start"
              : "Play challenge"}
          </button>
        ) : (
          <span className="studio-text-badge">
            {round.track === "vocabulary" ? "Word challenge" : "Read & explore"}
          </span>
        )
      }
    >
      <EditionNotice archived={round.archived} />
      <GameSourceCredit source={round.source} hasAudio={requiresAudio} />
      {round.recall ? (
        <ReadingRecall
          round={round}
          completed={speech.completedPlays > 0}
          answers={gapAnswers}
          disabled={frozen || !canAnswer}
          onChange={setGapAnswers}
          onSubmit={() => void submit()}
        />
      ) : round.kind === "evidence-hunt" ? (
        <EvidenceHunt
          passage={round.passage}
          question={round.instruction}
          choices={round.choices ?? []}
          selected={choice}
          disabled={frozen}
          onSelect={setChoice}
        />
      ) : (
        round.passage && (
          <section
            className={`text-game-passage passage-${round.kind}`}
            aria-label={
              round.track === "reading"
                ? "Reading passage"
                : "Vocabulary prompt"
            }
          >
            {round.passage.split("\n\n").map((paragraph, index) => (
              <p key={index}>
                {round.kind === "word-context" ? (
                  <>
                    {paragraph.split("___")[0]}
                    <mark className="context-gap">{selectedWord ?? "___"}</mark>
                    {paragraph.split("___")[1]}
                  </>
                ) : (
                  paragraph
                )}
              </p>
            ))}
          </section>
        )
      )}
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
      {speech.playing && (
        <div className="game-equalizer" aria-hidden="true">
          {Array.from({ length: 8 }, (_, i) => (
            <span key={i} style={{ animationDelay: `${i * 0.08}s` }} />
          ))}
        </div>
      )}
      {speech.status && <p role="status">{speech.status}</p>}
      {speech.error && <p role="alert">{speech.error}</p>}
      {round.recall && !speech.available && (
        <p role="alert">
          This passage cannot be read. Refresh the level or choose another
          passage.
        </p>
      )}
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
      ) : round.kind === "scene-words" ? (
        <SceneWords
          round={round}
          value={gapAnswers[0] ?? ""}
          disabled={frozen || !canAnswer}
          activity={activity}
          score={result?.score}
          onChange={(value) => setGapAnswers([value])}
          onSubmit={() => void submit()}
        />
      ) : round.kind === "mini-crossword" ? (
        <Crossword
          entries={round.crossword ?? []}
          answers={gapAnswers}
          disabled={frozen}
          onChange={setGapAnswers}
          onSubmit={() => void submit()}
        />
      ) : round.recall ? null : gapFill ? (
        <GapFillSentence
          parts={round.gapText!}
          audioText={round.audioText}
          answers={gapAnswers}
          disabled={frozen || !canAnswer}
          onChange={setGapAnswers}
          onSubmit={() => void submit()}
        />
      ) : tileKind ? (
        <TileBuilder
          tiles={round.tiles ?? []}
          placed={placed}
          joiner={
            round.kind === "spelling" || round.kind === "word-scramble"
              ? ""
              : " "
          }
          label={
            round.kind === "spelling" || round.kind === "word-scramble"
              ? "Build the word"
              : round.kind === "reading-order"
                ? "Order the paragraphs"
                : round.kind === "sentence-puzzle"
                  ? "Build the summary"
                  : "Build the sentence"
          }
          disabled={frozen || !canAnswer}
          onChange={setPlaced}
        />
      ) : round.kind === "evidence-hunt" ? null : round.kind === "dictation" ? (
        <label className="practice-response studio-notebook">
          Type what you hear
          <textarea
            value={response}
            maxLength={2000}
            disabled={frozen}
            onChange={(event) => setResponse(event.target.value)}
          />
          <span className="studio-answer-count">
            {count} {count === 1 ? "word" : "words"}
          </span>
        </label>
      ) : (
        <>
          {quickFire && round.picture && (
            <div className="quick-picture">
              <Picture kind={round.picture} size="large" decorative={false} />
            </div>
          )}
          {quickFire && (
            <QuickFireTimer
              key={quickStart ?? "idle"}
              limit={round.timeLimit ?? 10}
              startedAt={quickStart}
              stopped={frozen}
            />
          )}
          <div
            className={`game-choices ${
              round.choices?.some((item) => item.picture)
                ? `pictures-${round.choices.length}`
                : "words"
            }`}
            aria-label="Answer choices"
          >
            {round.choices?.map((item, index) => (
              <button
                key={item.id}
                className={
                  choice === item.id ? "game-choice selected" : "game-choice"
                }
                disabled={frozen || !canAnswer}
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
        </>
      )}
      {!speaking && !result && (
        <button
          className="primary"
          disabled={
            frozen ||
            !canAnswer ||
            (gapFill
              ? !gapsComplete
              : round.kind === "dictation"
                ? !/[\p{L}\p{N}]/u.test(response)
                : tileKind
                  ? [
                      "reading-order",
                      "word-scramble",
                      "sentence-puzzle",
                    ].includes(round.kind)
                    ? placed.length !== round.tiles?.length
                    : !/[\p{L}\p{N}]/u.test(tileAnswer)
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
        <section className="game-result-panel" aria-label="Game result">
          <GameCelebration score={result.score} />
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
            <GapFillResult
              feedback={result.feedback.gapFill}
              crossword={round.kind === "mini-crossword"}
            />
          )}
          {result.feedback.sceneWordNotes && (
            <SceneWordNotebook
              notes={result.feedback.sceneWordNotes}
              token={props.token}
            />
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
    </GameStudio>
  );
}
