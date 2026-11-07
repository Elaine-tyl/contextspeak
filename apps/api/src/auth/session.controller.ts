import {
  Controller,
  Inject,
  Get,
  Post,
  Req,
  Res,
  Body,
  HttpCode,
  SetMetadata,
  BadRequestException,
  UnauthorizedException,
} from "@nestjs/common";
import type { Request, Response } from "express";
import { SessionService, hash, type Session } from "./session.service";
export const PublicAuth = () => SetMetadata("auth:public", true);
export type SessionRequest = Request & {
  authSession?: Session;
  authToken?: string;
};
function website(req: SessionRequest) {
  if (!req.authSession || req.authSession.kind !== "web")
    throw new UnauthorizedException();
  return req.authSession;
}
function field(body: unknown, key: string, pattern: RegExp) {
  if (!body || typeof body !== "object" || Array.isArray(body))
    throw new BadRequestException();
  const value = (body as Record<string, unknown>)[key];
  if (typeof value !== "string" || !pattern.test(value))
    throw new BadRequestException();
  return value;
}
@Controller("auth")
export class SessionController {
  constructor(@Inject(SessionService) private readonly auth: SessionService) {}
  @Get("login")
  @PublicAuth()
  login(@Req() req: Request, @Res() res: Response) {
    return this.auth.login(req, res);
  }
  @Get("callback")
  @PublicAuth()
  callback(@Req() req: Request, @Res() res: Response) {
    return this.auth.callback(req, res);
  }
  @Get("session")
  session(@Req() req: SessionRequest) {
    const session = website(req);
    return {
      identity: { issuer: session.user.issuer, subject: session.user.subject },
      csrfToken: hash(req.authToken! + ":csrf"),
      expiresAt: session.expiresAt,
    };
  }
  @Post("logout")
  @HttpCode(200)
  async logout(
    @Req() req: SessionRequest,
    @Res({ passthrough: true }) res: Response,
  ) {
    website(req);
    await this.auth.db.authSession.deleteMany({
      where: { tokenHash: req.authSession!.tokenHash },
    });
    this.auth.setCookie(res, this.auth.cookieName, "", 0);
    return { ok: true };
  }
  @Post("pairings")
  @PublicAuth()
  begin(@Req() req: Request) {
    return this.auth.beginPairing(req);
  }
  @Post("pairings/approve")
  @HttpCode(200)
  approve(@Req() req: SessionRequest, @Body() body: unknown) {
    const session = website(req);
    this.auth.limit(req, "approve");
    return this.auth.approve(
      field(body, "userCode", /^[A-F0-9]{12}$/),
      session.userId,
    );
  }
  @Post("pairings/poll")
  @PublicAuth()
  async poll(@Req() req: Request, @Body() body: unknown, @Res() res: Response) {
    this.auth.limit(req, "poll", 120);
    const result = await this.auth.poll(
      field(body, "deviceCode", /^[A-Za-z0-9_-]{43}$/),
    );
    res.status(result ? 200 : 202).json(result ?? { status: "pending" });
  }
  @Post("extensions/revoke")
  @HttpCode(200)
  async revokeAll(@Req() req: SessionRequest) {
    const session = website(req);
    await this.auth.db.$transaction(async (tx) => {
      await tx.user.update({
        where: { id: session.userId },
        data: { extensionVersion: { increment: 1 } },
      });
      await tx.authSession.deleteMany({
        where: { userId: session.userId, kind: "extension" },
      });
    });
    return { ok: true };
  }
  @Post("extension/disconnect")
  @HttpCode(200)
  async disconnect(@Req() req: SessionRequest) {
    if (req.authSession?.kind !== "extension")
      throw new UnauthorizedException();
    await this.auth.db.authSession.deleteMany({
      where: { tokenHash: req.authSession.tokenHash },
    });
    return { ok: true };
  }
}
