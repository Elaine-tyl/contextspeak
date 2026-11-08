import {
  Controller,
  Get,
  Post,
  Put,
  Body,
  Req,
  Param,
  Query,
  Inject,
  BadRequestException,
} from "@nestjs/common";
import {
  translationPreferencesSchema,
  lookupSchema,
  translationSchema,
  settingsSchema,
} from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
import { libraryFiltersSchema } from "./library-filters";
import { LibraryService } from "./library.service";
import type { z } from "zod";
function parse<T>(schema: z.ZodType<T>, value: unknown): T {
  const result = schema.safeParse(value);
  if (!result.success) throw new BadRequestException("Invalid request");
  return result.data;
}
@Controller("v1")
export class LibraryController {
  constructor(@Inject(LibraryService) private library: LibraryService) {}
  @Get("identity") identity(@Req() req: AuthenticatedRequest) {
    return req.identity;
  }
  @Get("translation-options") translationOptions() {
    return this.library.translationOptions();
  }
  @Get("translation-preferences") translationPreferences(
    @Req() req: AuthenticatedRequest,
  ) {
    return this.library.translationPreferences(req.identity);
  }
  @Put("translation-preferences") setTranslationPreferences(
    @Req() req: AuthenticatedRequest,
    @Body() body: unknown,
  ) {
    return this.library.setTranslationPreferences(
      req.identity,
      parse(translationPreferencesSchema, body),
    );
  }
  @Get("settings") settings(@Req() req: AuthenticatedRequest) {
    return this.library.settings(req.identity);
  }
  @Put("settings") setSettings(
    @Req() req: AuthenticatedRequest,
    @Body() body: unknown,
  ) {
    return this.library.setSettings(req.identity, parse(settingsSchema, body));
  }
  @Post("lookups") capture(
    @Req() req: AuthenticatedRequest,
    @Body() body: unknown,
  ) {
    return this.library.capture(req.identity, parse(lookupSchema, body));
  }
  @Get("lookups") history(
    @Req() req: AuthenticatedRequest,
    @Query("limit") limit?: string,
    @Query("cursor") cursor?: string,
    @Query("q") q?: string,
    @Query("sourceLanguage") sourceLanguage?: string,
    @Query("targetLanguage") targetLanguage?: string,
  ) {
    return this.library.list(
      req.identity,
      limit === undefined ? 20 : Number(limit),
      cursor,
      parse(libraryFiltersSchema, { q, sourceLanguage, targetLanguage }),
    );
  }
  @Get("learning-items") items(
    @Req() req: AuthenticatedRequest,
    @Query("q") q?: string,
    @Query("sourceLanguage") sourceLanguage?: string,
    @Query("targetLanguage") targetLanguage?: string,
  ) {
    return this.library.items(
      req.identity,
      parse(libraryFiltersSchema, { q, sourceLanguage, targetLanguage }),
    );
  }
  @Get("lookups/:id/translation") translationResult(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
  ) {
    return this.library.translationResult(req.identity, id);
  }
  @Post("lookups/:id/retry") retry(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
  ) {
    return this.library.retry(req.identity, id);
  }
  @Post("translations") translate(
    @Req() req: AuthenticatedRequest,
    @Body() body: unknown,
  ) {
    return this.library.translate(req.identity, parse(translationSchema, body));
  }
}
