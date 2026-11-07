import {
  mkdir,
  readFile,
  writeFile,
  mkdtemp,
  rm,
  access,
} from "node:fs/promises";
import { join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { spawn, execFileSync, type ChildProcess } from "node:child_process";
import EmbeddedPostgres from "embedded-postgres";
import { PrismaClient } from "@prisma/client";
import { startIssuer } from "../tests/helpers/issuer";
import { createApp } from "../apps/api/src/app";
const ephemeral = process.env.CONTEXTSPEAK_EPHEMERAL === "1";
await mkdir(".local", { recursive: true });
const directory = ephemeral
  ? await mkdtemp(join(tmpdir(), "cs-dev-"))
  : resolve(".local");
const sockets = await mkdtemp(join(tmpdir(), "cs-socket-"));
const databaseDir = join(directory, "postgres");
const database = new EmbeddedPostgres({
  databaseDir,
  port: 55432,
  user: "contextspeak",
  password: "contextspeak",
  persistent: true,
  postgresFlags: ["-h", "127.0.0.1", "-k", sockets],
  onLog: () => {},
  onError: () => {},
});
let issuer: Awaited<ReturnType<typeof startIssuer>> | undefined;
let prisma: PrismaClient | undefined;
let app: Awaited<ReturnType<typeof createApp>> | undefined;
let web: ChildProcess | undefined;
let stopping = false;
async function stop() {
  if (stopping) return;
  stopping = true;
  web?.kill("SIGTERM");
  await app?.close();
  await prisma?.$disconnect();
  await issuer?.close();
  await database.stop().catch(() => {});
  await rm(sockets, { recursive: true, force: true });
  if (ephemeral) await rm(directory, { recursive: true, force: true });
}
process.once("SIGINT", () => {
  void stop().then(() => process.exit(0));
});
process.once("SIGTERM", () => {
  void stop().then(() => process.exit(0));
});
try {
  try {
    await access(join(databaseDir, "PG_VERSION"));
  } catch {
    await database.initialise();
  }
  await database.start();
  const admin = database.getPgClient("postgres", "127.0.0.1");
  await admin.connect();
  const exists = await admin.query(
    "SELECT 1 FROM pg_database WHERE datname='contextspeak'",
  );
  if (!exists.rowCount) await admin.query("CREATE DATABASE contextspeak");
  await admin.end();
  const url =
    "postgresql://contextspeak:contextspeak@127.0.0.1:55432/contextspeak";
  execFileSync(
    process.execPath,
    [
      "node_modules/prisma/build/index.js",
      "migrate",
      "deploy",
      "--schema",
      "apps/api/prisma/schema.prisma",
    ],
    { env: { ...process.env, DATABASE_URL: url }, stdio: "pipe" },
  );
  prisma = new PrismaClient({ datasourceUrl: url });
  await prisma.$connect();
  issuer = await startIssuer(4319);
  const tokenA = await issuer.token("learner-a");
  const tokenB = await issuer.token("learner-b");
  await writeFile(".local/demo.json", JSON.stringify({ tokenA, tokenB }), {
    mode: 0o600,
  });
  app = await createApp({
    prisma,
    issuer: issuer.issuer,
    audience: issuer.audience,
    jwksUrl: issuer.jwksUrl,
  });
  await app.listen(4318, "127.0.0.1");
  console.log(
    "Local fixture API: http://127.0.0.1:4318; website: http://127.0.0.1:3000/library",
  );
  console.log(
    "Short-lived developer tokens are in .local/demo.json. Copy tokenA into both extension and website. Do not commit this file.",
  );
  web = spawn(
    process.execPath,
    [
      "node_modules/next/dist/bin/next",
      "dev",
      "apps/web",
      "--hostname",
      "127.0.0.1",
      "--port",
      "3000",
      "--webpack",
    ],
    { stdio: "inherit", env: { ...process.env, NEXT_TELEMETRY_DISABLED: "1" } },
  );
  web.once("exit", (code) => {
    if (!stopping) void stop().then(() => process.exit(code ?? 1));
  });
} catch (error) {
  console.error(
    "Local startup failed:",
    error instanceof Error ? error.message : "unknown error",
  );
  await stop();
  process.exitCode = 1;
}
