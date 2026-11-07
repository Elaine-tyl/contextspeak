-- AlterTable
ALTER TABLE "User" ADD COLUMN     "extensionVersion" INTEGER NOT NULL DEFAULT 0;

-- CreateTable
CREATE TABLE "LoginAttempt" (
    "stateHash" TEXT NOT NULL,
    "browserHash" TEXT NOT NULL,
    "verifier" TEXT NOT NULL,
    "nonce" TEXT NOT NULL,
    "destination" TEXT NOT NULL,
    "expiresAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "LoginAttempt_pkey" PRIMARY KEY ("stateHash")
);

-- CreateTable
CREATE TABLE "AuthSession" (
    "tokenHash" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "kind" TEXT NOT NULL,
    "extensionVersion" INTEGER NOT NULL DEFAULT 0,
    "expiresAt" TIMESTAMP(3) NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "AuthSession_pkey" PRIMARY KEY ("tokenHash")
);

-- CreateTable
CREATE TABLE "ExtensionPairing" (
    "deviceHash" TEXT NOT NULL,
    "userCodeHash" TEXT NOT NULL,
    "userId" TEXT,
    "extensionVersion" INTEGER NOT NULL DEFAULT 0,
    "expiresAt" TIMESTAMP(3) NOT NULL,
    "consumedAt" TIMESTAMP(3),
    "lastPolledAt" TIMESTAMP(3),

    CONSTRAINT "ExtensionPairing_pkey" PRIMARY KEY ("deviceHash")
);

-- CreateIndex
CREATE INDEX "AuthSession_userId_kind_idx" ON "AuthSession"("userId", "kind");

-- CreateIndex
CREATE INDEX "AuthSession_expiresAt_idx" ON "AuthSession"("expiresAt");

-- CreateIndex
CREATE UNIQUE INDEX "ExtensionPairing_userCodeHash_key" ON "ExtensionPairing"("userCodeHash");

-- CreateIndex
CREATE INDEX "ExtensionPairing_expiresAt_idx" ON "ExtensionPairing"("expiresAt");

-- AddForeignKey
ALTER TABLE "AuthSession" ADD CONSTRAINT "AuthSession_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ExtensionPairing" ADD CONSTRAINT "ExtensionPairing_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;
