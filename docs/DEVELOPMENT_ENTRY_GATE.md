# Development entry gate

**Current decision:** LOCAL DEVELOPMENT GO — GATE 1 ONLY  
**Evidence date:** 2026-08-29  
**Merge/feature/deploy decision:** NO-GO

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
| Remote CI | PENDING | Commit/push not authorised |
| Named owner diff review | PENDING | Brian Rabuthu named; final review follows remote CI |

The implementation packet is technically complete for local review, but Gate 1 is not approved. Its manifest and report are in `docs/evidence/gate-1/`. Gate 2 remains blocked.

## Blockers before Gate 2

- accountable owner Brian Rabuthu must record the final dated Gate 1 GO after remote CI;
- browser authentication/session/CSRF design is undecided;
- tenancy enforcement mechanism is undecided;
- migration and deployment topology need approved ADRs;
- legal/privacy and security documents are drafts, not approvals;
- Gate 2 work packet has not been approved.

Do not resolve those choices by guessing. Record the decision owner and request the smallest explicit choice.
