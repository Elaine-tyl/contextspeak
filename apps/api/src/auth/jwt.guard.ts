import { UnauthorizedException, type CanActivate, type ExecutionContext } from '@nestjs/common';
import type { Request } from 'express';
import type { Identity } from '@contextspeak/contracts';
import { identityVerifier, type AuthConfig } from './identity';
export type AuthenticatedRequest = Request & { identity: Identity };
export class JwtGuard implements CanActivate {
 private readonly verify: ReturnType<typeof identityVerifier>;
 constructor(config: AuthConfig) { this.verify = identityVerifier(config); }
 async canActivate(context: ExecutionContext) {
  const req = context.switchToHttp().getRequest<AuthenticatedRequest>();
  const match = /^Bearer ([^\s]+)$/.exec(req.headers.authorization ?? '');
  if (!match || match[1].length > 8192) throw new UnauthorizedException();
  try { req.identity = await this.verify(match[1]); return true; } catch { throw new UnauthorizedException(); }
 }
}
