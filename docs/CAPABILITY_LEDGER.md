# Advertified capability ledger

**Evidence date:** 2026-08-29  
**Clean parent recorded before remediation:** `0986f62ad0748289fdafe8f36f5f9a3dabaab4d8`  
**Current change state:** uncommitted owner-review diff  
**Status vocabulary:** ABSENT, SCAFFOLDED, IMPLEMENTED, VERIFIED, BLOCKED

A capability is VERIFIED only when repeatable evidence was observed. Documentation does not make a capability implemented.

## Gate 0 — repository baseline

| Capability | Status | Evidence |
|---|---|---|
| Correct os2 repository and branch | VERIFIED | configured os2 path, `master`; clean at start |
| Binding contributor/AI rules | IMPLEMENTED | `AGENTS.md` |
| Complete normative v1.1 source | VERIFIED | Seven ordered parts; all seven hashes match source chunks |
| React dependency lock | VERIFIED | Exact React 19.2.0 and locked install |
| Commercial API baseline | VERIFIED | Release build and health endpoint tests |
| Agent runtime baseline | VERIFIED | Provider disabled; zero agents claimed |
| PostgreSQL 16 local foundation | VERIFIED | PostGIS and pgvector image plus health checks |
| Redis, MinIO, and MailHog | VERIFIED | Four os2 services healthy |
| CI definition | IMPLEMENTED | Real architecture, web, API, Python, and Compose jobs |
| GitHub CI result | BLOCKED | Requires owner-authorised commit/push |
| Product journeys | ABSENT | Correctly excluded from Gate 0 |

**Local Gate 0 evidence:** PASS; Gate 1 work is authorised.  
**Merge/release:** NO-GO until owner diff review and remote CI.

## Gate 1 — architecture guardrails

| Capability | Status | Evidence / blocker |
|---|---|---|
| C# dependency direction | VERIFIED | Domain → none; Application → Domain; Infrastructure → Application/Domain; API → Application/Infrastructure |
| C# analyzers and complexity | VERIFIED | .NET 8 analyzers; Release build 0 warnings/errors |
| TypeScript complexity/function limits | VERIFIED | Oxlint 0 warnings/errors with complexity 10 and function maximum 60 |
| Python complexity/function limits | IMPLEMENTED | Ruff C90 and 60-line architecture rule; local Ruff rerun remains pending |
| 400-line authored-source rule | VERIFIED | Architecture suite |
| Controlled violating fixtures | VERIFIED | Dependency, DB import, ADR, function, and file-size detectors reject known violations |
| Tenant deny-by-default harness | VERIFIED | Missing, inactive, wrong actor, wrong tenant, and missing permission all deny identically |
| Cross-tenant command containment | VERIFIED | Denial occurs before idempotency lookup or handler execution |
| Command/idempotency contract | VERIFIED | Duplicate fixture returns one canonical outcome; changed payload conflicts |
| Audit/outbox correlation | VERIFIED | Mismatched tenant/command/correlation cannot commit |
| Optimistic concurrency contract | VERIFIED | Expected version is mandatory and outcome advances exactly once |
| Opportunity state-machine skeleton | VERIFIED | Typed canonical transitions and invalid-transition negatives |
| Governed master-data registry | VERIFIED | Single embedded JSON source; 11 coherent collections |
| PostgreSQL master-data migration | VERIFIED | Throwaway PostgreSQL 16 apply/bootstrap/reapply/protection/rollback test |
| Stable master-data codes and audit | VERIFIED | Database rejects code change/delete and records item history |
| Closed eleven-agent roster | VERIFIED | Exact code-controlled roster test; no agent is claimed implemented |
| Versioned invocation/output contracts | VERIFIED | Strict typed Pydantic v1 envelopes and exact resource versions |
| Deterministic provider | VERIFIED | Exact fixture or safe failure; no fallback, live call, or cost |
| Agent evaluation fixture format | VERIFIED | Evidence/assumption/unknown classification and tool/cost boundaries validated |
| ADR ownership process | IMPLEMENTED | Accepted status requires actual names and ISO decision date |
| Evidence manifest/report format | IMPLEMENTED | Versioned schema and templates; Gate 1 report pending owner review |
| Accepted ADR and Gate 1 owner decision | BLOCKED | Brian Rabuthu is the accountable owner; final GO awaits remote CI and evidence review |
| Remote CI | BLOCKED | No commit or push was authorised |

**Local executed evidence:** web lint/type-check/2 tests/build pass; API build and 18 tests pass; agent runtime 9 tests pass; architecture 20 tests pass.  
**Gate 1 decision:** PENDING. An AI cannot close the gate.

## Gates 2–13

All product capabilities remain ABSENT and gate-BLOCKED. A document, route label, container, contract, or scaffold is not a product implementation.

| Gate | Status |
|---|---|
| 2 Canonical commercial foundation | BLOCKED |
| 3 Authenticated application shell | BLOCKED |
| 4 Evidence and Opportunity | BLOCKED |
| 5 Canonical Brief | BLOCKED |
| 6 Inventory truth | BLOCKED |
| 7 Planning | BLOCKED |
| 8 Proposal and client decision | BLOCKED |
| 9 Rapid OOH | BLOCKED |
| 10 Supplier marketplace | BLOCKED |
| 11 Campaign delivery and learning | BLOCKED |
| 12 Hardening and certification | BLOCKED |
| 13 Production launch | BLOCKED |

Local evidence is not remote CI evidence. No tests discovered is not a pass. A proposed ADR is not accepted. An AI cannot approve legal compliance, security, a gate, or production readiness.
