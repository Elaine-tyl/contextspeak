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
  lookupSchema,
  translationSchema,
  settingsSchema,
} from "@contextspeak/contracts";
import type { AuthenticatedRequest } from "../auth/jwt.guard";
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
  ) {
    return this.library.list(
      req.identity,
      limit === undefined ? 20 : Number(limit),
      cursor,
    );
  }
  @Get("learning-items") items(@Req() req: AuthenticatedRequest) {
    return this.library.items(req.identity);
  }
  @Post("lookups/:id/retry") retry(
    @Req() req: AuthenticatedRequest,
    @Param("id") id: string,
  ) {
    return this.library.retry(req.identity, id);
  }
  @Post("translations") translate(@Body() body: unknown) {
    return this.library.translate(parse(translationSchema, body));
  }
}
