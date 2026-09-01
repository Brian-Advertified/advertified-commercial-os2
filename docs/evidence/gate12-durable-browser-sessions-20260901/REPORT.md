# Gate 12 durable browser sessions — 2026-09-01

## Implemented result

The provider-neutral browser session is no longer process memory. `IBrowserSessionStore` is backed by PostgreSQL and stores only the SHA-256 digest of the existing 256-bit random opaque browser token plus canonical identity, creation, expiry and invalidation state. The raw token remains only in the HttpOnly browser cookie and is never stored in the database.

Migration `202609010029_DurableBrowserSessions` adds the dedicated `commercial.browser_sessions` table with token-digest, expiry and invalidation checks, user linkage, expiry indexes and restricted application-role privileges. Production Cognito/OIDC remains a separate provider adapter/configuration concern.

## Restart proof

The current local migration runner applied the current migration graph successfully and the API started with the PostgreSQL session store. A connected Playwright sequence then:

1. signed in and captured the exact opaque browser cookie;
2. restarted only the existing API container;
3. reused the exact pre-restart cookie and remained authenticated;
4. logged out, durably invalidating the session;
5. restarted only the existing API container a second time; and
6. reused the original cookie and remained unauthenticated.

Both restart assertions passed. The normal connected product set also passes 4/4 with the new session store, and final-tree architecture passes 42/42.

## Remaining production identity boundary

This closes server-side session restart durability; it does not claim production identity complete. Amazon Cognito/OIDC authorization code with PKCE, provider logout/refresh, MFA policy, provider-token protection/rotation, managed configuration, sandbox tests and independent Security/Privacy review still remain before production authentication can be called complete.

No new Compose project, live provider, production resource or paid AI call was used.
