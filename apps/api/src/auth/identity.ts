import { createRemoteJWKSet, jwtVerify } from "jose";
import type { Identity } from "@contextspeak/contracts";
// Only server-created identities can carry a pinned session owner; JSON/JWT claims cannot forge it.
const owners = new WeakMap<Identity, string>();
export function sessionIdentity(user: {
  id: string;
  issuer: string;
  subject: string;
}): Identity {
  const identity = { issuer: user.issuer, subject: user.subject };
  owners.set(identity, user.id);
  return identity;
}
export const sessionOwner = (identity: Identity) => owners.get(identity);
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
