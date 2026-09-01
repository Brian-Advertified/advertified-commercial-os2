# Gate 12 production OIDC work packet

**Status:** IN PROGRESS — implementation and deterministic verification only
**Date:** 2026-09-01
**Normative sources:** ADR-0005; specification Sections 20, 21.1, 26 and 27
**Provider decision:** Amazon Cognito User Pools through standards-based OpenID Connect authorization code flow with PKCE

## Bounded requirement

Complete the provider side of the existing backend-for-frontend identity design without moving authority into the browser or identity provider. React must continue to receive only the opaque Advertified session cookie and safe session status. The Commercial API remains responsible for canonical User, Membership, tenant and permission resolution.

## Implementation

- Add a production `Oidc` authentication mode using ASP.NET Core OpenID Connect code flow with PKCE.
- Require HTTPS authority/logout endpoints, client ID and managed client secret configuration outside local/test execution.
- Keep provider tokens out of browser storage and out of the durable Advertified session table; provider tokens are not persisted by this slice.
- Resolve the immutable OIDC subject through a provider/subject binding. On first successful login, an identity may bind only to one existing active canonical User whose verified email uniquely matches; provider role/group claims never grant Advertified permissions.
- Persist only a SHA-256 digest of the provider subject in the identity binding table.
- Reuse `PostgresBrowserSessionStore` after successful OIDC authentication; session expiry cannot exceed the validated provider token expiry.
- Enforce configured MFA evidence for canonical users marked `mfa_enabled`.
- Add browser login/logout endpoints that preserve only safe local return paths and invoke the configured Cognito logout endpoint after durable local invalidation.
- Keep deterministic local sign-in unchanged for Development/Test.

## Security boundaries

- No auto-provisioned Advertified user, membership, tenant or role is created from provider claims.
- Email matching requires `email_verified=true`; an existing conflicting provider-subject binding fails closed.
- The OIDC callback never trusts browser-supplied tenant or role claims.
- OIDC configuration is startup-validated and deterministic authentication remains forbidden outside Development/Test.
- Login/callback failure returns to a human-safe sign-in state without exposing tokens, provider payloads or exception text.

## Verification after the release batch is complete

- Production-mode startup rejects incomplete or unsafe OIDC configuration.
- Local deterministic browser-session and connected journeys remain green.
- OIDC resolver tests prove verified-email first binding, immutable subject binding, inactive/ambiguous user denial and MFA denial.
- OpenAPI/Zod contracts agree after the session surface changes.
- No live Cognito request or production credential is used during redevelopment/certification.
