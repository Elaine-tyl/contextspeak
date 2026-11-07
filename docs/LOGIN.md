# Google login and extension pairing

## Google setup

1. In Google Cloud, configure an OAuth consent screen, audience and test users, then create an OAuth **Web application** client. Only the `openid` scope is requested; email addresses are not used as account identifiers.
2. Register this exact development redirect URI: `http://127.0.0.1:4318/auth/callback`. It is the API callback, not the website URL or an extension URL. Register the corresponding HTTPS API callback separately before a hosted pilot.
3. Copy `.env.example` to `.env.local` and set `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET`. The secret stays in the API process. Never put it in a `NEXT_PUBLIC_` or `WXT_` variable.
4. Run `pnpm dev`, open the library, and select **Sign in with Google**. New Google identities start with collection disabled. Normal development now uses free local English/Chinese translation; see [translation setup](TRANSLATION.md).

Source: [Google OpenID Connect documentation](https://developers.google.com/identity/openid-connect/openid-connect).

## Flow and guarantees

`Website → API login → Google authorization → API callback → HttpOnly session → library`

`Extension → pairing request → website sign-in + explicit approval → private polling exchange → separate extension credential`

- `openid-client` implements authorization-code exchange, PKCE, state, nonce and signed ID-token validation. Login attempts expire after ten minutes, require the initiating browser cookie, and are consumed once. Provider access/refresh tokens are discarded.
- Accounts use issuer + subject. Cookie sessions last seven days; extension credentials last thirty days with no silent refresh. Re-pair after expiry. Only credential hashes are stored in PostgreSQL; extension credentials remain in trusted extension storage. Website credentials are never stored in JavaScript storage.
- Cookie-authenticated writes require the exact website Origin and a session-specific CSRF header. Reads carrying a stale CSRF value are rejected too, protecting tabs after an account switch. Session changes require reloading the library.
- Pairings expire after ten minutes. Public approval codes are separate from high-entropy private device proofs. Poll every five seconds. Explicit approval never occurs automatically after sign-in; match the code and account before approving. The extension can access the same library/settings/translation APIs as the website, but cannot manage website sessions or approve other devices.
- Pairing exchange is transactional and single-use. Cancellation rechecks the connection revision after delivery. Failed network delivery may consume an exchange without connecting; start a new pairing. A server-issued credential that cannot be revoked while offline remains bounded by its expiry; use **Revoke all extensions** from the website to confirm revocation.
- **Sign out** revokes the current website session only. **Disconnect** clears the extension locally and attempts server revocation. **Revoke all extensions** advances an account epoch, invalidating both existing device credentials and approved but unfinished pairings, including concurrent exchanges. Other accounts are unaffected.
- No auth response is cacheable. Cookies are host-only, HttpOnly and SameSite=Lax; HTTPS origins use Secure `__Host-` cookies. Plain HTTP is accepted only with the explicit loopback development option.

## Testing and deployment boundary

`pnpm dev:test` explicitly enables a loopback simulated OIDC provider with learner A/B. `pnpm test:e2e` uses it with disposable PostgreSQL and a real unpacked extension in Chromium. The normal launcher requires Google credentials; it never falls back to test sign-in. Legacy JWT ownership tests use a separate explicit API configuration; OIDC deployments do not accept those JWTs.

Before a hosted pilot, deploy API and website on HTTPS origins on the **same site** (prefer the same origin via reverse proxy), configure the registered callback and exact CORS origin, provide managed PostgreSQL and server-side secrets, and build the browser clients with matching public origins. Cross-site cookie deployments are not supported by SameSite=Lax. The current launcher is for local development, not production hosting. WXT public origin variables must also match the extension host permission. Website public API origin is set at Next build time.

Per-process bounded rate limits protect login/pairing/approval/poll endpoints. Multi-instance production needs shared edge rate limits, lifecycle monitoring, a scheduled cleanup job for expired auth rows, and HTTPS smoke tests. Account deletion/export and provider-wide logout are separate later milestones. A localhost development OAuth client is configured in Google Cloud, and real Google sign-in was verified on 2026-11-07. Credentials remain outside Git. Installed-Chrome extension pairing and fixture capture also passed the live smoke test below. The automated suite uses a simulated provider; hosted HTTPS deployment remains unverified.

Set public origins explicitly in the build environment (the root `.env.local` is loaded by the development launcher, not automatically by every workspace build):

```bash
NEXT_PUBLIC_API_ORIGIN=https://api.example.com pnpm build:web
WXT_API_ORIGIN=https://api.example.com WXT_WEB_ORIGIN=https://learn.example.com pnpm build:extension
```


## Live development smoke test — 2026-11-07

Verified in the user's installed Chrome profile against real Google sign-in:

- The Google OAuth Web client remains in Testing with one approved test account and the exact localhost API callback.
- Google authorization returns to the library and creates an active website session for the verified Google identity.
- Explicit website approval is redeemed by the installed extension into a separate active extension credential. Collection consent is enabled and saved.
- Selecting **available** on `/sample.html` and clicking **Translate selection** saves one owned encounter and one vocabulary candidate. The selection panel and library both show **Free to take part or ready to be used.** The stored result is `succeeded`, provided by `original-fixtures-v1`.
- Grammarly remains active. A narrow root-body hydration compatibility fix is covered by browser regressions for `/library` and `/pair`; page interactions still work.

This verifies the local Google login → extension pairing → fixture capture → library journey. It does not verify a paid translation provider or hosted production deployment. The app is left running and connected for continued development. OAuth secrets remain in the ignored, owner-readable `.env.local`; they are not part of this documentation.
