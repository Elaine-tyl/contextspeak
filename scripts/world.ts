// This standalone guest companion never loads the application's .env.local.
import { resolve } from "node:path";
import { createWorldServer } from "../packages/world/server";
import { localProviders } from "../packages/world/providers";

const server = createWorldServer({
  webRoot: resolve(".local/world-builds/web"),
  macArchive: resolve(
    ".local/world-builds/web/unity/ContextSpeak-Cafe-macOS.zip",
  ),
  ...localProviders(resolve(".local/world-tools")),
});
server.listen(4320, "127.0.0.1", () => {
  console.log("ContextSpeak Life: http://127.0.0.1:4320");
  console.log(
    "Local guest prototype · English · no cloud requests or account sync",
  );
});
server.on("error", (error) => {
  console.error(
    (error as NodeJS.ErrnoException).code === "EADDRINUSE"
      ? "Cafe is already running on port 4320."
      : "Could not start the local cafe companion.",
  );
  process.exitCode = 1;
});
for (const signal of ["SIGINT", "SIGTERM"] as const)
  process.once(signal, () => server.close(() => process.exit(0)));
