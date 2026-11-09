import { spawn, type ChildProcess } from "node:child_process";
import { homedir } from "node:os";
import {
  LOCAL_SPEECH_MODEL,
  LOCAL_SPEECH_DIGEST,
} from "../apps/api/src/speech/ollama-speech.provider";
const url = "http://127.0.0.1:11434";
async function tags() {
  const response = await fetch(url + "/api/tags", {
    signal: AbortSignal.timeout(2000),
  });
  if (!response.ok) throw new Error("Ollama unavailable");
  return response.json();
}
export async function startLocalSpeech(setup = false) {
  let child: ChildProcess | undefined;
  try {
    await tags();
  } catch {
    // Keep application credentials out of the local model process environment.
    child = spawn("ollama", ["serve"], {
      env: {
        PATH: process.env.PATH,
        HOME: homedir(),
        NODE_ENV: process.env.NODE_ENV ?? "development",
        OLLAMA_HOST: "127.0.0.1:11434",
      },
      stdio: "ignore",
    });
    let failure: Error | undefined;
    child.once("error", (error) => {
      failure = error;
    });
    for (let attempt = 0; attempt < 20; attempt++) {
      if (failure)
        throw new Error("Install Ollama, then run pnpm speech:setup.");
      try {
        await tags();
        break;
      } catch {
        await new Promise((resolve) => setTimeout(resolve, 250));
      }
    }
  }
  try {
    if (setup) {
      const pull = spawn("ollama", ["pull", LOCAL_SPEECH_MODEL], {
        env: {
          PATH: process.env.PATH,
          HOME: homedir(),
          NODE_ENV: process.env.NODE_ENV ?? "development",
          OLLAMA_HOST: "127.0.0.1:11434",
        },
        stdio: "inherit",
      });
      const code = await new Promise((resolve, reject) => {
        pull.once("exit", resolve);
        pull.once("error", reject);
      });
      if (code !== 0) throw new Error("Phonon download failed.");
    }
    const models = await tags();
    if (
      !models.models?.some(
        (model: { name: string; digest: string }) =>
          model.name === LOCAL_SPEECH_MODEL &&
          model.digest === LOCAL_SPEECH_DIGEST,
      )
    )
      throw new Error(
        "Pinned Phonon model missing or changed. Run pnpm speech:setup.",
      );
    console.log("Pinned local Phonon English voice ready.");
    return child;
  } catch (error) {
    child?.kill("SIGTERM");
    throw error;
  }
}
if (process.argv.includes("--setup")) {
  const server = await startLocalSpeech(true);
  server?.unref();
}
