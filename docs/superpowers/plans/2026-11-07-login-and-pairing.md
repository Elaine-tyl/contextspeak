# Login and extension pairing implementation plan

> Use `superpowers:executing-plans` to implement this plan inline.

**Goal:** Replace developer-token UI with OIDC login and explicit extension pairing.
**Architecture:** Standard authorization-code flow through openid-client (state, nonce, PKCE). PostgreSQL stores one-use login attempts, hashed website sessions and extension credentials. The browser receives an HttpOnly session cookie; the extension receives a separate revocable credential after website approval.
**Scope:** Google sign-in through standard OIDC configuration; translation remains fixtures. Live provider credentials are configured outside Git. Existing signed-token tests remain a separate test harness, never enabled alongside production OIDC.

## Security decisions

- Seven-day website sessions; thirty-day extension credentials; ten-minute login and pairing attempts. No provider access/refresh tokens retained.
- Cookie mutations require exact website Origin and a session-specific CSRF header. Cookies are host-only, HttpOnly, SameSite=Lax, Secure outside explicit loopback development.
- Pairing has separate public user code and private polling secret. Approval requires signed-in website session and explicit confirmation; exchange is atomic and one-use. Polling is throttled.
- Identity is issuer + subject, never email. Sign-out revokes the current website session; a separate action revokes all paired extensions.
- Default development requires OIDC configuration. A local simulated OIDC provider is enabled only by an explicit test command/flag.

## Tasks

- [x] Add failing API regressions for login, state/browser binding, replay, session CSRF, pairing approval/exchange/expiry, ownership and revocation.
- [x] Add persistent auth schema, OIDC service/controller and request authentication; pass API regressions.
- [x] Replace website token entry with sign-in/session/logout and pairing approval page; replace extension token entry with pairing/disconnect. Preserve capture account partitioning.
- [x] Exercise the full flow in actual extension browser tests against a local simulated OIDC provider; keep existing capture regressions.
- [x] Document provider setup and limitations; run full tests, typecheck, formatting and production builds, then fresh review.

## Review focus

Callback in another browser, concurrent exchanges, stale pairing after disconnect, credential leakage in content messages, cross-account cookie changes in another tab. Tests must reject unsafe mutations and preserve the original capture flow.

## Verification record

Initial login regressions failed with missing routes. Pending-approval revocation failed with 200 where 401 was expected, then passed with account epochs. Tests also cover callback browser binding/expiry/PKCE/nonce, CSRF, concurrent exchange/revocation, stale tabs, pairing cancellation and existing capture behavior. Verified: `pnpm test` (51 tests), `pnpm test:e2e` (6 Chromium tests), `pnpm typecheck`, `pnpm format:check`, both production builds, `git diff --check`, and `pnpm audit --prod` (no known vulnerabilities). Review findings for unfinished approvals, late credential delivery, stale account tabs, and unsaved popup edits were fixed. Live Google sign-in remains dependent on externally configured credentials.
