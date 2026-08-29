# Gate 2 evidence report

**Evidence date:** 2026-08-29
**Repository/branch:** advertified-commercial-os2 / master
**Base commit:** `6f75c7ea4d86e3f8d9637cad40a764fb886c9513`
**Working tree:** uncommitted review
**Decision:** GO — Brian Rabuthu, 2026-08-29

## Authorised outcome

The owner-approved local non-production packet covers the canonical commercial foundation
in specification sections 5, 18–21 and 25. It permits six canonical aggregate models,
tenant-safe PostgreSQL persistence, deterministic identity boundaries, migration tooling,
and API contracts. Gate 3 screens, live identity/provider resources, production data,
deployment, external actions and later product gates remain out of scope.

## Changes

| Path | Created/changed | Capability | Data impact |
|---|---|---|---|
| `global.json`, `Directory.Build.props`, C# projects | Changed | .NET 10 and C# 14 baseline | None |
| `api/src/Advertified.Commercial.Domain/Commercial/` | Created | Six canonical domain models | Canonical local schema now applied |
| `api/src/Advertified.Commercial.Infrastructure/` | Changed | Persistence, permissions, RLS, audit, outbox and idempotency | Empty local commercial schema and governed seeds |
| `api/src/Advertified.Commercial.DatabaseMigrator/` | Created | Explicit least-privilege migration runner | Applied only to the owner-authorised os2 local target |
| `api/Authentication/`, `api/Endpoints/`, `api/Errors/`, `api/OpenApi/` | Created | Deterministic identity, six-aggregate routes, HTTP command contracts and safe errors | Canonical writes use the application role and command unit of work |
| `api/src/Advertified.Commercial.Application/Foundation/` | Created | Versioned command/query DTOs and explicit ports | None |
| `api/src/Advertified.Commercial.Infrastructure/Foundation/` | Created | Tenant-authorized readers and persisted command handlers | Tenant/User updates and ClientAccount/Agency/Contact creates |
| `shared/contracts/openapi/` | Created | Retained OpenAPI v1 contract | None |
| `api/tests/Advertified.Commercial.Api.Tests/` | Changed | Acceptance evidence | Disposable PostgreSQL databases only |
| `shared/contracts/master-data.json` | Changed | Registry 1.2.0 and exact Gate 2 permission mappings | 13 permissions synchronised locally |
| `docs/adr/0009-dotnet-10-csharp-14-baseline.md` | Created | Owner-directed runtime decision | None |

## Verification

| Check | Exact command | Outcome | Retained evidence |
|---|---|---|---|
| Architecture guardrails | `python -m pytest tests/architecture -q` | PASS — 20 passed | Console result and manifest |
| API Release build | `dotnet build api/Advertified.Commercial.Api.csproj --configuration Release --no-restore` | PASS — 0 warnings/errors | Console result and manifest |
| Migration-runner Release build | `dotnet build api/src/Advertified.Commercial.DatabaseMigrator/Advertified.Commercial.DatabaseMigrator.csproj --configuration Release --no-restore` | PASS — 0 warnings/errors | Console result and manifest |
| Migration-runner safe refusal | `api/src/Advertified.Commercial.DatabaseMigrator/bin/Release/net10.0/Advertified.Commercial.DatabaseMigrator.exe` without arguments | PASS — exit 2 and no database change | Console result and manifest |
| Complete C# suite | `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --configuration Release --no-build --verbosity minimal` | PASS — 29 passed | Console result and manifest |
| Six-aggregate HTTP acceptance | `dotnet test ... --filter FullyQualifiedName~CommercialFoundationApiAcceptanceTests` | PASS — 1 end-to-end policy-family journey | Tenant/User/Membership/ClientAccount/Agency/Contact, cross-tenant denial, ETag/If-Match, idempotency, correlation, cursor, audit and outbox |
| Local identity safety | Human-safe API boundary tests in the complete suite | PASS | Unauthenticated, expired, invalid identity and interactive service-identity requests deny safely; deterministic mode cannot start in Production |
| OpenAPI generation | `dotnet swagger tofile --output shared/contracts/openapi/advertified-commercial-api.v1.json api/bin/Release/net10.0/Advertified.Commercial.Api.dll v1` with documented generation-only environment | PASS | Retained v1 JSON and semantic contract test |
| Compose definition | `docker compose -f infrastructure/docker-compose.yml config --quiet` | PASS | Local command result |
| Main local preflight | Exact-container database/schema/role queries | PASS — PostgreSQL 16.0.15; target `advertified`; schemas and group roles initially absent | Pre-write target evidence |
| Least-privilege role provisioning | Exact local `CREATE ROLE` and minimum `GRANT` statements | PASS | Both group roles NOLOGIN/NOSUPERUSER/NOCREATEROLE/NOCREATEDB/NOINHERIT/NOBYPASSRLS |
| Owner-authorised main migration | Dedicated runner with `--apply` and ignored local credential | PASS — 2 migrations; 14 registry collections | Exact target `advertified-os2-dev-postgres-1` / `advertified` |
| Post-migration security | Read-only migration/registry/owner/RLS/privilege queries | PASS | EF 10.0.11 history; 13 permissions; 9 forced-RLS tables; migrator ownership; missing context returns zero |
| Docker connector schema read | Advertified Docker Local exact-container `docker_exec` with the same read-only query | FAIL — connector JSON decoder rejected a control character | Tooling defect; local Docker CLI supplied the read-only evidence |
| Docker connector command execution | Advertified Docker Local exact-container `docker_exec` using `psql --version` | FAIL — `INVALID_ARGUMENT` | Connector tooling defect; exact-container local Docker CLI used |
| First post-migration SQL check | One PowerShell-native command containing quoted EF history identifiers | FAIL — host quoting removed identifiers | PostgreSQL rejected the query; corrected stdin SQL check passed without mutation |
| Role-to-permission contract | Accepted registry, exact mappings and database-backed resolver | PASS | 13 stable permissions; service roles receive none; cross-tenant resolution denies |
| Remote CI and independent reviews | Owner-authorised publication and named reviews | PENDING | No remote publication authorised |

