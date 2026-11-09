CREATE TABLE "SpeechAsset" (
  "key" TEXT NOT NULL,
  "userId" TEXT NOT NULL,
  "bytes" INTEGER NOT NULL,
  "requestHash" TEXT NOT NULL,
  "digest" TEXT NOT NULL,
  "lastUsedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "SpeechAsset_pkey" PRIMARY KEY ("key"),
  CONSTRAINT "SpeechAsset_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE INDEX "SpeechAsset_userId_requestHash_lastUsedAt_idx" ON "SpeechAsset"("userId", "requestHash", "lastUsedAt");
CREATE INDEX "SpeechAsset_lastUsedAt_idx" ON "SpeechAsset"("lastUsedAt");
