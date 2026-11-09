import {
  BadRequestException,
  Body,
  Controller,
  Get,
  HttpCode,
  Inject,
  Post,
  Req,
  Res,
  UnauthorizedException,
} from "@nestjs/common";
import { pipeline } from "node:stream/promises";
import type { Response } from "express";
import type { SessionRequest } from "../auth/session.controller";
import { SessionService } from "../auth/session.service";
import { AccountService } from "./account.service";
function website(req: SessionRequest) {
  if (req.authSession?.kind !== "web") throw new UnauthorizedException();
  return req.authSession;
}
@Controller("v1/account")
export class AccountController {
  constructor(
    @Inject(AccountService) private readonly accounts: AccountService,
    @Inject(SessionService) private readonly auth: SessionService,
  ) {}
  @Get("export")
  async export(@Req() req: SessionRequest, @Res() res: Response) {
    const session = website(req);
    this.auth.limit(req, "account-export", 3);
    await this.accounts.withExport(session.userId, async (file) => {
      res.setHeader("Cache-Control", "no-store");
      res.setHeader("X-Content-Type-Options", "nosniff");
      res.attachment("contextspeak-data.json");
      await pipeline(
        file.createReadStream({ start: 0, autoClose: false }),
        res,
      );
    });
  }
  @Post("delete")
  @HttpCode(200)
  async remove(
    @Req() req: SessionRequest,
    @Body() body: unknown,
    @Res({ passthrough: true }) res: Response,
  ) {
    const session = website(req);
    if (
      !body ||
      typeof body !== "object" ||
      Array.isArray(body) ||
      (body as Record<string, unknown>).confirmation !== "DELETE"
    )
      throw new BadRequestException("Type DELETE to confirm account deletion.");
    const result = await this.accounts.remove(session.userId);
    this.auth.setCookie(res, this.auth.cookieName, "", 0);
    return result;
  }
}
