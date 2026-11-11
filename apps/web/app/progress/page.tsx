"use client";
import Link from "next/link";
import type { SkillProgress } from "@contextspeak/contracts";
import { API } from "../../src/api";
import { practiceEntry } from "../../src/learning-plan";
import { DailyActions } from "./DailyActions";
import { PersonalizationPanel } from "./PersonalizationPanel";
import { useProgress } from "./useProgress";
import "../library/library.css";
import "../practice/practice.css";
import "./progress.css";
function SkillCard({
  title,
  value,
  href,
}: {
  title: string;
  value: SkillProgress;
  href: string;
}) {
  return (
    <section
      className="progress-card tone-speak"
      aria-label={`${title} progress`}
    >
      <h2>{title}</h2>
      <strong className="progress-number">
        {value.average === null ? (
          <span className="progress-none">No score yet</span>
        ) : (
          <>
            {value.average}
            <small>/100</small>
          </>
        )}
      </strong>
      {value.average !== null && (
        <div className="meter" aria-hidden="true">
          <span style={{ width: `${value.average}%` }} />
        </div>
      )}
      <p className="muted">
        Based on {value.scored} scored{" "}
        {value.scored === 1 ? "answer" : "answers"} in the last 30 days
      </p>
      {value.scored === 0 && (
        <p>
          Only current, reliable assessments appear here. Older or unscorable
          feedback may still be visible in your practice history.
        </p>
      )}
      {value.unscorable > 0 && (
        <p className="muted">
          {value.unscorable} unclear{" "}
          {value.unscorable === 1 ? "recording was" : "recordings were"} left
          out.
        </p>
      )}
      <p>
        {value.focus
          ? `Work on next: ${value.focus.dimension.replace("taskResponse", "task response")} (seen in ${value.focus.evidenceCount} different tasks).`
          : "Keep practising — we'll suggest what to work on once there's a clear pattern."}
      </p>
      <Link className="secondary" href={href}>
        Practise {title.toLowerCase()}
      </Link>
    </section>
  );
}
export default function ProgressPage() {
  const { data, error, loading, signedIn, token, refresh } = useProgress();
  return (
    <main className="page progress-page">
      <div className="page-head">
        <div>
          <h1>Learning progress</h1>
          <p className="page-lead">
            Start with one of today’s steps, then see how you’re doing. Scores
            are rough estimates, not official levels.
          </p>
        </div>
        {signedIn && (
          <button className="secondary" onClick={refresh}>
            Refresh
          </button>
        )}
      </div>
      {loading && <p role="status">Loading progress…</p>}
      {error && <p role="alert">{error}</p>}
      {!signedIn && !loading && (
        <section className="connect-panel sign-in-panel">
          <div>
            <h2>Sign in to see your progress</h2>
            <p>
              Your reviews, practice and game levels are saved to your account.
            </p>
          </div>
          <a className="primary" href={API + "/auth/login?returnTo=/progress"}>
            Sign in with Google
          </a>
        </section>
      )}
      {data && (
        <>
          <h2 className="progress-section-title">Today</h2>
          <DailyActions data={data} />
          <section
            className="progress-card tone-review"
            aria-label="Vocabulary review progress"
          >
            <div className="section-head">
              <h2>Vocabulary review</h2>
              <Link className="primary" href="/library?view=reviews">
                Review due vocabulary
              </Link>
            </div>
            <dl className="progress-totals">
              {[
                ["Due now", data.vocabulary.due],
                ["Words in review", data.vocabulary.active],
                ["Saved words", data.vocabulary.saved],
                ["Reviews done", data.vocabulary.reviews],
                ["Paused", data.vocabulary.paused],
              ].map(([label, value]) => (
                <div key={label}>
                  <dt>{label}</dt>
                  <dd>{value}</dd>
                </div>
              ))}
            </dl>
            <h3>How your last 30 days of reviews went</h3>
            <ul className="rating-summary">
              {Object.entries(data.vocabulary.recentRatings).map(
                ([rating, count]) => (
                  <li key={rating} className={`rating-${rating}`}>
                    <strong>{count}</strong>
                    {rating[0].toUpperCase() + rating.slice(1)}
                  </li>
                ),
              )}
            </ul>
          </section>
          <div className="progress-grid">
            <SkillCard
              title="Listening"
              value={data.listening}
              href={practiceEntry(data.personalization, "listening")}
            />
            <SkillCard
              title="Speaking"
              value={data.speaking}
              href={practiceEntry(data.personalization, "speaking")}
            />
          </div>
          <section className="progress-card" aria-label="Focus vocabulary">
            <h2>Words to revisit</h2>
            {data.vocabulary.difficult.length ? (
              <ul>
                {data.vocabulary.difficult.map((word) => (
                  <li key={word.id}>
                    <Link href={`/library/item/${word.id}`}>{word.word}</Link>{" "}
                    <span className="muted">
                      — marked Again or Hard {word.count}{" "}
                      {word.count === 1 ? "time" : "times"} this month
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p>
                Nothing to worry about yet. Words you mark “Again” or “Hard” in
                review will show up here.
              </p>
            )}
          </section>
          {data.personalization && (
            <PersonalizationPanel
              value={data.personalization}
              token={token}
              refresh={refresh}
            />
          )}
          <section className="tone-speak" aria-label="Unlocked game levels">
            <h2 className="progress-section-title">Game levels</h2>
            <div className="progress-grid">
              {data.games.map((mode) => (
                <section
                  className="progress-card"
                  key={mode.kind}
                  aria-label={mode.label}
                >
                  <h3>{mode.label}</h3>
                  <p>
                    Level <strong>{mode.level}</strong> of 10 unlocked
                  </p>
                  <div className="meter" aria-hidden="true">
                    <span style={{ width: `${mode.level * 10}%` }} />
                  </div>
                  <Link href={`/games?mode=${mode.kind}`}>
                    Practise {mode.label.toLowerCase()}
                  </Link>
                </section>
              ))}
            </div>
          </section>
          <details className="fine-print how-calculated">
            <summary>How these numbers are calculated</summary>
            Review totals and game access use saved records. Skill averages use
            up to {data.sampleLimit} recent scored attempts per source
            (vocabulary practice and games), within 30 days. Self-check-only and
            unreliable speech are excluded; focus requires different independent
            tasks. Activity is not a mastery claim.
          </details>
          <p className="fine-print">
            Updated {new Date(data.updatedAt).toLocaleString()}
          </p>
        </>
      )}
    </main>
  );
}
