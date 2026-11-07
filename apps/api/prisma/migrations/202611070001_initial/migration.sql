-- CreateTable
CREATE TABLE "User" (
    "id" TEXT NOT NULL,
    "issuer" TEXT NOT NULL,
    "subject" TEXT NOT NULL,
    "collectionEnabled" BOOLEAN NOT NULL DEFAULT false,
    "excludedOrigins" TEXT[] DEFAULT ARRAY[]::TEXT[],
    "consentVersion" INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LearningItem" (
    "id" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "text" TEXT NOT NULL,
    "normalized" TEXT NOT NULL,
    "sourceLanguage" TEXT NOT NULL,
    "targetLanguage" TEXT NOT NULL,
    "senseStatus" TEXT NOT NULL DEFAULT 'unresolved',
    "status" TEXT NOT NULL DEFAULT 'candidate',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "LearningItem_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LookupEvent" (
    "id" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "eventId" TEXT NOT NULL,
    "payloadHash" TEXT NOT NULL,
    "candidateId" TEXT NOT NULL,
    "text" TEXT NOT NULL,
    "context" TEXT,
    "sourceUrl" TEXT,
    "sourceLanguage" TEXT NOT NULL,
    "targetLanguage" TEXT NOT NULL,
    "translationStatus" TEXT NOT NULL DEFAULT 'pending',
    "meaning" TEXT,
    "provider" TEXT,
    "errorCode" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "LookupEvent_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "User_issuer_subject_key" ON "User"("issuer", "subject");

-- CreateIndex
CREATE UNIQUE INDEX "LearningItem_userId_normalized_sourceLanguage_targetLanguag_key" ON "LearningItem"("userId", "normalized", "sourceLanguage", "targetLanguage");

-- CreateIndex
CREATE UNIQUE INDEX "LearningItem_id_userId_key" ON "LearningItem"("id", "userId");

-- CreateIndex
CREATE INDEX "LookupEvent_userId_createdAt_id_idx" ON "LookupEvent"("userId", "createdAt", "id");

-- CreateIndex
CREATE INDEX "LookupEvent_translationStatus_idx" ON "LookupEvent"("translationStatus");

-- CreateIndex
CREATE UNIQUE INDEX "LookupEvent_userId_eventId_key" ON "LookupEvent"("userId", "eventId");

-- AddForeignKey
ALTER TABLE "LearningItem" ADD CONSTRAINT "LearningItem_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LookupEvent" ADD CONSTRAINT "LookupEvent_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LookupEvent" ADD CONSTRAINT "LookupEvent_candidateId_userId_fkey" FOREIGN KEY ("candidateId", "userId") REFERENCES "LearningItem"("id", "userId") ON DELETE CASCADE ON UPDATE CASCADE;

