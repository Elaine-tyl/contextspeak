import { createHash, randomBytes } from "node:crypto";
import {
  ForbiddenException,
  UnauthorizedException,
  ConflictException,
  HttpException,
} from "@nestjs/common";
import type { PrismaClient, AuthSession, User } from "@prisma/client";
import type { Request, Response } from "express";
import * as oidc from "openid-client";
export interface OidcConfig {
  issuer: string;
  clientId: string;
  clientSecret: string;
  apiOrigin: string;
  webOrigin: string;
  allowLoopbackHttp?: boolean;
}
export const hash = (value: string) =>
  createHash("sha256").update(value).digest("hex");
const secret = () => randomBytes(32).toString("base64url");
const later = (seconds: number) => new Date(Date.now() + seconds * 1000);
export type Session = AuthSession & { user: User };
export class SessionService {
  readonly cookieName: string;
  readonly browserCookie: string;
  private config!: oidc.Configuration;
  private readonly limits = new Map<string, { until: number; count: number }>();
  constructor(
    readonly db: PrismaClient,
    readonly options: OidcConfig,
  ) {
    for (const value of [
      options.issuer,
      options.apiOrigin,
      options.webOrigin,
    ]) {
      const url = new URL(value);
      if (url.username || url.password || url.search || url.hash)
        throw new Error("Invalid auth URL");
      if (
        url.protocol !== "https:" &&
        !(
          options.allowLoopbackHttp &&
          url.protocol === "http:" &&
          ["127.0.0.1", "localhost", "[::1]"].includes(url.hostname)
        )
      )
        throw new Error(
          "Authentication requires HTTPS (except explicit loopback development)",
        );
    }
    for (const value of [options.apiOrigin, options.webOrigin])
      if (new URL(value).origin !== value)
        throw new Error("Auth origins must not contain a path");
    this.cookieName = options.apiOrigin.startsWith("https:")
      ? "__Host-cs_session"
      : "cs_session";
    this.browserCookie = options.apiOrigin.startsWith("https:")
      ? "__Host-cs_login"
      : "cs_login";
  }
  async init() {
    this.config = await oidc.discovery(
      new URL(this.options.issuer),
      this.options.clientId,
      this.options.clientSecret,
      oidc.ClientSecretPost(this.options.clientSecret),
      {
        timeout: 8,
        ...(this.options.allowLoopbackHttp
          ? { execute: [oidc.allowInsecureRequests] }
          : {}),
      },
    );
  }
  cookie(req: Request, name: string) {
    const values = (req.headers.cookie ?? "")
      .split(";")
      .map((x) => x.trim())
      .filter((x) => x.startsWith(name + "="));
    if (values.length !== 1) return "";
    const value = values[0].slice(name.length + 1);
    return /^[A-Za-z0-9_-]{43}$/.test(value) ? value : "";
  }
  setCookie(res: Response, name: string, value: string, seconds: number) {
    res.cookie(name, value, {
      httpOnly: true,
      secure: this.options.apiOrigin.startsWith("https:"),
      sameSite: "lax",
      path: "/",
      maxAge: seconds * 1000,
    });
  }
  // Bounded per-process abuse protection. Deployments also need shared edge limits.
  limit(req: Request, action: string, maximum = 20) {
    const now = Date.now();
    for (const [key, value] of this.limits)
      if (value.until <= now) this.limits.delete(key);
    const key = action + ":" + (req.ip ?? "unknown");
    const value = this.limits.get(key) ?? { until: now + 60000, count: 0 };
    if (this.limits.size >= 5000 || ++value.count > maximum)
      throw new HttpException("Too many requests", 429);
    this.limits.set(key, value);
  }
  async cleanup() {
    const now = new Date();
    await Promise.all([
      this.db.loginAttempt.deleteMany({ where: { expiresAt: { lte: now } } }),
      this.db.extensionPairing.deleteMany({
        where: { expiresAt: { lte: now } },
      }),
      this.db.authSession.deleteMany({ where: { expiresAt: { lte: now } } }),
    ]);
  }
  async login(req: Request, res: Response) {
    this.limit(req, "login");
    await this.cleanup();
    const state = oidc.randomState(),
      nonce = oidc.randomNonce(),
      verifier = oidc.randomPKCECodeVerifier(),
      browser = secret();
    const code =
      typeof req.query.pair === "string" &&
      /^[A-F0-9]{12}$/.test(req.query.pair)
        ? req.query.pair
        : "";
    await this.db.loginAttempt.create({
      data: {
        stateHash: hash(state),
        browserHash: hash(browser),
        verifier,
        nonce,
        destination: code
          ? "/pair?code=" + code
          : req.query.returnTo === "/games"
            ? "/games"
            : "/library",
        expiresAt: later(600),
      },
    });
    this.setCookie(res, this.browserCookie, browser, 600);
    const url = oidc.buildAuthorizationUrl(this.config, {
      redirect_uri: this.options.apiOrigin + "/auth/callback",
      scope: "openid",
      state,
      nonce,
      code_challenge: await oidc.calculatePKCECodeChallenge(verifier),
      code_challenge_method: "S256",
      prompt: "select_account",
    });
    res.redirect(url.href);
  }
  async callback(req: Request, res: Response) {
    this.limit(req, "callback");
    const state = typeof req.query.state === "string" ? req.query.state : "";
    const browser = this.cookie(req, this.browserCookie);
    const attempt = await this.db.loginAttempt.findUnique({
      where: { stateHash: hash(state) },
    });
    if (
      !attempt ||
      !browser ||
      hash(browser) !== attempt.browserHash ||
      attempt.expiresAt <= new Date()
    )
      throw new UnauthorizedException("Sign-in expired. Please try again.");
    const consumed = await this.db.loginAttempt.deleteMany({
      where: { stateHash: hash(state), browserHash: hash(browser) },
    });
    if (consumed.count !== 1) throw new UnauthorizedException();
    try {
      const url = new URL(req.originalUrl, this.options.apiOrigin);
      const tokens = await oidc.authorizationCodeGrant(this.config, url, {
        pkceCodeVerifier: attempt.verifier,
        expectedState: state,
        expectedNonce: attempt.nonce,
        idTokenExpected: true,
      });
      const claims = tokens.claims();
      if (!claims?.sub || !claims.iss) throw new Error("Missing identity");
      const user = await this.db.user.upsert({
        where: { issuer_subject: { issuer: claims.iss, subject: claims.sub } },
        create: { issuer: claims.iss, subject: claims.sub },
        update: {},
      });
      // Rotate an existing session on successful sign-in; never reuse caller credentials.
      const previous = this.cookie(req, this.cookieName);
      if (previous)
        await this.db.authSession.deleteMany({
          where: { tokenHash: hash(previous), kind: "web" },
        });
      const credential = await this.issue(user.id, "web", 7 * 86400);
      this.setCookie(res, this.cookieName, credential, 7 * 86400);
      this.setCookie(res, this.browserCookie, "", 0);
      res.redirect(this.options.webOrigin + attempt.destination);
    } catch {
      throw new UnauthorizedException(
        "Google sign-in failed. Please try again.",
      );
    }
  }
  async issue(userId: string, kind: "web" | "extension", seconds: number) {
    const token = secret();
    await this.db.authSession.create({
      data: { tokenHash: hash(token), userId, kind, expiresAt: later(seconds) },
    });
    return token;
  }
  async authenticate(
    req: Request,
  ): Promise<{ session: Session; token: string }> {
    const header = req.headers.authorization;
    const bearer = header !== undefined;
    const token = bearer
      ? /^Bearer ([A-Za-z0-9_-]{43})$/.exec(header)?.[1]
      : this.cookie(req, this.cookieName);
    if (!token) throw new UnauthorizedException();
    const session = await this.db.authSession.findUnique({
      where: { tokenHash: hash(token) },
      include: { user: true },
    });
    if (
      !session ||
      session.kind !== (bearer ? "extension" : "web") ||
      session.expiresAt <= new Date() ||
      (bearer && session.extensionVersion !== session.user.extensionVersion)
    )
      throw new UnauthorizedException();
    if (!bearer) {
      const csrf = req.headers["x-csrf-token"];
      const expected = hash(token + ":csrf");
      // A stale tab must not silently act as a newly signed-in account, even for GETs.
      if (csrf !== undefined && csrf !== expected)
        throw new UnauthorizedException();
      if (
        !["GET", "HEAD", "OPTIONS"].includes(req.method) &&
        (req.headers.origin !== this.options.webOrigin || csrf !== expected)
      )
        throw new ForbiddenException();
    }
    return { session, token };
  }
  async beginPairing(req: Request) {
    this.limit(req, "pair", 10);
    await this.cleanup();
    const deviceCode = secret(),
      userCode = randomBytes(6).toString("hex").toUpperCase();
    await this.db.extensionPairing.create({
      data: {
        deviceHash: hash(deviceCode),
        userCodeHash: hash(userCode),
        expiresAt: later(600),
      },
    });
    return {
      deviceCode,
      userCode,
      verificationUrl: this.options.webOrigin + "/pair?code=" + userCode,
      expiresIn: 600,
      interval: 5,
    };
  }
  async approve(userCode: string, userId: string) {
    const user = await this.db.user.findUniqueOrThrow({
      where: { id: userId },
    });
    const result = await this.db.extensionPairing.updateMany({
      where: {
        userCodeHash: hash(userCode),
        userId: null,
        consumedAt: null,
        expiresAt: { gt: new Date() },
      },
      data: { userId, extensionVersion: user.extensionVersion },
    });
    if (!result.count)
      throw new ConflictException(
        "Code expired or already approved. Start pairing again.",
      );
    return { ok: true };
  }
  async poll(deviceCode: string) {
    return this.db.$transaction(async (tx) => {
      // Lock by the high-entropy private proof; a public code can never redeem credentials.
      await tx.$queryRaw`SELECT "deviceHash" FROM "ExtensionPairing" WHERE "deviceHash" = ${hash(deviceCode)} FOR UPDATE`;
      const pair = await tx.extensionPairing.findUnique({
        where: { deviceHash: hash(deviceCode) },
      });
      if (!pair || pair.consumedAt || pair.expiresAt <= new Date())
        throw new UnauthorizedException();
      if (pair.lastPolledAt && Date.now() - pair.lastPolledAt.getTime() < 5000)
        throw new HttpException("Poll every five seconds", 429);
      await tx.extensionPairing.update({
        where: { deviceHash: pair.deviceHash },
        data: { lastPolledAt: new Date() },
      });
      if (!pair.userId) return null;
      const user = await tx.user.findUniqueOrThrow({
        where: { id: pair.userId },
      });
      if (pair.extensionVersion !== user.extensionVersion)
        throw new UnauthorizedException();
      const token = secret();
      await tx.authSession.create({
        data: {
          tokenHash: hash(token),
          userId: pair.userId,
          kind: "extension",
          extensionVersion: pair.extensionVersion,
          expiresAt: later(30 * 86400),
        },
      });
      await tx.extensionPairing.update({
        where: { deviceHash: pair.deviceHash },
        data: { consumedAt: new Date() },
      });
      return { token, expiresIn: 30 * 86400 };
    });
  }
}
