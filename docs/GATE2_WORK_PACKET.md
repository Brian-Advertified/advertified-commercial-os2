# Gate 2 work packet — canonical commercial foundation

## Status

Approved for local non-production implementation by Brian Rabuthu on 2026-08-29. ADR-0005 through ADR-0008 are Accepted for this bounded local scope.

Remote publication and CI are owner-deferred. That is not a PASS and does not authorise merge, release or deployment. Independent Engineering, Security/Privacy and Operations review remains mandatory before publication, production or deployment.

## Outcome

Authenticated, tenant-scoped C# commands persist the first canonical commercial aggregates in PostgreSQL with database-enforced isolation, concurrency, idempotency, audit and outbox consequences.

## Required decisions

- ADR-0005: identity, browser session, CSRF, logout and service identities.
- ADR-0006: PostgreSQL tenant isolation and tenant-safe associations.
- ADR-0007: migration ownership and local/CI execution topology.
- ADR-0008: Zod, NotificationService/Toastr, human-safe errors and visual experience. Its Gate 2 API-boundary rules apply now; screen implementation remains Gate 3.

## In scope

### Canonical aggregates

- Tenant;
- User;
- Membership;
- ClientAccount;
- Agency as a tenant-owned profile, not a duplicate identity or tenancy system;
- Contact with purpose-limited personal data and consent basis.

### Persistence and consequences

- PostgreSQL migrations and separate application/migration roles;
- direct tenant keys, composite tenant-safe foreign keys and row-level security where specified;
- governed status, role, tenant-type, VAT, currency and contact-purpose codes from master data;
- bigint optimistic version, UTC timestamps and UUID identifiers;
- idempotency record, append-only audit event and transactional outbox;
- indexes for tenant, status and stable query sort.

### API foundation

- `/api/v1/me` and `/api/v1/workspaces`;
- tenant-scoped commands and queries required for the six aggregates;
- secure deterministic development identity adapter;
- correlation, ETag/If-Match, Idempotency-Key and cursor contracts;
- safe `application/problem+json` responses with stable codes;
- versioned OpenAPI output;
- Zod validation when the browser first consumes any Gate 2 contract.

### User-language boundary

The API and browser expose only safe business wording. Raw exceptions, SQL/provider messages, stack traces, prompts, private reasoning, internal job messages and implementation-only lifecycle terminology never reach users.

## Out of scope

- authenticated dashboards or product screens;
- Opportunities, Evidence, Briefs, planning, inventory, proposals or bookings;
- live Cognito or other provider resources;
- emails, notifications to external recipients or commercial side effects;
- live/paid AI;
- production topology, deployment or production data;
- Toastr rendering, charts, maps and application animation until Gate 3 screens exist.

## Smallest implementation sequence

1. Implement Tenant, User and Membership plus identity/session ports.
2. Add tenant-safe migration roles, policies, audit/outbox and idempotency storage.
3. Implement ClientAccount, Agency and Contact commands.
4. Expose the minimal identity/workspace and tenant-scoped API contracts.
5. Apply Zod only to browser-consumed contracts.
6. Retain evidence and request the Gate 2 owner decision.

Each step must remain coherent and reversible. Do not build generic repositories, speculative frameworks or future-gate entities.

## Acceptance evidence

| Risk | Minimum evidence |
|---|---|
| Authentication | Unauthenticated, expired, invalid-CSRF and invalid-service-identity denial |
| Tenancy | Parameterised cross-tenant read/write/enumeration/association denial |
| Commands | Duplicate idempotency key returns one result; changed payload conflicts |
| Concurrency | Stale If-Match conflicts without partial consequences |
| Persistence | Empty apply, representative upgrade and rollback/compensating recovery |
| Audit/outbox | State, audit and outbox commit atomically with matching tenant/correlation |
| API | Versioned OpenAPI and safe ProblemDetails contract |
| Browser boundary | Representative malformed consumed payload rejected by Zod |
| Architecture | Dependency, line, complexity, magic-code and provider-boundary rules remain green |

## Test discipline

Tests exist only to prove the table above, a domain invariant or an observed regression.

- Parameterise equivalent entity/status cases.
- Prefer one policy-family integration test over identical per-entity copies.
- Do not test EF Core, ASP.NET Core, PostgreSQL, Zod or notification-library internals.
- Do not create test-count targets, duplicate fixtures or broad snapshot suites.
- Add authenticated Playwright only when Gate 3 creates a real user journey.
- No live provider, external send or production resource may be used.

## Completion boundary

Gate 2 is complete only when code, migrations, contracts and retained evidence agree and Brian Rabuthu records a dated GO. A local implementation does not approve itself.

Brian Rabuthu directed progression to Gate 3 on 2026-08-29. The retained Gate 2 evidence
therefore records local GO; remote publication and production reviews remain separate.
