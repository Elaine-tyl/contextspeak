CREATE TABLE "GameAttempt" (
  "id" TEXT NOT NULL,
  "userId" TEXT NOT NULL,
  "eventId" TEXT NOT NULL,
  "roundId" TEXT NOT NULL,
  "score" INTEGER,
  "payloadHash" TEXT NOT NULL,
  "payload" JSONB NOT NULL,
  "feedback" JSONB NOT NULL,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "GameAttempt_pkey" PRIMARY KEY ("id"),
  CONSTRAINT "GameAttempt_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE UNIQUE INDEX "GameAttempt_userId_eventId_key" ON "GameAttempt"("userId", "eventId");
CREATE INDEX "GameAttempt_userId_score_roundId_idx" ON "GameAttempt"("userId", "score", "roundId");
