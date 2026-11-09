import "reflect-metadata";
import { GamesController } from "./games/games.controller";
import { GamesService } from "./games/games.service";
import { SpeechController } from "./speech/speech.controller";
import { SpeechStore } from "./speech/speech.store";
import { SpeechService } from "./speech/speech.service";
import type { SpeechProvider } from "./speech/gemini-speech.provider";
import type { NestExpressApplication } from "@nestjs/platform-express";
import { SpeakingController } from "./practice/speaking.controller";
import { SpeakingService } from "./practice/speaking.service";
import type { SpeakingProvider } from "./practice/speaking.provider";
import { PracticeService } from "./practice/practice.service";
import { PracticeController } from "./practice/practice.controller";
import { ReviewService } from "./reviews/review.service";
import { ReviewController } from "./reviews/review.controller";
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
  speechProvider?: SpeechProvider;
  speechFallback?: SpeechProvider;
  speechCacheDir?: string;
  speakingProvider?: SpeakingProvider;
  provider?: TranslationProvider;
  origins?: string[];
};
export async function createApp(config: AppConfig) {
  const library = new LibraryService(
    config.prisma,
    config.provider ?? new FixtureTranslationProvider(),
  );
  const practice = new PracticeService(config.prisma, library);
  const auth =
    "oidc" in config
      ? new SessionService(config.prisma, config.oidc)
      : undefined;
  await auth?.init();
  @Module({
    controllers: [
      GamesController,
      LibraryController,
      SpeechController,
      ReviewController,
      PracticeController,
      SpeakingController,
      ...(auth ? [SessionController] : []),
    ],
    providers: [
      { provide: LibraryService, useValue: library },
      {
        provide: GamesService,
        useValue: new GamesService(
          config.prisma,
          library,
          config.speakingProvider,
        ),
      },
      {
        provide: SpeechService,
        useValue: new SpeechService(
          library,
          config.speechProvider,
          config.speechCacheDir
            ? new SpeechStore(config.prisma, config.speechCacheDir)
            : undefined,
          config.speechFallback,
        ),
      },
      {
        provide: PracticeService,
        useValue: practice,
      },
      {
        provide: SpeakingService,
        useValue: new SpeakingService(
          config.prisma,
          library,
          practice,
          config.speakingProvider,
        ),
      },
      {
        provide: ReviewService,
        useValue: new ReviewService(config.prisma, library),
      },
      ...(auth ? [{ provide: SessionService, useValue: auth }] : []),
    ],
  })
  class ApiModule {}
  const app = await NestFactory.create<NestExpressApplication>(ApiModule, {
    logger: false,
    bodyParser: false,
  });
  // Only the assessment route accepts base64 media; other JSON routes retain the small limit.
  app.useBodyParser("json", {
    limit: "3mb",
    type: (req) =>
      /^\/v1\/(?:practice\/[^/?]+\/speaking-assessment|games\/attempts)(?:\?|$)/.test(
        req.url ?? "",
      ) && (req.headers["content-type"] ?? "").startsWith("application/json"),
  });
  app.useBodyParser("json", { limit: "100kb" });
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
