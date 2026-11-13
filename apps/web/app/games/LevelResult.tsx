import { GameCelebration } from "./GameEffects";
import type { GameLevelRun } from "@contextspeak/contracts";
import { DictationResult } from "../practice/DictationResult";
import { SpeakingFeedback } from "../practice/SpeakingFeedback";
import { EditionNotice, GameSourceCredit } from "./GameContext";
import { GapFillResult } from "./GapFill";
import { SceneWordNotebook } from "./SceneWords";
export function LevelResult({
  run,
  token,
  busy,
  onRetry,
  onNext,
}: {
  run: GameLevelRun;
  token: string;
  busy: boolean;
  onRetry(): void;
  onNext(): void;
}) {
  const opponent = run.receipts[0]?.feedback.opponentScore;
  return (
    <section className="game-round" aria-label="Level result">
      <GameCelebration score={run.score} complete />
      <h2>
        {run.score === null
          ? "No reliable level score"
          : `Level score: ${run.score}/100`}
      </h2>
      <EditionNotice archived={run.rounds.some((round) => round.archived)} />
      {run.kind === "pk" && run.score !== null && opponent !== undefined && (
        <p className="game-match-result">
          {run.score > opponent
            ? "You win"
            : run.score === opponent
              ? "Draw"
              : "Echo wins this round"}{" "}
          · You {run.score} : Echo {opponent}
        </p>
      )}
      <p>
        {run.passed
          ? "Level passed. Well done!"
          : run.score === null
            ? "At least one recording could not be scored reliably. Retry with clearer audio; this is not counted as a zero."
            : "Keep practising. Reach 70/100 to unlock the next level."}
      </p>
      <p>
        {run.receipts.length} of {run.rounds.length} questions completed · Level{" "}
        {run.level}
      </p>
      <div className="practice-tabs" aria-label="Level actions">
        <button className="secondary" disabled={busy} onClick={onRetry}>
          Retry
        </button>
        <button
          className="primary"
          disabled={busy || run.nextLevel === null}
          onClick={onNext}
        >
          Next
        </button>
      </div>
      {run.level === 10 && run.passed && (
        <p>You have completed the highest level in this game mode.</p>
      )}
      {run.nextLevel && (
        <small>
          Next opens Level {run.nextLevel}. Your theme and source are kept where
          matching questions exist; otherwise All is used.
        </small>
      )}
      <details>
        <summary>Review answers</summary>
        {run.receipts.map((receipt, index) => {
          const round = run.rounds[index];
          return (
            <section key={receipt.id} className="game-answer-review">
              <h3>
                Question {index + 1} ·{" "}
                {receipt.score === null ? "Unscorable" : `${receipt.score}/100`}
              </h3>
              <GameSourceCredit source={round.source} />
              {receipt.feedback.dictation && (
                <DictationResult
                  feedback={receipt.feedback.dictation}
                  language="en"
                />
              )}
              {receipt.feedback.speaking && (
                <SpeakingFeedback result={receipt.feedback.speaking} />
              )}
              {receipt.feedback.gapFill && (
                <GapFillResult
                  feedback={receipt.feedback.gapFill}
                  crossword={run.kind === "mini-crossword"}
                />
              )}
              {receipt.feedback.correctAnswer && (
                <p>Correct answer: {receipt.feedback.correctAnswer}</p>
              )}
              <p>{receipt.feedback.explanation}</p>
              {receipt.feedback.sceneWordNotes && (
                <SceneWordNotebook
                  notes={receipt.feedback.sceneWordNotes}
                  token={token}
                />
              )}
            </section>
          );
        })}
      </details>
    </section>
  );
}
