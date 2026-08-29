# Development entry gate

**Current decision:** GATE 3 GO — GATE 4 IMPLEMENTATION NO-GO
**Evidence date:** 2026-08-29  
**Remote merge/deploy decision:** NO-GO

This is the operational handoff for a developer or implementation agent.

## Gate 0 evidence

| Check | State | Evidence |
|---|---|---|
| Repository identity | PASS | os2 path, `master`, clean parent observed before remediation |
| Web | PASS | locked install; lint/type-check; 2 tests; production build |
| Commercial API | PASS | Release build; 2 real liveness/readiness tests |
| Agent runtime | PASS | 3 tests; no DB/provider SDK; provider disabled; zero agents claimed |
| Architecture | PASS | 10 real assertions |
| PostgreSQL | PASS | custom PostgreSQL 16 image; health requires PostGIS and pgvector |
| Local services | PASS | four os2 Compose services healthy on loopback-only ports |
| CI definition | PASS | real jobs; correct branch; no placeholder success |
| Remote CI run | PENDING | requires an owner-authorised commit/push |
| Secrets and cost | PASS | no committed credential added; live provider false; cost zero |
| Documentation | PASS | complete split v1.1 spec and truthful ledger |
| Diff review | PENDING | complete after final owner review |

A pending remote CI or owner diff review blocks merge, not local Gate 1 work.

## Authorised Gate 1 work packet

Outcome: make architectural bypasses mechanically difficult before any domain feature starts.

In scope:

1. tenant context and deny-by-default authorisation contracts;
2. cross-tenant negative test harness;
3. command envelope, idempotency, optimistic concurrency, audit, and outbox contracts;
4. aggregate-specific state-machine representation;
5. PostgreSQL master/reference table model and safe migration tests;
6. deterministic agent-provider interface with live adapters absent or disabled;
7. versioned invocation/output envelopes and evaluation fixture format;
8. static dependency/analyser enforcement for C#, Python, and TypeScript;
9. ADR ownership and approval process;
10. evidence manifest and gate completion template.

Out of scope:

- product screens and journeys;
- production authentication-provider integration;
- business aggregates beyond contract skeletons;
- live AI/provider SDKs or calls;
- inventory ingestion, planning, proposals, booking, payment, email, or maps;
- cloud mutation, deployment, production data, or legacy code reuse.

## Gate 1 acceptance evidence

- tests deliberately demonstrate tenant/permission violations are denied;
- duplicate command fixture proves one canonical outcome;
- audit/outbox contract proves consequence and event correlation;
- master-data migration proves stable codes and no inline alternatives;
- deterministic provider contract proves zero live calls and bounded invocation;
- architecture checks fail against controlled violating fixtures;
- all three builds and test suites remain green;
- exact diff and evidence report reviewed by the named owner.

## Current Gate 1 evidence

| Check | State | Observed result |
|---|---|---|
| C# Release build | PASS | 0 warnings/errors |
| C# API/governance tests | PASS | 18 passed |
| PostgreSQL master-data migration | PASS | Throwaway PostgreSQL 16 apply/reapply/protection/history/rollback |
| React lint/type-check/tests/build | PASS | 0 lint warnings/errors; 2 tests; build successful |
| Agent runtime tests | PASS | 9 deterministic tests |
| Architecture guardrails | PASS | 20 tests including evidence-schema integrity and controlled violations |
| Python Ruff | PENDING | Enforced in CI; current local rerun still required |
| Remote CI | DEFERRED | Local commit `6f75c7e`; owner deferred publication after no usable `origin` was found |
| Named owner diff review | PENDING | Brian Rabuthu named; final review follows remote CI |

Brian Rabuthu accepted ADR-0005 through ADR-0008 and the exact Gate 2 work packet for local non-production implementation on 2026-08-29. Pending Ruff and remote CI remain truthful blockers for publication; they do not authorise merge, release or deployment.

## Deferred evidence and non-local blockers

- Python Ruff and remote CI remain pending while branch publication is deferred;
- no usable Git remote is configured for local commit `6f75c7e`;
- independent Engineering, Security/Privacy and Operations review is mandatory before publication, production or deployment;
- legal/privacy and security documents remain drafts, not production approvals;
- migration `202608290002_CanonicalCommercialFoundation` was explicitly authorised and applied only to the scoped os2 local database; future migrations require separate approval.

Gate 2 received local GO from Brian Rabuthu on 2026-08-29. Brian then approved the exact
local-only scope in `docs/GATE3_WORK_PACKET.md` by directing implementation to continue.
That packet is now implemented and locally verified. Brian recorded Gate 3 GO on
2026-08-29. Do not begin Gate 4 until he separately approves an exact Gate 4 work packet.

## Current Gate 2 implementation evidence

| Check | State | Observed result |
|---|---|---|
| .NET 10/C# 14 retarget | PASS | All C# projects target `net10.0`; explicit C# 14; accepted ADR-0009 |
| Release builds | PASS | Commercial API and dedicated migration runner build with 0 warnings/errors |
| Complete C# suite | PASS | 29 tests on .NET 10 |
| Architecture guardrails | PASS | 20 tests |
| Disposable migration | PASS | Empty apply, repeat apply/bootstrap, protection and rollback using least-privilege effective role |
| Tenant isolation | PASS | Forced RLS and cross-tenant read/write/association denial verified in disposable PostgreSQL |
| API boundary | PASS | Six-aggregate routes, ETag/If-Match, idempotency, correlation, cursor, typed safe errors and retained OpenAPI v1 contract |
| Local identity boundary | PASS | Production startup, expired/invalid identity and interactive service-identity denial verified; no cookie-authenticated browser endpoint exists in Gate 2 |
| Main local database | PASS | Exact owner-authorised migration applied through the runner; migration history, owners, forced RLS and privileges verified |
| Role-permission mapping | PASS | 13 governed Gate 2 permissions; exact conservative mappings; database-backed resolution; no service-role permissions |
| Remote CI and independent review | PENDING | No commit, push, merge, release or deployment authorised or performed |

The retained report and machine-readable manifest are in `docs/evidence/gate-2/`.
Brian Rabuthu directed progression to Gate 3 on 2026-08-29, recording local Gate 2 GO.
That decision did not authorise publication, production or deployment; the separately
approved Gate 3 packet authorised only the local implementation recorded below.

## Current Gate 3 implementation evidence

| Check | State | Observed result |
|---|---|---|
| Commercial API and migration runner Release builds | PASS | .NET 10/C# 14; 0 warnings/errors |
| Complete C# suite | PASS | 32 tests, including real disposable-PostgreSQL browser-session journey |
| Architecture guardrails | PASS | 21 tests |
| Web lint, type-check, tests and build | PASS | 0 lint/type findings; 4 focused tests; Vite build |
| Browser acceptance | PASS | 4 Playwright cases across desktop and compact reduced-motion viewports |
| Web runtime audit | PASS | 0 vulnerabilities |
| Live provider, production resource or database migration | NOT USED | Local deterministic fixtures/session only; no schema change |
| Remote CI and independent review | PENDING | No commit, push, merge, release or deployment authorised or performed |

The retained report and manifest are in `docs/evidence/gate-3/`. Brian Rabuthu recorded the
dated local Gate 3 completion GO on 2026-08-29. This does not authorise Gate 4,
publication, production or deployment.
