import "reflect-metadata";
import { Module } from "@nestjs/common";
import { NestFactory } from "@nestjs/core";
import { PrismaClient } from "@prisma/client";
import { JwtGuard } from "./auth/jwt.guard";
import type { AuthConfig } from "./auth/identity";
import { LibraryController } from "./library/library.controller";
import { LibraryService } from "./library/library.service";
import {
  FixtureTranslationProvider,
  type TranslationProvider,
} from "./library/translation.provider";
export type AppConfig = AuthConfig & {
  prisma: PrismaClient;
  provider?: TranslationProvider;
  origins?: string[];
};
export async function createApp(config: AppConfig) {
  const library = new LibraryService(
    config.prisma,
    config.provider ?? new FixtureTranslationProvider(),
  );
  @Module({
    controllers: [LibraryController],
    providers: [{ provide: LibraryService, useValue: library }],
  })
  class ApiModule {}
  const app = await NestFactory.create(ApiModule, {
    logger: false,
    bodyParser: true,
  });
  app.useGlobalGuards(new JwtGuard(config));
  app.enableCors({
    origin: (
      origin: string | undefined,
      cb: (err: Error | null, allow?: boolean) => void,
    ) =>
      cb(
        null,
        !origin ||
          (config.origins ?? ["http://127.0.0.1:3000"]).includes(origin),
      ),
    methods: ["GET", "POST", "PUT"],
    allowedHeaders: ["Authorization", "Content-Type"],
  });
  await app.init();
  library.start();
  return app;
}
