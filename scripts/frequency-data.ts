import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import snapshot from "../apps/api/src/personalization/frequency.json";
// Optional upstream file verifies the exact pinned extraction without a network call.
const source = process.argv[2];
const rows = source
  ? (await readFile(source, "utf8"))
      .trim()
      .split(/\r?\n/)
      .slice(0, 5000)
      .flatMap((line, index) => {
        const [word, count] = line.trim().split(/\s+/);
        return /^[a-z]+(?:['-][a-z]+)*$/.test(word)
          ? [[word, Number(count), index + 1]]
          : [];
      })
  : snapshot.rows;
const hash = createHash("sha256").update(JSON.stringify(rows)).digest("hex");
if (hash !== snapshot.sha256)
  throw new Error("Frequency snapshot differs from the pinned extraction");
console.log(
  `Verified ${rows.length} subtitle-frequency rows (${snapshot.license}).`,
);
