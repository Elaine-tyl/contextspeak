// Writes games/arcade/pictures.json from the shared picture library so the Godot
// arcade draws exactly the same SVGs as the website. Run: pnpm arcade:pictures
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { pictureLibrary } from "../../packages/contracts/src/pictures";

export function pictureJson() {
  return JSON.stringify(pictureLibrary, null, 2) + "\n";
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  writeFileSync(
    new URL("../../games/arcade/pictures.json", import.meta.url),
    pictureJson(),
  );
  console.log("Wrote games/arcade/pictures.json");
}
