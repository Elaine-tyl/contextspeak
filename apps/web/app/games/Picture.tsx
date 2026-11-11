import type { PictureId } from "@contextspeak/contracts";
export function Picture({ kind }: { kind: PictureId }) {
  return (
    <svg
      viewBox="0 0 100 100"
      width="96"
      height="96"
      aria-hidden="true"
      style={{ fill: "var(--brand-soft)", stroke: "var(--brand)" }}
      strokeWidth="4"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {kind === "umbrella" && (
        <>
          <path d="M12 48a38 36 0 0 1 76 0Q76 37 64 48Q50 37 37 48Q25 37 12 48Z" />
          <path d="M50 14v64q0 16 14 9q5-3 5-10" fill="none" />
        </>
      )}
      {kind === "book" && (
        <>
          <path d="M10 20q20-8 40 4q20-12 40-4v62q-20-7-40 3q-20-10-40-3Z" />
          <path
            d="M50 24v61M20 34l18 2M20 46l18 2M62 36l18-2M62 48l18-2"
            fill="none"
          />
        </>
      )}
      {kind === "bridge" && (
        <>
          <path d="M8 76V35h84v41H73a23 26 0 0 0-46 0Z" />
          <path
            d="M8 24v18M26 24v12M44 24v12M62 24v12M80 24v12M92 24v18M8 27h84M8 88q10-7 20 0t20 0t20 0t24 0"
            fill="none"
          />
        </>
      )}
      {kind === "bicycle" && (
        <>
          <circle cx="23" cy="68" r="18" />
          <circle cx="77" cy="68" r="18" />
          <path
            d="m23 68 19-32 18 32H23l35-24 19 24-15-44h13M34 35h17"
            fill="none"
          />
        </>
      )}
    </svg>
  );
}
