import { createHash } from "node:crypto";
import { z } from "zod";
import snapshot from "./frequency.json";
const rows = z
  .array(
    z.tuple([
      z.string().regex(/^[a-z]+(?:['-][a-z]+)*$/),
      z.number().int().positive(),
      z.number().int().min(1).max(5000),
    ]),
  )
  .parse(snapshot.rows);
if (
  createHash("sha256").update(JSON.stringify(rows)).digest("hex") !==
  snapshot.sha256
)
  throw new Error("Frequency snapshot integrity failed");
const entries = new Map(
  rows.map(([word, count, rank]) => [word, { count, rank }]),
);
export const frequencySource = {
  title: snapshot.source,
  url: snapshot.url,
  license: snapshot.license,
  licenseUrl: snapshot.licenseUrl,
};
export function frequencyFor(word: string) {
  return entries.get(word.normalize("NFKC").trim().toLowerCase()) ?? null;
}
