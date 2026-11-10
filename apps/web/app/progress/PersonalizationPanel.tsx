"use client";
import { useEffect, useState } from "react";
import {
  conversationScenarios,
  type Personalization,
  type ScenarioId,
  type LearningProfile,
} from "@contextspeak/contracts";
import { api } from "../../src/api";
import { goalLabel } from "../../src/learning-plan";
import { Priorities } from "./Priorities";
const sameGoals = (a: ScenarioId[], b: ScenarioId[]) =>
  a.length === b.length && a.every((goal) => b.includes(goal));
export function PersonalizationPanel({
  value,
  token,
  refresh,
}: {
  value: Personalization;
  token: string;
  refresh: () => void;
}) {
  const [draft, setDraft] = useState({
    goals: value.profile.goals,
    base: value.profile.goals,
  });
  const [busy, setBusy] = useState("");
  const [message, setMessage] = useState("");
  const [failed, setFailed] = useState(false);
  const confirmed = JSON.stringify(value.profile.goals);
  useEffect(() => {
    const goals = JSON.parse(confirmed) as ScenarioId[];
    // A background refresh may update saved goals, but must not erase a draft.
    setDraft((current) =>
      sameGoals(current.goals, current.base) || sameGoals(current.goals, goals)
        ? { goals, base: goals }
        : current,
    );
  }, [confirmed]);
  const dirty = !sameGoals(draft.goals, draft.base);
  const conflict = dirty && !sameGoals(draft.base, value.profile.goals);
  async function save(path: string, body?: object) {
    setBusy(path);
    setMessage("");
    setFailed(false);
    try {
      const saved = await api<LearningProfile>(
        token,
        "/v1/personalization/" + path,
        path === "goals" ? "PUT" : "POST",
        body,
        { timeoutMs: path === "suggestions" ? 60000 : 6000 },
      );
      if (path === "goals") setDraft({ goals: saved.goals, base: saved.goals });
      setMessage(
        path === "goals"
          ? "Goals saved."
          : "Suggestions checked. Choose which goals to use.",
      );
      refresh();
    } catch {
      setFailed(true);
      setMessage(
        "Could not confirm the update. Refresh to check saved changes before retrying.",
      );
      refresh();
    } finally {
      setBusy("");
    }
  }
  const eligibility = value.analysis ?? {
    status: "unavailable",
    newContexts: 0,
    minimum: 10,
  };
  return (
    <section className="progress-card" aria-label="Personal learning plan">
      <div className="plan-columns">
        <div className="goal-panel">
          <p className="eyebrow">Your everyday conversations</p>
          <h2>Your learning plan</h2>
          <p>Choose situations you want to feel more comfortable in.</p>
          <fieldset disabled={Boolean(busy)}>
            <legend>Practice goals</legend>
            {conversationScenarios.map((goal) => (
              <label className="goal-choice" key={goal.id}>
                <input
                  type="checkbox"
                  checked={draft.goals.includes(goal.id)}
                  onChange={(event) => {
                    setMessage("");
                    setDraft({
                      ...draft,
                      goals: event.target.checked
                        ? [...draft.goals, goal.id]
                        : draft.goals.filter((id) => id !== goal.id),
                    });
                  }}
                />
                {goal.title}
              </label>
            ))}
          </fieldset>
          {conflict ? (
            <p role="alert">
              Goals changed in another tab.{" "}
              <button
                className="text-button"
                onClick={() =>
                  setDraft({
                    goals: value.profile.goals,
                    base: value.profile.goals,
                  })
                }
              >
                Reload saved goals
              </button>
            </p>
          ) : dirty ? (
            <p className="draft-message" role="status">
              Unsaved goal changes
            </p>
          ) : (
            <p className="section-copy">
              {draft.base.length
                ? "Your saved goals shape these practice suggestions."
                : "No goals saved yet. You can still practise."}
            </p>
          )}
          <div className="goal-actions">
            <button
              disabled={Boolean(busy) || !dirty || conflict}
              onClick={() =>
                void save("goals", {
                  goals: draft.goals,
                  expectedGoals: draft.base,
                })
              }
            >
              {busy === "goals" ? "Saving…" : "Save goals"}
            </button>
            <button
              className="secondary"
              disabled={Boolean(busy) || conflict}
              onClick={() =>
                void save("goals", {
                  goals: [],
                  dismissed: [],
                  expectedGoals: draft.base,
                })
              }
            >
              Reset goals
            </button>
          </div>
          <div className="scenario-analysis">
            <h3>Find goals from your reading</h3>
            <p className="section-copy">
              AI can suggest goals using up to 30 saved English excerpts. You
              decide which to use.
            </p>
            <p>
              {eligibility.status === "up-to-date"
                ? "Suggestions are up to date."
                : eligibility.status === "unavailable"
                  ? "AI suggestions are unavailable. Manual goals still work."
                  : `${Math.min(eligibility.newContexts, eligibility.minimum)} of ${eligibility.minimum} new contexts ready`}
            </p>
            <button
              className="secondary"
              disabled={Boolean(busy) || eligibility.status !== "ready"}
              onClick={() => void save("suggestions")}
            >
              {busy === "suggestions"
                ? "Checking your contexts…"
                : "Analyze saved contexts"}
            </button>
            <details>
              <summary>What is shared with AI?</summary>
              <p>
                Only saved selection text and bounded English excerpts are sent
                to the configured provider. No full pages or page addresses.{" "}
                {value.contextCount} distinct contexts in{" "}
                {value.historySampleSize} recent successful lookups. Ten
                different new contexts are needed between analyses; repeats do
                not count.
              </p>
            </details>
          </div>
          {message && <p role={failed ? "alert" : "status"}>{message}</p>}
          {value.profile.suggestions
            .filter(
              (suggestion) =>
                !value.profile.dismissed.includes(suggestion.scenario),
            )
            .map((suggestion) => (
              <div className="scenario-suggestion" key={suggestion.scenario}>
                <h3>{goalLabel(suggestion.scenario)}</h3>
                <p>{suggestion.reason}</p>
                <details>
                  <summary>
                    Based on {suggestion.evidenceIds.length} saved excerpts
                  </summary>
                  <p>
                    AI suggestion; this is not a confirmed fact about your
                    needs.
                  </p>
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
                <div className="goal-actions">
                  <button
                    disabled={
                      Boolean(busy) ||
                      dirty ||
                      draft.base.includes(suggestion.scenario)
                    }
                    onClick={() =>
                      void save("goals", {
                        goals: [
                          ...new Set([...draft.base, suggestion.scenario]),
                        ],
                        expectedGoals: draft.base,
                      })
                    }
                  >
                    {draft.base.includes(suggestion.scenario)
                      ? "Goal selected"
                      : "Use this goal"}
                  </button>
                  <button
                    className="secondary"
                    disabled={Boolean(busy) || dirty}
                    onClick={() =>
                      void save("goals", {
                        goals: draft.base,
                        dismissed: [
                          ...new Set([
                            ...value.profile.dismissed,
                            suggestion.scenario,
                          ]),
                        ],
                        expectedGoals: draft.base,
                      })
                    }
                  >
                    Dismiss
                  </button>
                </div>
              </div>
            ))}
        </div>
        <Priorities value={value} />
      </div>
    </section>
  );
}
