import type {
  GameRound,
  PracticeRecommendation,
} from "@contextspeak/contracts";
export type GameSettings = { level: number; topic: string; source: string };
export function GameSetup({
  rounds,
  unlocked,
  recommendation,
  busy,
  settings,
  onChange,
  onStart,
  onRefresh,
}: {
  rounds: GameRound[];
  unlocked: number;
  recommendation?: PracticeRecommendation;
  busy: boolean;
  settings: GameSettings;
  onChange(value: GameSettings): void;
  onStart(): void;
  onRefresh(): void;
}) {
  const { level, topic, source } = settings;
  const available = rounds.filter((round) => round.level === level);
  const themes = [
    ...new Set(
      available
        .map((round) => round.topic)
        .filter((value): value is string => !!value),
    ),
  ];
  // Keep a chosen source visible even at a level with no matching excerpts.
  const sources = [
    ...new Set(
      rounds.flatMap((round) => (round.source ? [round.source.provider] : [])),
    ),
  ];
  const visible = available.filter(
    (round) =>
      (topic === "all" || round.topic === topic) &&
      (source === "all" ||
        (round.source?.provider ?? "ContextSpeak") === source),
  );
  const recommended = Math.min(recommendation?.level ?? 1, unlocked);
  return (
    <>
      <div className="practice-tabs" aria-label="Game difficulty">
        {Array.from({ length: 10 }, (_, index) => index + 1).map((value) => (
          <button
            key={value}
            className={level === value ? "primary" : "secondary"}
            disabled={busy || value > unlocked}
            aria-pressed={level === value}
            onClick={() =>
              onChange({ ...settings, level: value, topic: "all" })
            }
          >{`Level ${value}${value > unlocked ? " · Locked" : ""}`}</button>
        ))}
      </div>
      {recommendation && (
        <section className="game-progress" aria-label="Practice recommendation">
          <strong>Recommended: Level {recommended}</strong>
          <p>
            {recommendation.level > unlocked
              ? "Build evidence in this game mode before trying a higher level."
              : recommendation.reason}
          </p>
          {recommendation.focus && (
            <p>
              Focus:{" "}
              {recommendation.focus.replace("taskResponse", "task response")}
            </p>
          )}
          <button
            className="secondary"
            disabled={busy}
            onClick={() =>
              onChange({ ...settings, level: recommended, topic: "all" })
            }
          >
            Use recommended level
          </button>
        </section>
      )}
      <div className="game-source-filter">
        <label htmlFor="game-theme">Theme</label>
        <select
          id="game-theme"
          disabled={busy}
          value={topic}
          onChange={(event) =>
            onChange({ ...settings, topic: event.target.value })
          }
        >
          <option value="all">All themes</option>
          {themes.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
        <label htmlFor="game-content-source">Content source</label>
        <select
          id="game-content-source"
          disabled={busy}
          value={source}
          onChange={(event) =>
            onChange({ ...settings, source: event.target.value })
          }
        >
          <option value="all">All sources</option>
          <option value="ContextSpeak">ContextSpeak originals</option>
          {sources.map((value) => (
            <option key={value} value={value}>
              {value}
            </option>
          ))}
        </select>
      </div>
      <p>
        {visible.length
          ? `${Math.min(3, visible.length)} different questions in this level run. Available themes depend on reviewed content.`
          : "No matching questions. Choose another theme or All sources."}
      </p>
      <button
        className="primary"
        disabled={busy || !visible.length}
        onClick={onStart}
      >
        {busy ? "Starting level…" : "Start level"}
      </button>
      <button className="secondary" disabled={busy} onClick={onRefresh}>
        Refresh progress
      </button>
      <p className="section-copy">
        AI-generated audio · credited open texts · ten levels per mode. AI PK
        uses a simulated opponent, not a real player.
      </p>
    </>
  );
}
