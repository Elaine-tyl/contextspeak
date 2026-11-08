CREATE UNIQUE INDEX "LookupEvent_id_userId_key" ON "LookupEvent"("id", "userId");
CREATE TABLE "PracticeAttempt" (
  "id" TEXT NOT NULL PRIMARY KEY,
  "userId" TEXT NOT NULL,
  "eventId" TEXT NOT NULL,
  "lookupId" TEXT NOT NULL,
  "payloadHash" TEXT NOT NULL,
  "payload" JSONB NOT NULL,
  "task" JSONB NOT NULL,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "PracticeAttempt_lookupId_userId_fkey" FOREIGN KEY ("lookupId", "userId") REFERENCES "LookupEvent"("id", "userId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE UNIQUE INDEX "PracticeAttempt_userId_eventId_key" ON "PracticeAttempt"("userId", "eventId");
CREATE INDEX "PracticeAttempt_userId_lookupId_createdAt_idx" ON "PracticeAttempt"("userId", "lookupId", "createdAt");
