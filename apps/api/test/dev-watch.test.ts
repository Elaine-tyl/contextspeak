import { spawn } from "node:child_process";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { expect, it } from "vitest";

it("reloads imported question data with the development command", async () => {
  const directory = await mkdtemp(join(tmpdir(), "cs-watch-test-"));
  const entry = join(directory, "server.ts");
  const content = join(directory, "questions.json");
  await writeFile(join(directory, "package.json"), '{"type":"module"}');
  await writeFile(content, '{"revision":1}');
  await writeFile(
    entry,
    `import data from './questions.json';\nconsole.log('CATALOG:' + data.revision);\nsetInterval(() => {}, 1000);\n`,
  );
  const manifest = JSON.parse(await readFile("package.json", "utf8"));
  // Exercise the actual launcher flags, replacing only its server entry point.
  const args = manifest.scripts.dev.split(" ").slice(1, -1);
  const child = spawn(
    process.execPath,
    [resolve("node_modules/tsx/dist/cli.mjs"), ...args, entry],
    { cwd: directory, stdio: ["ignore", "pipe", "pipe"] },
  );
  const exited = new Promise<void>((done) => child.once("exit", () => done()));
  let output = "";
  child.stdout.on("data", (chunk) => {
    output += chunk;
  });
  child.stderr.on("data", (chunk) => {
    output += chunk;
  });
  try {
    const runningOutput = () => {
      if (child.exitCode !== null || child.signalCode !== null)
        throw new Error(`Development launcher exited early: ${output}`);
      return output;
    };
    await expect.poll(runningOutput, { timeout: 5000 }).toContain("CATALOG:1");
    await writeFile(content, '{"revision":2}');
    await expect.poll(runningOutput, { timeout: 7000 }).toContain("CATALOG:2");
  } finally {
    child.kill("SIGTERM");
    await exited;
    await rm(directory, { recursive: true, force: true });
  }
}, 20000);
