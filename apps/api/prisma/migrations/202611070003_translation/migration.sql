ALTER TABLE "User" ADD COLUMN "translationPreferences" JSONB;
ALTER TABLE "LookupEvent" ADD COLUMN "detectedSourceLanguage" TEXT;
