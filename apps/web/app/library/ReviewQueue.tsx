"use client";
import { useEffect, useRef, useState } from "react";
import type {
  ReviewPrompt,
  ReviewQueue as Queue,
  ReviewReceipt,
  ReviewSubmission,
} from "@contextspeak/contracts";
import { reviewQueueSchema } from "@contextspeak/contracts";
import { api } from "../../src/api";
import { languageName } from "../../src/languages";
function dateLabel(value: string) {
  return new Date(value).toLocaleString("en-GB", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}
interface Props {
  token: string;
  onChanged(): void;
}
export function ReviewQueue({ token, onChanged }: Props) {
  const [queue, setQueue] = useState<Queue | null>(null);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const request = useRef(0);
  async function refresh() {
    const current = ++request.current;
    setLoading(true);
    setError("");
    try {
      // Keep the last valid queue if a malformed response arrives during refresh.
      const result = reviewQueueSchema.parse(
        await api<unknown>(token, "/v1/reviews"),
      );
      if (current === request.current) setQueue(result);
    } catch {
      if (current === request.current)
        setError("Could not load reviews. Check your connection and refresh.");
    } finally {
      if (current === request.current) setLoading(false);
    }
  }
  useEffect(() => {
    setQueue(null);
    void refresh();
    return () => {
      request.current++;
    };
  }, [token]);
  useEffect(() => {
    // Never interrupt recall or discard an unconfirmed rating's retry event.
    if (!queue || queue.items.length || loading || error) return;
    let timer: ReturnType<typeof setTimeout>;
    let checking = false;
    function check() {
      if (document.hidden || checking) return;
      checking = true;
      void refresh();
    }
    const dueAt = Date.parse(queue.nextDueAt ?? "");
    function schedule() {
      // Bound long waits to tolerate clock changes and avoid timer overflow.
      timer = setTimeout(
        () => {
          if (document.hidden) return;
          if (Date.now() < dueAt) schedule();
          else check();
        },
        Math.min(60000, Math.max(1000, dueAt - Date.now())),
      );
    }
    if (Number.isFinite(dueAt)) schedule();
    document.addEventListener("visibilitychange", check);
    return () => {
      clearTimeout(timer);
      document.removeEventListener("visibilitychange", check);
    };
  }, [queue, loading, error, token]);
  async function completed(receipt?: ReviewReceipt) {
    setMessage(receipt ? "Review saved." : "Reviews paused for this item.");
    await refresh();
    onChanged();
  }
  const card = queue?.items[0];
  return (
    <section
      className="library-section review-queue"
      role="region"
      aria-label="Vocabulary review"
    >
      <div className="section-head">
        {queue ? (
          <p className="review-count">
            <span className="badge due">{queue.dueCount} due now</span>
            <span className="badge">{queue.totalCount} words in review</span>
          </p>
        ) : (
          <span />
        )}
        <button
          className="secondary"
          disabled={loading}
          onClick={() => void refresh()}
        >
          Refresh queue
        </button>
      </div>
      {message && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
      {card ? (
        <RecallCard
          key={`${card.id}:${card.version}`}
          card={card}
          token={token}
          onCompleted={completed}
        />
      ) : queue ? (
        <div className="empty">
          <h3>{queue.totalCount ? "You’re up to date." : "No reviews yet."}</h3>
          <p>
            {queue.nextDueAt
              ? `Next review: ${dateLabel(queue.nextDueAt)}. This queue updates automatically.`
              : "Open your Word list and choose “Start reviewing” on any word."}
          </p>
        </div>
      ) : (
        <p aria-live="polite">
          {loading ? "Loading reviews…" : "Refresh to try again."}
        </p>
      )}
    </section>
  );
}
const hints = {
  again: "Forgot",
  hard: "Struggled",
  good: "Remembered",
  easy: "Instantly",
} as const;
interface CardProps {
  card: ReviewPrompt;
  token: string;
  onCompleted(receipt?: ReviewReceipt): Promise<void>;
}
function RecallCard({ card, token, onCompleted }: CardProps) {
  const [revealed, setRevealed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const attempt = useRef<ReviewSubmission | null>(null);
  const active = useRef(true);
  useEffect(() => {
    active.current = true;
    return () => {
      active.current = false;
    };
  }, []);
  async function rate(rating: ReviewSubmission["rating"]) {
    if (
      !revealed ||
      busy ||
      (attempt.current && attempt.current.rating !== rating)
    )
      return;
    if (!attempt.current)
      attempt.current = {
        eventId: crypto.randomUUID(),
        version: card.version,
        rating,
      };
    setBusy(true);
    setError("");
    try {
      // Retain the same event after a timeout: the server may have saved it already.
      const result = await api<ReviewReceipt>(
        token,
        `/v1/reviews/${card.id}/rate`,
        "POST",
        attempt.current,
      );
      if (active.current) await onCompleted(result);
    } catch {
      if (active.current)
        setError(
          "Rating not confirmed. Retry the same rating, or refresh the queue if this card changed.",
        );
    } finally {
      if (active.current) setBusy(false);
    }
  }
  async function pause() {
    setBusy(true);
    try {
      await api(token, `/v1/reviews/${card.id}/pause`, "POST");
      if (active.current) await onCompleted();
    } catch {
      if (active.current)
        setError("Could not pause reviews. Check your connection and retry.");
    } finally {
      if (active.current) setBusy(false);
    }
  }
  return (
    <article className="lookup-card recall-card">
      <small>What does this mean in {languageName(card.targetLanguage)}?</small>
      <h3>{card.prompt}</h3>
      {!revealed ? (
        <button className="primary" onClick={() => setRevealed(true)}>
          Show answer
        </button>
      ) : (
        <>
          <p className="review-answer">{card.answer}</p>
          {card.context && <blockquote>{card.context}</blockquote>}
          <div className="review-actions" aria-label="Rate your recall">
            {(["again", "hard", "good", "easy"] as const).map((rating) => (
              <button
                key={rating}
                className="secondary"
                disabled={
                  busy ||
                  (!!attempt.current && attempt.current.rating !== rating)
                }
                onClick={() => void rate(rating)}
              >
                {rating[0].toUpperCase() + rating.slice(1)}
                <span className="rating-hint" aria-hidden="true">
                  {hints[rating]}
                </span>
              </button>
            ))}
          </div>
        </>
      )}
      {error && <p role="alert">{error}</p>}
      <button
        className="link-button pause-review"
        disabled={busy || !!attempt.current}
        onClick={() => void pause()}
      >
        Pause reviews
      </button>
    </article>
  );
}
