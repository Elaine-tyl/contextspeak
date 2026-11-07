import { existsSync } from "node:fs";
import { mkdir } from "node:fs/promises";
import { resolve, join } from "node:path";
import { pathToFileURL } from "node:url";
import { spawn, execFileSync, type ChildProcess } from "node:child_process";

const root = resolve(".local");
const project = resolve("services/translator");
const python = join(project, ".venv/bin/python");
const uv = join(root, "uv-bootstrap/bin/uv");
// The model service has no need for application credentials.
const env = {
  ...process.env,
  GOOGLE_CLIENT_SECRET: undefined,
  GOOGLE_CLIENT_ID: undefined,
  DATABASE_URL: undefined,
  UV_PYTHON_INSTALL_DIR: join(root, "python"),
  UV_CACHE_DIR: join(root, "uv-cache"),
  XDG_DATA_HOME: join(root, "translator/data"),
  XDG_CACHE_HOME: join(root, "translator/cache"),
  XDG_CONFIG_HOME: join(root, "translator/config"),
  ARGOS_CHUNK_TYPE: "MINISBD",
  ARGOS_DEVICE_TYPE: "cpu",
};
function run(command: string, args: string[]) {
  execFileSync(command, args, { env, stdio: "inherit" });
}
export async function setupTranslator() {
  await mkdir(root, { recursive: true });
  // A project-local bootstrap avoids changing the user's Python installation.
  if (!existsSync(uv)) {
    run("python3", ["-m", "venv", join(root, "uv-bootstrap")]);
    run(join(root, "uv-bootstrap/bin/python"), [
      "-m",
      "pip",
      "install",
      "uv==0.9.26",
    ]);
  }
  run(uv, ["python", "install", "3.11.14", "--no-bin"]);
  run(uv, [
    "sync",
    "--project",
    project,
    "--python",
    "3.11.14",
    "--locked",
    "--quiet",
  ]);
  run(python, [join(project, "install_models.py")]);
}
export async function startLocalTranslator(
  options: { port?: number; onSpawn?: (child: ChildProcess) => void } = {},
): Promise<ChildProcess | undefined> {
  const port = options.port ?? 5001;
  const url = `http://127.0.0.1:${port}/languages`;
  const ready = async () => {
    try {
      const response = await fetch(url, { signal: AbortSignal.timeout(700) });
      return response.ok && Array.isArray(await response.json());
    } catch {
      return false;
    }
  };
  if (await ready()) return; // Reuse an already-running local translator.
  await setupTranslator();
  const child = spawn(
    python,
    [
      "-c",
      "from libretranslate.main import main; main()",
      "--host",
      "127.0.0.1",
      "--port",
      String(port),
      "--load-only",
      "en,zh",
      "--disable-files-translation",
      "--disable-web-ui",
      "--char-limit",
      "2000",
      "--threads",
      "2",
    ],
    { env, stdio: "inherit" },
  );
  options.onSpawn?.(child); // Register before awaiting readiness, so shutdown owns the child.
  console.log(`Starting local translator (PID ${child.pid})`);
  let spawnError: Error | undefined;
  child.once("error", (error) => {
    spawnError = error;
  });
  for (let attempt = 0; attempt < 60; attempt++) {
    if (spawnError) throw spawnError;
    if (await ready()) return child;
    if (child.exitCode !== null)
      throw new Error("Local translator failed to start");
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  child.kill("SIGTERM");
  throw new Error("Local translator startup timed out");
}
if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(resolve(process.argv[1])).href
) {
  if (process.argv.includes("--setup")) await setupTranslator();
  else {
    let child: ChildProcess | undefined;
    for (const signal of ["SIGINT", "SIGTERM"] as const)
      process.once(signal, () => {
        child?.kill(signal);
        process.exit(0);
      });
    const portArgument = process.argv.indexOf("--port");
    const port =
      portArgument === -1 ? 5001 : Number(process.argv[portArgument + 1]);
    if (!Number.isInteger(port) || port < 1 || port > 65535)
      throw new Error("Invalid translator port");
    child = await startLocalTranslator({
      port,
      onSpawn: (spawned) => {
        child = spawned;
      },
    });
    console.log(`Free local translator: http://127.0.0.1:${port}`);
  }
}
