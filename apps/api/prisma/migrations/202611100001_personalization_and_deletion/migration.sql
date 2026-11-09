ALTER TABLE "User" ADD COLUMN "learningProfile" JSONB;
CREATE TABLE "AccountDeletion" (
  "id" TEXT NOT NULL,
  "userId" TEXT NOT NULL,
  "deletedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "hadSpeechAssets" BOOLEAN NOT NULL DEFAULT false,
  "cleanupCompletedAt" TIMESTAMP(3),
  "cleanupAttempts" INTEGER NOT NULL DEFAULT 0,
  "lastCleanupAt" TIMESTAMP(3),
  CONSTRAINT "AccountDeletion_pkey" PRIMARY KEY ("id")
);
CREATE UNIQUE INDEX "AccountDeletion_userId_key" ON "AccountDeletion"("userId");
