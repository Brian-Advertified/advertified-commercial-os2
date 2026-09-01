# Gate 12 durable browser-session work packet

**Status:** IMPLEMENTED AND RESTART-VERIFIED LOCALLY — production OIDC remains separate  
**Date:** 2026-09-01  
**Normative sources:** ADR-0005; specification Sections 21.1, 26 and 27  
**Production provider:** unchanged; Cognito/OIDC remains a separate adapter/configuration slice

## Problem

The browser already uses an opaque HttpOnly session token, but the current `IBrowserSessionStore` is process memory. An API restart therefore destroys all sessions and the store cannot support multiple restartable API instances. Production requires server-side durable session state.

## Bounded implementation

- Keep the existing opaque 256-bit random browser token and store only its SHA-256 digest.
- Persist canonical `UserId`, `ActorId`, service-identity flag, creation, expiry and invalidation state in PostgreSQL.
- Never store the raw browser token.
- Preserve the current cookie, CSRF and authentication-handler contracts.
- Use the existing application database and least-privilege application role; do not add Redis, another database, another container or provider dependency.
- Keep Cognito tokens/refresh, OAuth exchange, MFA and managed secret protection outside this slice; no provider token is invented or stored.
- Expired or invalidated sessions resolve as unauthenticated; invalidation is durable.

## Migration

Add forward migration `202609010029_DurableBrowserSessions` with a dedicated `commercial.browser_sessions` table keyed by token hash, indexed by expiry, with canonical user linkage, retained actor identity and database checks for digest shape and expiry ordering. The migration must be additive and independently reversible when no active session rows prevent rollback.

## Verification

1. Pinned Linux API/migrator images compile on .NET SDK 10.0.400.
2. Existing connected journeys pass with the PostgreSQL session store.
3. A connected session created before an API container restart remains authenticated after the restart.
4. Logout invalidation remains effective after restart.
5. Architecture checks remain green.
6. No live provider, production resource, new Compose project or paid AI call is used.

## Certification boundary

This makes the provider-neutral browser session durable. It does not complete production Cognito/OIDC, provider-token encryption/refresh, MFA policy, invitation flow, production cookie-domain configuration or external identity certification.
