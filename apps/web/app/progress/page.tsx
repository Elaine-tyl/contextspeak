"use client";
import Link from "next/link";
import type { SkillProgress } from "@contextspeak/contracts";
import { API } from "../../src/api";
import { PersonalizationPanel } from "./PersonalizationPanel";
import { useProgress } from "./useProgress";
import "../library/library.css";
import "../practice/practice.css";
import "./progress.css";
function SkillCard({ title, value }: { title: string; value: SkillProgress }) {
  return (
    <section className="progress-card" aria-label={`${title} progress`}>
      <h2>{title}</h2>
      <strong className="progress-number">
        {value.average === null ? "No score yet" : `${value.average}/100`}
      </strong>
      <p>{value.scored} assessed samples · last 30 days</p>
      {value.unscorable > 0 && (
        <p>{value.unscorable} unscorable recordings excluded from scores.</p>
      )}
      <p>
        {value.focus
          ? `Focus: ${value.focus.dimension.replace("taskResponse", "task response")} · ${value.focus.evidenceCount} different independent tasks.`
          : "No consistent focus has been established from recent independent tasks."}
      </p>
      <Link href="/library?view=practice">Practise {title.toLowerCase()}</Link>
    </section>
  );
}
export default function ProgressPage() {
  const { data, error, loading, signedIn, token, refresh } = useProgress();
  return (
    <main className="progress-page">
      <Link href="/library">Back to library</Link>
      <p className="eyebrow">ContextSpeak · your learning</p>
      <h1>Learning progress</h1>
      <p>
        Review activity, assessed practice and unlocked games. Practice scores
        are estimates, not proof of mastery or CEFR levels.
      </p>
      {loading && <p role="status">Loading progress…</p>}
      {error && <p role="alert">{error}</p>}
      {!signedIn && !loading && (
        <a className="primary" href={API + "/auth/login?returnTo=/progress"}>
          Sign in with Google
        </a>
      )}
      {signedIn && (
        <button className="secondary" onClick={refresh}>
          Refresh
        </button>
      )}
      {data && (
        <>
          <section
            className="progress-card"
            aria-label="Vocabulary review progress"
          >
            <h2>Vocabulary review</h2>
            <dl className="progress-totals">
              {[
                ["Saved vocabulary", data.vocabulary.saved],
                ["Active review cards", data.vocabulary.active],
                ["Due now", data.vocabulary.due],
                ["Paused cards", data.vocabulary.paused],
                ["Completed reviews", data.vocabulary.reviews],
              ].map(([label, value]) => (
                <div key={label}>
                  <dt>{label}</dt>
                  <dd>{value}</dd>
                </div>
              ))}
            </dl>
            <Link className="primary" href="/library?view=reviews">
              Review due vocabulary
            </Link>
            <h3>Last 30 days</h3>
            <p>
              {Object.entries(data.vocabulary.recentRatings)
                .map(([rating, count]) => `${rating}: ${count}`)
                .join(" · ")}
            </p>
          </section>
          <div className="progress-grid">
            <SkillCard title="Listening" value={data.listening} />
            <SkillCard title="Speaking" value={data.speaking} />
          </div>
          <section className="progress-card" aria-label="Focus vocabulary">
            <h2>Words to revisit</h2>
            {data.vocabulary.difficult.length ? (
              <ul>
                {data.vocabulary.difficult.map((word) => (
                  <li key={word.id}>
                    <Link href={`/library/item/${word.id}`}>{word.word}</Link> ·{" "}
                    {word.count} Again/Hard reviews in the last 30 days
                  </li>
                ))}
              </ul>
            ) : (
              <p>
                No recent Again/Hard review evidence yet. A saved word alone
                does not indicate difficulty.
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
          <section aria-label="Unlocked game levels">
            <h2>Game levels</h2>
            <div className="progress-grid">
              {data.games.map((mode) => (
                <section
                  className="progress-card"
                  key={mode.kind}
                  aria-label={mode.label}
                >
                  <h3>{mode.label}</h3>
                  <p>
                    Unlocked Level <strong>{mode.level}</strong> of 10
                  </p>
                  <Link href={`/games?mode=${mode.kind}`}>
                    Practise {mode.label.toLowerCase()}
                  </Link>
                </section>
              ))}
            </div>
          </section>
          <p className="section-copy">
            Review totals and game access use saved records. Skill averages use
            up to {data.sampleLimit} recent scored attempts per source
            (vocabulary practice and games), within 30 days. Self-check-only and
            unreliable speech are excluded; focus requires different independent
            tasks. Activity is not a mastery claim.
          </p>
          <p className="section-copy">
            Updated {new Date(data.updatedAt).toLocaleString()}
          </p>
        </>
      )}
    </main>
  );
}
