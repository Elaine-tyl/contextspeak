import { createServer } from "node:http";
import { randomBytes, createHash } from "node:crypto";
import { generateKeyPair, exportJWK, SignJWT } from "jose";

// Simulated identity provider for tests only. Never run this on a public interface.
export async function startOidcIssuer(port = 0) {
  const { privateKey, publicKey } = await generateKeyPair("RS256");
  const jwk = {
    ...(await exportJWK(publicKey)),
    kid: "oidc-test",
    alg: "RS256",
    use: "sig",
  };
  const codes = new Map<
    string,
    { sub: string; nonce: string; challenge: string; redirect: string }
  >();
  let origin = "";
  const server = createServer(async (req, res) => {
    const url = new URL(req.url!, origin);
    res.setHeader("content-type", "application/json");
    if (url.pathname === "/.well-known/openid-configuration") {
      return res.end(
        JSON.stringify({
          issuer: origin,
          authorization_endpoint: origin + "/authorize",
          token_endpoint: origin + "/token",
          jwks_uri: origin + "/jwks",
          response_types_supported: ["code"],
          subject_types_supported: ["public"],
          id_token_signing_alg_values_supported: ["RS256"],
          token_endpoint_auth_methods_supported: ["client_secret_post"],
          code_challenge_methods_supported: ["S256"],
        }),
      );
    }
    if (url.pathname === "/jwks")
      return res.end(JSON.stringify({ keys: [jwk] }));
    if (url.pathname === "/authorize") {
      if (!url.searchParams.has("test_user")) {
        res.setHeader("content-type", "text/html");
        const a = new URL(url);
        a.searchParams.set("test_user", "learner-a");
        const b = new URL(url);
        b.searchParams.set("test_user", "learner-b");
        return res.end(
          `<h1>Simulated identity provider (tests only)</h1><a href="${a.href.replaceAll("&", "&amp;")}">Test learner A</a><a href="${b.href.replaceAll("&", "&amp;")}">Test learner B</a>`,
        );
      }
      const code = randomBytes(32).toString("hex");
      codes.set(code, {
        sub: url.searchParams.get("test_user")!,
        nonce: url.searchParams.get("nonce")!,
        challenge: url.searchParams.get("code_challenge")!,
        redirect: url.searchParams.get("redirect_uri")!,
      });
      const redirect = new URL(url.searchParams.get("redirect_uri")!);
      redirect.searchParams.set("code", code);
      redirect.searchParams.set("state", url.searchParams.get("state")!);
      res.writeHead(302, { location: redirect.href });
      return res.end();
    }
    if (url.pathname === "/token") {
      let body = "";
      for await (const chunk of req) body += chunk;
      const form = new URLSearchParams(body);
      const saved = codes.get(form.get("code")!);
      codes.delete(form.get("code")!);
      if (
        !saved ||
        form.get("client_id") !== "test-client" ||
        form.get("client_secret") !== "test-secret" ||
        saved.redirect !== form.get("redirect_uri") ||
        saved.challenge !==
          createHash("sha256")
            .update(form.get("code_verifier") ?? "")
            .digest("base64url")
      ) {
        res.statusCode = 400;
        return res.end(JSON.stringify({ error: "invalid_grant" }));
      }
      const id_token = await new SignJWT({ nonce: saved.nonce })
        .setProtectedHeader({ alg: "RS256", kid: "oidc-test" })
        .setIssuer(origin)
        .setSubject(saved.sub)
        .setAudience("test-client")
        .setIssuedAt()
        .setExpirationTime("5m")
        .sign(privateKey);
      return res.end(
        JSON.stringify({
          access_token: "unused-provider-token",
          token_type: "Bearer",
          expires_in: 300,
          id_token,
        }),
      );
    }
    res.writeHead(404).end();
  });
  await new Promise<void>((r) => server.listen(port, "127.0.0.1", r));
  const address = server.address();
  if (!address || typeof address === "string")
    throw new Error("No issuer address");
  origin = `http://127.0.0.1:${address.port}`;
  return {
    issuer: origin,
    clientId: "test-client",
    clientSecret: "test-secret",
    close: () =>
      new Promise<void>((r, j) => server.close((e) => (e ? j(e) : r()))),
  };
}
