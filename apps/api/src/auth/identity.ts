import { createRemoteJWKSet, jwtVerify } from "jose";
import type { Identity } from "@contextspeak/contracts";
export interface AuthConfig {
  issuer: string;
  audience: string;
  jwksUrl: string;
}
export function identityVerifier(config: AuthConfig) {
  const jwks = createRemoteJWKSet(new URL(config.jwksUrl));
  return async (token: string): Promise<Identity> => {
    const { payload } = await jwtVerify(token, jwks, {
      issuer: config.issuer,
      audience: config.audience,
      algorithms: ["RS256"],
      requiredClaims: ["sub", "exp", "iat"],
    });
    if (!payload.sub?.trim() || !payload.iss)
      throw new Error("Invalid identity");
    return { issuer: payload.iss, subject: payload.sub };
  };
}