The complete C# suite includes disposable empty migration apply, repeated apply/bootstrap,
stable-code protection, rollback, main API startup without auto-migration, forced-RLS
cross-tenant negatives, persisted command idempotency/audit/outbox, the six-aggregate HTTP
journey, stale `If-Match`, cursor pagination, production fail-closed development
authentication, expired/invalid identity denial, service-identity separation, safe problems
and retained OpenAPI comparison.

Token antiforgery is intentionally not installed on this Gate 2 surface because the accepted
ADR requires it for unsafe **cookie-authenticated** browser requests and Gate 2 exposes no
cookie-authenticated endpoint. The deterministic Development/Test scheme uses no browser
cookie or provider token. Invalid-CSRF evidence becomes mandatory when the separately gated
cookie session exists. Likewise, the browser does not consume a Gate 2 contract, so Zod is
not yet applicable.

## Safety and boundaries

- Cross-tenant negative result: application-role reads, writes and associations deny under forced RLS.
- Permission-denial result: missing, inactive, cross-tenant and missing-permission cases deny identically; database cross-tenant resolution returns no membership; service roles map to no interactive permissions.
- Command-boundary result: changed payload reusing an idempotency key and stale `If-Match` both return stable safe conflicts without partial consequences; an exact retry returns the canonical result and one replay audit.
- Query-boundary result: all six aggregates have an authorized read path; membership/workspace reads apply the governed role mapping; opaque cursor pages retain deterministic sort order.
- Migration/rollback result: disposable apply/reapply/bootstrap/protection/rollback passes; API startup never migrates; exact local apply passes.
- Live or paid provider used: No.
- Incremental AI cost: 0 minor units.
- Production resource used or changed: No.
- Secrets or production data introduced: No.
- Consequential external action performed: No.
- Main local development database changed: Yes, with exact owner approval; two migrations and governed reference data only.

## Unresolved blockers

1. Remote CI and independent Engineering, Security/Privacy and Operations review remain
   pending until the owner authorises publication.

## Diff and review

- Unrelated user changes preserved: Yes; the inherited uncommitted Gate 2 work was retained.
- Complete diff inspected: Yes locally; Brian Rabuthu directed progression to Gate 3 on 2026-08-29.
- Corrected verification failure: the first final 29-test run had 28 pass and one fail because
  EF could not translate a value-object role filter in `/workspaces`; the query was corrected
  without weakening RLS or permission checks, the targeted journey passed, and the full
  29-test suite then passed.
- Files staged: None.
- Commit/push/deploy performed: None.
- Accountable owner: Brian Rabuthu.
- Required reviewers: independent Engineering, Security/Privacy and Operations before publication/production/deployment.
- Owner decision/date: Gate 2 GO — Brian Rabuthu, 2026-08-29.

An AI prepared this report and did not approve the gate, security, privacy, legal
compliance or production readiness.
