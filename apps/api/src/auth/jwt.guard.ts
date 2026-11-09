import {
  UnauthorizedException,
  type CanActivate,
  type ExecutionContext,
} from "@nestjs/common";
import type { Request } from "express";
import type { Identity } from "@contextspeak/contracts";
import { identityVerifier, sessionIdentity, type AuthConfig } from "./identity";
import type { SessionService } from "./session.service";
import type { SessionRequest } from "./session.controller";
export type AuthenticatedRequest = Request & { identity: Identity };
export class JwtGuard implements CanActivate {
  private readonly verify: ReturnType<typeof identityVerifier> | undefined;
  constructor(
    config?: AuthConfig,
    private readonly auth?: SessionService,
  ) {
    this.verify = config ? identityVerifier(config) : undefined;
  }
  async canActivate(context: ExecutionContext) {
    const req = context.switchToHttp().getRequest<AuthenticatedRequest>();
    if (Reflect.getMetadata("auth:public", context.getHandler())) return true;
    if (this.auth) {
      const { session, token } = await this.auth.authenticate(req);
      (req as SessionRequest).authSession = session;
      (req as SessionRequest).authToken = token;
      req.identity = sessionIdentity(session.user);
      return true;
    }
    const match = /^Bearer ([^\s]+)$/.exec(req.headers.authorization ?? "");
    if (!match || match[1].length > 8192) throw new UnauthorizedException();
    try {
      req.identity = await this.verify!(match[1]);
      return true;
    } catch {
      throw new UnauthorizedException();
    }
  }
}
