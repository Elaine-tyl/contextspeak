import { spawn, type ChildProcess } from "node:child_process";
import { homedir } from "node:os";
import {
  LOCAL_AI_MODEL,
  LOCAL_AI_DIGEST,
} from "../apps/api/src/library/ollama.provider";
const url = "http://127.0.0.1:11434";
export async function startLocalAi(onSpawn: (child: ChildProcess) => void) {
  let child: ChildProcess | undefined;
  let startupError: Error | undefined;
  const tags = () =>
    fetch(url + "/api/tags", { signal: AbortSignal.timeout(2000) }).then(
      (r) => {
        if (!r.ok) throw new Error("Ollama unavailable");
        return r.json();
      },
    );
  let models;
  try {
    models = await tags();
  } catch {
    // Do not pass Google credentials or database settings to the model server.
    const spawned = spawn("ollama", ["serve"], {
      env: {
        PATH: process.env.PATH,
        HOME: homedir(),
        OLLAMA_HOST: "127.0.0.1:11434",
        NODE_ENV: process.env.NODE_ENV ?? "development",
      },
      stdio: "ignore",
    });
    child = spawned;
    onSpawn(spawned);
    spawned.once("error", (error) => {
      startupError = error;
    });
    for (let attempt = 0; attempt < 20; attempt++) {
      if (startupError)
        throw new Error(
          "Install Ollama from ollama.com, then run pnpm ai:setup.",
        );
      try {
        models = await tags();
        break;
      } catch {
        await new Promise((r) => setTimeout(r, 500));
      }
    }
  }
  if (
    !models?.models?.some(
      (model: any) =>
        model.name === LOCAL_AI_MODEL && model.digest === LOCAL_AI_DIGEST,
    )
  )
    throw new Error(
      `Pinned local AI model missing or changed. Run pnpm ai:setup. Expected ${LOCAL_AI_MODEL} (${LOCAL_AI_DIGEST}).`,
    );
  return child;
}
if (process.argv.includes("--setup")) {
  const child = spawn("ollama", ["pull", LOCAL_AI_MODEL], { stdio: "inherit" });
  const code = await new Promise<number | null>((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", resolve);
  });
  if (code !== 0)
    throw new Error("Model setup failed. Install/start Ollama first.");
  const tags = await (await fetch(url + "/api/tags")).json();
  if (
    !tags.models?.some(
      (m: any) => m.name === LOCAL_AI_MODEL && m.digest === LOCAL_AI_DIGEST,
    )
  )
    throw new Error(
      "Downloaded model digest differs from the tested model. Refusing silent model upgrade.",
    );
  console.log("Pinned local AI model ready.");
}
