import {
  GeminiSpeechProvider,
  FixtureSpeechProvider,
} from "../apps/api/src/speech/gemini-speech.provider";
import { GeminiTranslationProvider } from "../apps/api/src/library/gemini.provider";
import { startLocalAi } from "./local-ai";
import { OllamaTranslationProvider } from "../apps/api/src/library/ollama.provider";
import { mkdir, mkdtemp, rm, access } from "node:fs/promises";
import { join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { spawn, execFileSync, type ChildProcess } from "node:child_process";
import EmbeddedPostgres from "embedded-postgres";
import { PrismaClient } from "@prisma/client";
import { startOidcIssuer } from "../tests/helpers/oidc-issuer";
import { startLocalTranslator } from "./translator";
import { LibreTranslateProvider } from "../apps/api/src/library/libretranslate.provider";
import { FixtureTranslationProvider } from "../apps/api/src/library/translation.provider";
import {
  GeminiSpeakingProvider,
  FixtureSpeakingProvider,
} from "../apps/api/src/practice/speaking.provider";
import { createApp } from "../apps/api/src/app";
try {
  process.loadEnvFile(".env.local");
} catch (error) {
  if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
}
const testAuth = process.env.CONTEXTSPEAK_TEST_AUTH === "1";
if (testAuth && process.env.NODE_ENV === "production")
  throw new Error("Simulated sign-in is forbidden in production");
if (
  !testAuth &&
  (!process.env.GOOGLE_CLIENT_ID || !process.env.GOOGLE_CLIENT_SECRET)
)
  throw new Error(
    "Set GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET in .env.local. See docs/LOGIN.md. For local simulated sign-in only: pnpm dev:test.",
  );
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
let issuer: Awaited<ReturnType<typeof startOidcIssuer>> | undefined;
let prisma: PrismaClient | undefined;
let app: Awaited<ReturnType<typeof createApp>> | undefined;
let web: ChildProcess | undefined;
let translator: ChildProcess | undefined;
let localAi: ChildProcess | undefined;
let stopping = false;
async function stop() {
  if (stopping) return;
  stopping = true;
  web?.kill("SIGTERM");
  translator?.kill("SIGTERM");
  localAi?.kill("SIGTERM");
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
  if (testAuth) issuer = await startOidcIssuer(4319);
  const fixtureTranslation = process.env.TRANSLATION_PROVIDER === "fixture";
  const useGemini = process.env.TRANSLATION_PROVIDER === "gemini";
  const useAi = (process.env.TRANSLATION_PROVIDER ?? "ollama") === "ollama";
  if (
    !fixtureTranslation &&
    !useAi &&
    !useGemini &&
    !process.env.TRANSLATOR_URL
  )
    translator = await startLocalTranslator({
      onSpawn: (child) => {
        translator = child;
      },
    });
  if (useAi && !process.env.OLLAMA_URL)
    localAi = await startLocalAi((child) => {
      localAi = child;
    });
  const provider = fixtureTranslation
    ? new FixtureTranslationProvider()
    : useGemini
      ? new GeminiTranslationProvider({ apiKey: process.env.GEMINI_API_KEY })
      : useAi
        ? new OllamaTranslationProvider({ url: process.env.OLLAMA_URL })
        : new LibreTranslateProvider({
            url: process.env.TRANSLATOR_URL ?? "http://127.0.0.1:5001",
          });
  app = await createApp({
    prisma,
    provider,
    speakingProvider: fixtureTranslation
      ? new FixtureSpeakingProvider()
      : useGemini || process.env.SPEECH_PROVIDER === "gemini"
        ? new GeminiSpeakingProvider({ apiKey: process.env.GEMINI_API_KEY })
        : undefined,
    speechProvider: fixtureTranslation
      ? new FixtureSpeechProvider()
      : process.env.SPEECH_PROVIDER === "gemini"
        ? new GeminiSpeechProvider({ apiKey: process.env.GEMINI_API_KEY })
        : undefined,
    oidc: {
      issuer: issuer?.issuer ?? "https://accounts.google.com",
      clientId: issuer?.clientId ?? process.env.GOOGLE_CLIENT_ID!,
      clientSecret: issuer?.clientSecret ?? process.env.GOOGLE_CLIENT_SECRET!,
      apiOrigin: process.env.API_ORIGIN ?? "http://127.0.0.1:4318",
      webOrigin: process.env.WEB_ORIGIN ?? "http://127.0.0.1:3000",
      allowLoopbackHttp: true,
    },
  });
  await app.listen(4318, "127.0.0.1");
  console.log(
    "ContextSpeak API: http://127.0.0.1:4318; website: http://127.0.0.1:3000/library",
  );
  if (testAuth)
    console.log(
      "WARNING: simulated OIDC sign-in enabled on loopback for local tests only.",
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
