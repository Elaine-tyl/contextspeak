import "reflect-metadata";
import { Module } from "@nestjs/common";
import { NestFactory } from "@nestjs/core";
import { PrismaClient } from "@prisma/client";
import { JwtGuard } from "./auth/jwt.guard";
import type { AuthConfig } from "./auth/identity";
import { SessionService, type OidcConfig } from "./auth/session.service";
import { SessionController } from "./auth/session.controller";
import { LibraryController } from "./library/library.controller";
import { LibraryService } from "./library/library.service";
import {
  FixtureTranslationProvider,
  type TranslationProvider,
} from "./library/translation.provider";
export type AppConfig = (AuthConfig | { oidc: OidcConfig }) & {
  prisma: PrismaClient;
  provider?: TranslationProvider;
  origins?: string[];
};
export async function createApp(config: AppConfig) {
  const library = new LibraryService(
    config.prisma,
    config.provider ?? new FixtureTranslationProvider(),
  );
  const auth =
    "oidc" in config
      ? new SessionService(config.prisma, config.oidc)
      : undefined;
  await auth?.init();
  @Module({
    controllers: [LibraryController, ...(auth ? [SessionController] : [])],
    providers: [
      { provide: LibraryService, useValue: library },
      ...(auth ? [{ provide: SessionService, useValue: auth }] : []),
    ],
  })
  class ApiModule {}
  const app = await NestFactory.create(ApiModule, {
    logger: false,
    bodyParser: true,
  });
  app.useGlobalGuards(
    new JwtGuard("oidc" in config ? undefined : config, auth),
  );
  app.enableCors({
    origin: (
      origin: string | undefined,
      cb: (err: Error | null, allow?: boolean) => void,
    ) =>
      cb(
        null,
        !origin ||
          (
            config.origins ?? [
              auth?.options.webOrigin ?? "http://127.0.0.1:3000",
            ]
          ).includes(origin),
      ),
    credentials: true,
    methods: ["GET", "POST", "PUT"],
    allowedHeaders: ["Authorization", "Content-Type", "X-CSRF-Token"],
  });
  app.use(
    (
      _req: unknown,
      res: { setHeader: (name: string, value: string) => void },
      next: () => void,
    ) => {
      res.setHeader("Cache-Control", "no-store");
      res.setHeader("Referrer-Policy", "no-referrer");
      next();
    },
  );
  await app.init();
  library.start();
  return app;
}
