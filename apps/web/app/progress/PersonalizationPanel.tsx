"use client";
import Link from "next/link";
import { useEffect, useState } from "react";
import type {
  Personalization,
  ScenarioId,
  LearningProfile,
} from "@contextspeak/contracts";
import { api } from "../../src/api";
const scenarios: { id: ScenarioId; label: string }[] = [
  { id: "introductions", label: "Introductions" },
  { id: "making-plans", label: "Making plans" },
  { id: "asking-for-help", label: "Asking for help" },
];
export function PersonalizationPanel({
  value,
  token,
  refresh,
}: {
  value: Personalization;
  token: string;
  refresh: () => void;
}) {
  const [goals, setGoals] = useState(value.profile.goals);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const confirmedGoals = JSON.stringify(value.profile.goals);
  useEffect(
    () => setGoals(JSON.parse(confirmedGoals) as ScenarioId[]),
    [confirmedGoals],
  );
  async function save(path: string, body?: object) {
    setBusy(true);
    setMessage("");
    try {
      const saved = await api<LearningProfile>(
        token,
        "/v1/personalization/" + path,
        path === "goals" ? "PUT" : "POST",
        body,
        { timeoutMs: path === "suggestions" ? 60000 : 6000 },
      );
      if (path === "goals") setGoals(saved.goals);
      setMessage(
        path === "goals"
          ? "Goals saved."
          : "Suggestions checked. Choose which goals to use.",
      );
      refresh();
    } catch {
      setMessage(
        "Could not confirm the update. Refresh before retrying; previously saved progress remains available.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="progress-card" aria-label="Personal learning plan">
      <h2>Your learning plan</h2>
      <p>
        Choose everyday situations to practise. Suggestions are optional and
        never change your goals automatically.
      </p>
      <fieldset disabled={busy}>
        <legend>Confirmed goals</legend>
        {scenarios.map((scenario) => (
          <label className="goal-choice" key={scenario.id}>
            <input
              type="checkbox"
              checked={goals.includes(scenario.id)}
              onChange={(event) =>
                setGoals(
                  event.target.checked
                    ? [...goals, scenario.id]
                    : goals.filter((id) => id !== scenario.id),
                )
              }
            />
            {scenario.label}
          </label>
        ))}
      </fieldset>
      <button disabled={busy} onClick={() => void save("goals", { goals })}>
        Save goals
      </button>{" "}
      <button
        className="secondary"
        disabled={busy}
        onClick={() => void save("goals", { goals: [], dismissed: [] })}
      >
        Reset goals
      </button>
      <p>
        {value.contextCount} distinct English contexts in up to{" "}
        {value.historySampleSize} recent successful lookups.
      </p>
      <p>
        Analyze sends up to 30 saved English excerpts to the configured AI
        provider. It needs ten new distinct contexts. Estimates may be wrong;
        inspect the cited excerpts.
      </p>
      <button
        disabled={busy || !value.suggestionAvailable || value.contextCount < 10}
        onClick={() => void save("suggestions")}
      >
        Analyze saved contexts
      </button>
      {!value.suggestionAvailable && (
        <p>
          AI suggestions are unavailable. You can still choose goals and
          practise.
        </p>
      )}
      {message && <p role="status">{message}</p>}
      {value.profile.suggestions
        .filter(
          (suggestion) =>
            !value.profile.dismissed.includes(suggestion.scenario),
        )
        .map((suggestion) => (
          <div className="scenario-suggestion" key={suggestion.scenario}>
            <h3>
              {
                scenarios.find(
                  (scenario) => scenario.id === suggestion.scenario,
                )?.label
              }
            </h3>
            <p>{suggestion.reason}</p>
            <p>
              AI suggestion · uncalibrated confidence{" "}
              {Math.round(suggestion.confidence * 100)}%
            </p>
            <details>
              <summary>Inspect evidence</summary>
              <ul>
                {value.evidence
                  .filter((row) => suggestion.evidenceIds.includes(row.id))
                  .map((row) => (
                    <li key={row.id}>
                      {row.text}: {row.context}
                    </li>
                  ))}
              </ul>
            </details>
            <button
              disabled={busy || goals.includes(suggestion.scenario)}
              onClick={() =>
                void save("goals", {
                  goals: [...new Set([...goals, suggestion.scenario])],
                })
              }
            >
              Use this goal
            </button>{" "}
            <button
              className="secondary"
              disabled={busy}
              onClick={() =>
                void save("goals", {
                  goals,
                  dismissed: [
                    ...new Set([
                      ...value.profile.dismissed,
                      suggestion.scenario,
                    ]),
                  ],
                })
              }
            >
              Dismiss
            </button>
          </div>
        ))}
      <h3>Suggested vocabulary priorities</h3>
      <p>
        Priority is a practice order, not a mastery score. Based on up to 500
        recent successful English lookups and 500 recent vocabulary-practice
        attempts.
      </p>
      {!value.priorities.length && (
        <p>Save vocabulary to build your practice list.</p>
      )}
      <ol>
        {value.priorities.map((item) => (
          <li key={item.id}>
            <Link href={`/library/item/${item.id}`}>{item.word}</Link> ·
            priority {item.score}/100{" "}
            <Link
              href={`/practice/${item.id}?lookup=${item.lookupId}${value.profile.goals[0] ? `&scenario=${value.profile.goals[0]}` : ""}`}
            >
              Practise
            </Link>
            <details>
              <summary>Why this order?</summary>
              <p>
                {item.encounters} encounters in the sample ·{" "}
                {item.due ? "review due" : "no active review due"} · goal
                keyword clues: {item.goalClues.join(", ") || "none"}.
              </p>
              <p>
                Subtitle frequency:{" "}
                {item.frequency
                  ? `rank ${item.frequency.rank}, ${item.frequency.count} occurrences`
                  : "unknown (no frequency estimate)"}
                .
              </p>
              <p>
                Practice gap:{" "}
                {item.signals.gap === null
                  ? "unknown; needs two different independent scored tasks"
                  : `${Math.round(item.signals.gap * 100)}% from ${item.practiceEvidence} independent tasks`}
                .
              </p>
              <p>
                Weights: goal 30%, encounters 25%, subtitle frequency 20%,
                practice gap 15%, due review 10%. Missing signals are excluded
                and remaining weights normalized. Goal matching uses a small
                English keyword list.
              </p>
            </details>
          </li>
        ))}
      </ol>
      <p>
        Frequency evidence:{" "}
        <a href={value.frequencySource.url} target="_blank" rel="noreferrer">
          {value.frequencySource.title}
        </a>
        , Hermit Dave, filtered top-5,000 snapshot. Subtitle-domain counts are
        not universal usage frequency.{" "}
        <a href={value.frequencySource.licenseUrl}>
          {value.frequencySource.license}
        </a>
        .
      </p>
    </section>
  );
}
