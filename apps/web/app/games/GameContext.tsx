import { gameDifficultyBands, type GameRound } from "@contextspeak/contracts";

export function DifficultyGuide() {
  return (
    <p className="section-copy">
      Levels{" "}
      {gameDifficultyBands
        .map((band) => `${band.first}–${band.last}: ${band.label}`)
        .join(" · ")}
    </p>
  );
}

export function EditionNotice({ archived }: { archived?: boolean }) {
  if (!archived) return null;
  return (
    <p className="section-copy">
      Earlier edition · this saved level keeps its original questions. Retry
      starts the current edition. Unavailable themes or sources switch to All.
    </p>
  );
}

export function GameSourceCredit({ source }: { source: GameRound["source"] }) {
  if (!source) return null;
  return (
    <p className="game-source-credit">
      <a href={source.url} target="_blank" rel="noreferrer">
        Source: {source.provider} · {source.title}
      </a>
      {" · "}
      {source.author}
      {source.publishedAt && (
        <> · Published {source.publishedAt} · Archive feature</>
      )}
      {" · "}
      <a href={source.licenseUrl} target="_blank" rel="noreferrer">
        {source.license}
      </a>
      <small>
        Excerpt with whitespace normalised; exercise questions by ContextSpeak.
        Audio is AI-generated.
      </small>
    </p>
  );
}
