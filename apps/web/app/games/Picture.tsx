import { pictureLibrary, type PictureId } from "@contextspeak/contracts";

const sources = new Map<string, string>();
function source(kind: PictureId) {
  let value = sources.get(kind);
  if (!value) {
    value = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(pictureLibrary[kind].svg)}`;
    sources.set(kind, value);
  }
  return value;
}

/** Lightweight original SVG pictures for browser challenges. */
export function Picture({
  kind,
  size = "normal",
  decorative = true,
}: {
  kind: PictureId;
  size?: "normal" | "large";
  decorative?: boolean;
}) {
  if (!pictureLibrary[kind]) return null;
  return (
    <img
      className={size === "large" ? "game-picture large" : "game-picture"}
      src={source(kind)}
      alt={decorative ? "" : pictureLibrary[kind].label}
      aria-hidden={decorative || undefined}
      draggable={false}
    />
  );
}
