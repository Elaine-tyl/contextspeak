"use client";
import { useState } from "react";
import type { PracticeTask, PracticeReceipt } from "@contextspeak/contracts";
import { PracticeSession } from "./PracticeSession";
import { SpeakingFeedback } from "./SpeakingFeedback";
export function DialogueSession({
  task,
  token,
  onSaved,
  onLocked,
  onExpired,
}: {
  task: PracticeTask;
  token: string;
  onSaved(receipt: PracticeReceipt): void;
  onLocked(value: boolean): void;
  onExpired(): void;
}) {
  const [current, setCurrent] = useState(task);
  const [turns, setTurns] = useState<
    { partner: string; receipt: PracticeReceipt }[]
  >([]);
  const [waiting, setWaiting] = useState(false);
  const [round, setRound] = useState(0);
  const last = turns.at(-1)?.receipt;
  const complete = turns.length === 4;
  const total = turns.reduce(
    (sum, value) => sum + (value.receipt.speakingAssessment?.overall ?? 0),
    0,
  );
  return (
    <section aria-label="Guided conversation">
      <h2>
        {complete
          ? "Conversation complete"
          : `Conversation · turn ${turns.length + (waiting ? 0 : 1)} of 4`}
      </h2>
      <p>
        Listen to your partner, record your reply, replay it and submit for AI
        feedback. Each follow-up uses what you said. Four scored turns complete
        the conversation; unclear audio can be recorded again.
      </p>
      {turns.map(({ partner, receipt }, index) => (
        <section key={receipt.id} aria-label={`Completed turn ${index + 1}`}>
          <h3>Turn {index + 1}</h3>
          <p>
            <strong>Partner:</strong> {partner}
          </p>
          <details open={waiting && index === turns.length - 1}>
            <summary>
              Feedback · {receipt.speakingAssessment!.overall}/100
            </summary>
            <SpeakingFeedback result={receipt.speakingAssessment!} />
          </details>
        </section>
      ))}
      {complete ? (
        <div className="practice-answer" aria-label="Conversation summary">
          <h3>Conversation estimate: {Math.round(total / 4)}/100</h3>
          <p>
            Average of four turn estimates. Review the highlighted suggestions
            above, then practise the conversation again. These estimates do not
            change your vocabulary review schedule.
          </p>
        </div>
      ) : waiting ? (
        <button
          className="primary"
          onClick={() => {
            setCurrent({
              ...task,
              sourceText: last!.speakingAssessment!.nextTurn!,
              translation: "AI-generated follow-up",
            });
            setRound((value) => value + 1);
            setWaiting(false);
          }}
        >
          Continue conversation
        </button>
      ) : (
        <PracticeSession
          key={round}
          task={current}
          token={token}
          mode="speaking"
          conversation={last ? { previousTurnId: last.id } : {}}
          onSaved={(receipt) => {
            onSaved(receipt);
            if (receipt.speakingAssessment?.overall == null) return;
            setTurns((previous) => [
              ...previous,
              { partner: current.sourceText, receipt },
            ]);
            setWaiting(true);
          }}
          onLocked={onLocked}
          onExpired={onExpired}
          onDiscard={() => {
            onLocked(false);
            setRound((value) => value + 1);
          }}
        />
      )}
    </section>
  );
}
