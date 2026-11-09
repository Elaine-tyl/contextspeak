CREATE TABLE "GameLevelSession" (
  "id" TEXT NOT NULL,
  "userId" TEXT NOT NULL,
  "eventId" TEXT NOT NULL,
  "kind" TEXT NOT NULL,
  "level" INTEGER NOT NULL,
  "topic" TEXT,
  "source" TEXT,
  "roundIds" TEXT[] NOT NULL,
  "score" INTEGER,
  "completedAt" TIMESTAMP(3),
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "GameLevelSession_pkey" PRIMARY KEY ("id"),
  CONSTRAINT "GameLevelSession_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE UNIQUE INDEX "GameLevelSession_userId_eventId_key" ON "GameLevelSession"("userId", "eventId");
CREATE INDEX "GameLevelSession_userId_kind_level_score_idx" ON "GameLevelSession"("userId", "kind", "level", "score");
ALTER TABLE "GameAttempt" ADD COLUMN "levelSessionId" TEXT;
ALTER TABLE "GameAttempt" ADD CONSTRAINT "GameAttempt_levelSessionId_fkey" FOREIGN KEY ("levelSessionId") REFERENCES "GameLevelSession"("id") ON DELETE CASCADE ON UPDATE CASCADE;
CREATE UNIQUE INDEX "GameAttempt_levelSessionId_roundId_key" ON "GameAttempt"("levelSessionId", "roundId");
