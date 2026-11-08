CREATE TABLE "ReviewCard" (
  "itemId" TEXT NOT NULL PRIMARY KEY,
  "userId" TEXT NOT NULL,
  "prompt" TEXT NOT NULL,
  "answer" TEXT NOT NULL,
  "context" TEXT,
  "active" BOOLEAN NOT NULL DEFAULT true,
  "dueAt" TIMESTAMP(3) NOT NULL,
  "schedule" JSONB NOT NULL,
  "version" INTEGER NOT NULL DEFAULT 0,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "ReviewCard_itemId_userId_fkey" FOREIGN KEY ("itemId", "userId") REFERENCES "LearningItem"("id", "userId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE UNIQUE INDEX "ReviewCard_itemId_userId_key" ON "ReviewCard"("itemId", "userId");
CREATE INDEX "ReviewCard_userId_active_dueAt_idx" ON "ReviewCard"("userId", "active", "dueAt");
CREATE TABLE "ReviewEvent" (
  "id" TEXT NOT NULL PRIMARY KEY,
  "userId" TEXT NOT NULL,
  "eventId" TEXT NOT NULL,
  "itemId" TEXT NOT NULL,
  "rating" TEXT NOT NULL,
  "version" INTEGER NOT NULL,
  "dueAt" TIMESTAMP(3) NOT NULL,
  "log" JSONB NOT NULL,
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "ReviewEvent_itemId_userId_fkey" FOREIGN KEY ("itemId", "userId") REFERENCES "ReviewCard"("itemId", "userId") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE UNIQUE INDEX "ReviewEvent_userId_eventId_key" ON "ReviewEvent"("userId", "eventId");
