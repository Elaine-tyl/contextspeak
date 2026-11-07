import { readFile } from "node:fs/promises";
try {
  const tokens = JSON.parse(await readFile(".local/demo.json", "utf8"));
  console.log(process.argv[2] === "b" ? tokens.tokenB : tokens.tokenA);
} catch {
  console.error(
    "Start pnpm dev first. Tokens are local developer credentials; do not commit or share them.",
  );
  process.exitCode = 1;
}
