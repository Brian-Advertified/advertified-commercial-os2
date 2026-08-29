# ADR-0005: Identity, browser session and service authentication

## Status

Accepted for local non-production Gate 2 implementation — Brian Rabuthu, 2026-08-29. Remote publication, production and deployment remain prohibited.

## Context

Gate 2 requires authenticated tenant-scoped commands. React must not hold provider credentials or trusted tenant/role claims. The Commercial API must resolve identity, memberships and permissions. Redevelopment remains local and zero-live-provider.

## Decision owner and reviewers

| Responsibility | Actual name | Decision/date |
|---|---|---|
| Accountable owner | Brian Rabuthu | Accepted, 2026-08-29 |
| Engineering reviewer | Not required for local-only implementation | Independent review before publication |
| Security/privacy reviewer | Not required for local-only implementation | Independent review before publication/production |
| Operations reviewer | Not required for local-only implementation | Independent review before deployment |

Brian Rabuthu is the sole required reviewer for this reversible local-only decision. Independent reviews remain mandatory before publication, production or deployment.

## Options considered

1. Browser-held bearer tokens: simple SPA integration, but exposes tokens to browser storage and increases XSS impact.
2. C# backend-for-frontend session over OIDC: keeps provider tokens server-side and centralises CSRF, logout and membership resolution.
3. Local username/password tables: rejected for production because the domain must not store provider passwords or become an identity provider.

## Proposed decision

- Amazon Cognito User Pools is the production identity provider through standards-based OIDC.
- Interactive sign-in uses authorization code with PKCE.
- The C# API acts as a backend for frontend. React receives only an opaque secure session cookie and safe `/me` representation; it never receives or stores provider tokens.
- Session state is server-side with an opaque identifier. Production cookies are `HttpOnly`, `Secure`, host-scoped and `SameSite=Lax`; names, lifetimes and domains are typed configuration.
- Unsafe cookie-authenticated requests require ASP.NET Core antiforgery validation plus same-origin checks. CORS is an explicit allow-list.
- Logout invalidates server session state before redirecting through the provider logout flow.
- The API maps the provider subject to one canonical User and resolves active Membership records. Provider groups or browser claims never replace Commercial API permissions.
- Agent and worker identities use separate non-interactive OAuth clients/scopes. They cannot use human sessions or impersonate an end user.
- Development and tests use an explicit deterministic authentication adapter available only in Development/Test. Production startup fails if that adapter is enabled.
- No live Cognito resource, SDK call or credential is introduced in Gate 2 local work.

## Consequences

This adds server-side session storage and CSRF handling, but reduces browser token exposure and keeps tenant authority in the C# API. Cognito configuration remains deploy-time infrastructure and is not created by application startup.

## Implementation boundary

Gate 2 may implement provider-neutral identity/session ports, safe `/me` and `/workspaces` contracts, membership resolution and deterministic local authentication. Live provider setup and authenticated application pages remain separately gated.

## Verification

Only acceptance-critical evidence is required:

- unauthenticated, expired-session and invalid-CSRF requests deny safely;
- inactive user/membership and wrong-tenant cases return the same non-leaking denial;
- React/browser storage contains no provider token;
- development adapter cannot start in Production;
- service identities cannot use interactive permissions.

Equivalent denial cases are parameterised; provider framework behavior is not retested.

## References

- Normative sections 18.2, 20 and 21.1
- [Amazon Cognito authorization code with PKCE](https://docs.aws.amazon.com/cognito/latest/developerguide/using-pkce-in-authorization-code.html)
- [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery)
- `docs/adr/0002-no-autonomous-spend-or-publication.md`

## Decision record

- Proposed by: implementation agent for Brian Rabuthu
- Proposed date: 2026-08-29
- Accepted/rejected by: Brian Rabuthu
- Decision date: 2026-08-29
- Supersedes/superseded by: none
