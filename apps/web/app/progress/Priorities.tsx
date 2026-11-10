"use client";
import Link from "next/link";
import { useState } from "react";
import type { Personalization } from "@contextspeak/contracts";
import { aiLanguages } from "@contextspeak/contracts";
import { goalLabel, priorityHref } from "../../src/learning-plan";
export function Priorities({ value }: { value: Personalization }) {
  const [expanded, setExpanded] = useState(false);
  const items = expanded ? value.priorities : value.priorities.slice(0, 5);
  return (
    <section className="priorities" aria-label="Vocabulary priorities">
      <div className="plan-heading">
        <div>
          <p className="eyebrow">From your saved vocabulary</p>
          <h3>What to practise next</h3>
        </div>
        <span>{value.priorities.length} suggestions</span>
      </div>
      <p className="section-copy">
        An order for practice, not a mastery score. Start with one expression.
      </p>
      {!items.length && (
        <p>
          Save a word or sentence with ContextSpeak to build your practice list.{" "}
          <Link href="/library">Open your library</Link>
        </p>
      )}
      <ol className="priority-list">
        {items.map((item) => (
          <li className="priority-row" key={item.id}>
            <div className="priority-main">
              <div>
                <Link
                  className="priority-word"
                  href={`/library/item/${item.id}?returnTo=%2Fprogress`}
                >
                  {item.word}
                </Link>
                <p className="priority-meta">
                  {aiLanguages.find(
                    (language) => language.code === item.targetLanguage,
                  )?.name ?? item.targetLanguage}
                  {item.due ? " · Review due" : ""}
                  {item.matchedGoals?.[0]
                    ? ` · ${goalLabel(item.matchedGoals[0])}`
                    : ""}
                </p>
              </div>
              <Link
                className="priority-start"
                href={priorityHref(item, value.profile.goals)}
              >
                Practise<span className="sr-only"> {item.word}</span>
              </Link>
            </div>
            <details>
              <summary>Why this word?</summary>
              <p>
                {item.encounters} saved encounters ·{" "}
                {item.goalClues.length
                  ? `goal clues: ${item.goalClues.join(", ")}`
                  : "no confirmed-goal keyword match"}
                .
              </p>
              <p>
                Subtitle frequency:{" "}
                {item.frequency
                  ? `rank ${item.frequency.rank}, ${item.frequency.count} occurrences`
                  : "unknown; no frequency estimate"}
                . Practice gap:{" "}
                {item.signals.gap === null
                  ? "not enough independent evidence"
                  : `${Math.round(item.signals.gap * 100)}% from ${item.practiceEvidence} different tasks`}
                .
              </p>
              <p>
                Priority evidence: {item.score} points. Available evidence adds
                weight; missing evidence adds no boost and is not an ability
                judgment.
              </p>
            </details>
          </li>
        ))}
      </ol>
      {value.priorities.length > 5 && (
        <button
          className="secondary"
          aria-expanded={expanded}
          onClick={() => setExpanded(!expanded)}
        >
          {expanded
            ? "Show fewer priorities"
            : `Show all ${value.priorities.length} priorities`}
        </button>
      )}
      <details className="plan-method">
        <summary>How priorities are chosen</summary>
        <p>
          Goal keyword clues 30%, encounters 25%, subtitle frequency 20%,
          reliable practice gap 15%, due reviews 10%. This fixed evidence scale
          does not reward missing data. We sample up to 500 successful English
          lookups and 500 recent vocabulary-practice attempts.
        </p>
        <p>
          Frequency data:{" "}
          <a href={value.frequencySource.url} target="_blank" rel="noreferrer">
            {value.frequencySource.title}
          </a>
          , Hermit Dave. Adapted top-5,000 snapshot under{" "}
          <a href={value.frequencySource.licenseUrl}>
            {value.frequencySource.license}
          </a>
          . Subtitle-domain counts are not universal usage frequency.
        </p>
      </details>
    </section>
  );
}
