import { randomUUID } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import type { PrismaClient } from "@prisma/client";
import { startDatabase } from "../tests/helpers/database";

// Foreign-key order. A schema addition must be included before this rehearsal can pass.
const tables = [
  "User",
  "LearningItem",
  "LookupEvent",
  "LoginAttempt",
  "AuthSession",
  "ExtensionPairing",
  "ReviewCard",
  "ReviewEvent",
  "PracticeAttempt",
  "GameLevelSession",
  "GameAttempt",
  "SpeechAsset",
  "AccountDeletion",
] as const;
type Snapshot = Record<(typeof tables)[number], unknown[]>;

async function snapshot(db: PrismaClient): Promise<Snapshot> {
  const actual = await db.$queryRaw<{ name: string }[]>`
    SELECT table_name AS name FROM information_schema.tables
    WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
      AND table_name <> '_prisma_migrations'`;
  if (
    actual
      .map((table) => table.name)
      .sort()
      .join() !== [...tables].sort().join()
  )
    throw new Error(
      "Synthetic restore table list must match the migrated schema",
    );
  const result = {} as Snapshot;
  for (const table of tables) {
    const [row] = await db.$queryRawUnsafe<{ rows: unknown[] }[]>(
      `SELECT COALESCE(json_agg(t), '[]'::json) AS rows FROM "${table}" t`,
    );
    result[table] = row.rows;
  }
  return result;
}

async function replay(
  db: PrismaClient,
  ledger: {
    id: string;
    userId: string;
    deletedAt: string;
    hadSpeechAssets: boolean;
  }[],
) {
  for (const entry of ledger) {
    await db.$transaction(async (tx) => {
      await tx.$executeRaw`
        INSERT INTO "AccountDeletion" ("id", "userId", "deletedAt", "hadSpeechAssets", "cleanupAttempts")
        VALUES (${entry.id}, ${entry.userId}, ${new Date(entry.deletedAt)}, ${entry.hadSpeechAssets}, 0)
        ON CONFLICT ("userId") DO UPDATE SET
          "hadSpeechAssets" = "AccountDeletion"."hadSpeechAssets" OR EXCLUDED."hadSpeechAssets",
          "cleanupCompletedAt" = NULL`;
      await tx.user.deleteMany({ where: { id: entry.userId } });
    });
  }
}

/** No URL/path inputs: both databases and both JSON files are newly created in tmpdir. */
export async function runSyntheticRestore() {
  const directory = await mkdtemp(join(tmpdir(), "cs-synthetic-restore-"));
  let source: Awaited<ReturnType<typeof startDatabase>> | undefined;
  let restored: Awaited<ReturnType<typeof startDatabase>> | undefined;
  try {
    source = await startDatabase();
    const keep = randomUUID(),
      remove = randomUUID();
    for (const id of [keep, remove]) {
      await source.prisma.user.create({
        data: {
          id,
          issuer: "https://synthetic.invalid",
          subject: id,
          items: {
            create: {
              id,
              text: "synthetic",
              normalized: "synthetic",
              sourceLanguage: "en",
              targetLanguage: "en",
            },
          },
          sessions: {
            create: {
              tokenHash: id,
              kind: "web",
              expiresAt: new Date("2030-01-01"),
            },
          },
        },
      });
      await source.prisma.lookupEvent.create({
        data: {
          userId: id,
          candidateId: id,
          eventId: id,
          payloadHash: "synthetic",
          text: "synthetic",
          sourceLanguage: "en",
          targetLanguage: "en",
        },
      });
    }
    const beforeDeletion = await snapshot(source.prisma);
    const backupPath = join(directory, "synthetic-snapshot.json");
    const ledgerPath = join(directory, "later-deletion-ledger.json");
    await writeFile(backupPath, JSON.stringify(beforeDeletion), {
      mode: 0o600,
    });
    const ledger = [
      {
        id: randomUUID(),
        userId: remove,
        deletedAt: new Date().toISOString(),
        hadSpeechAssets: false,
      },
    ];
    await replay(source.prisma, ledger);
    // Separately persisted after the snapshot; never recovered from that stale snapshot.
    await writeFile(ledgerPath, JSON.stringify(ledger), { mode: 0o600 });
    restored = await startDatabase();
    const saved = JSON.parse(await readFile(backupPath, "utf8")) as Snapshot;
    await restored.prisma.$transaction(async (tx) => {
      for (const table of tables) {
        await tx.$executeRawUnsafe(
          `INSERT INTO "${table}" SELECT * FROM json_populate_recordset(NULL::"${table}", $1::json)`,
          JSON.stringify(saved[table]),
        );
      }
    });
    if ((await restored.prisma.user.count()) !== 2)
      throw new Error("Synthetic snapshot failed to restore");
    const laterLedger = JSON.parse(await readFile(ledgerPath, "utf8"));
    await replay(restored.prisma, laterLedger);
    await replay(restored.prisma, laterLedger); // Recovery retries must be idempotent.
    const remaining = await restored.prisma.user.findMany({
      select: { id: true },
    });
    if (remaining.length !== 1 || remaining[0].id !== keep)
      throw new Error("Synthetic deletion replay failed");
    const afterReplay = await snapshot(restored.prisma);
    return {
      snapshotUsers: saved.User.length,
      restoredUsers: afterReplay.User.length,
      restoredItems: afterReplay.LearningItem.length,
      restoredLookups: afterReplay.LookupEvent.length,
      restoredSessions: afterReplay.AuthSession.length,
      replayedDeletions: afterReplay.AccountDeletion.length,
    };
  } finally {
    const closed = await Promise.allSettled([
      restored?.close(),
      source?.close(),
    ]);
    await rm(directory, { recursive: true, force: true });
    for (const result of closed)
      if (result.status === "rejected") throw result.reason;
  }
}

if (
  process.argv[1] &&
  pathToFileURL(process.argv[1]).href === import.meta.url
) {
  const args = process.argv.slice(2);
  if (!args.length) {
    console.log(
      "Dry run: --synthetic creates two temporary migrated databases, backs up invented rows, restores them, replays a separate later deletion ledger twice, verifies cascades, then removes temporary data. No .env or .local is opened; DATABASE_URL is ignored. This is not a production backup or a human scoring validation.",
    );
  } else if (args.length === 1 && args[0] === "--synthetic") {
    console.log(JSON.stringify(await runSyntheticRestore(), null, 2));
  } else {
    throw new Error(
      "Usage: tsx scripts/pilot-check.ts [--synthetic]; external database/path inputs are forbidden",
    );
  }
}
