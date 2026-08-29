# Advertified capability ledger

**Evidence date:** 2026-08-29  
**Clean parent recorded before remediation:** `0986f62ad0748289fdafe8f36f5f9a3dabaab4d8`  
**Current change state:** Gates 2–3 committed locally as `115d500`; Gate 4 delivered and verified locally
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
| GitHub CI result | BLOCKED | Latest local commit `115d500`; no usable `origin`; publication not authorised |
| Product journeys | ABSENT | Correctly excluded from Gate 0 |

**Local Gate 0 evidence:** PASS; Gate 1 work is authorised.  
**Merge/release:** NO-GO until owner diff review and remote CI.

## Gate 1 — architecture guardrails

| Capability | Status | Evidence / blocker |
|---|---|---|
| C# dependency direction | VERIFIED | Domain → none; Application → Domain; Infrastructure → Application/Domain; API → Application/Infrastructure |
| C# analyzers and complexity | VERIFIED | .NET 10/C# 14 analyzers; Release API and migration-runner builds report 0 warnings/errors |
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
| Governed master-data registry | VERIFIED | Single embedded JSON source; 14 coherent collections |
| PostgreSQL master-data migration | VERIFIED | Throwaway PostgreSQL 16 apply/bootstrap/reapply/protection/rollback test |
| Stable master-data codes and audit | VERIFIED | Database rejects code change/delete and records item history |
| Closed eleven-agent roster | VERIFIED | Exact code-controlled roster test; no agent is claimed implemented |
| Versioned invocation/output contracts | VERIFIED | Strict typed Pydantic v1 envelopes and exact resource versions |
| Deterministic provider | VERIFIED | Exact fixture or safe failure; no fallback, live call, or cost |
| Agent evaluation fixture format | VERIFIED | Evidence/assumption/unknown classification and tool/cost boundaries validated |
| ADR ownership process | IMPLEMENTED | Accepted status requires actual names and ISO decision date |
| Evidence manifest/report format | IMPLEMENTED | Versioned schema and templates; Gate 1 report pending owner review |
| Accepted ADR and Gate 1 owner decision | BLOCKED | Brian Rabuthu is the accountable owner; final GO awaits remote CI and evidence review |
| Remote CI | BLOCKED | Local commit exists; publication and CI are owner-deferred, not passed |

**Local executed evidence:** web lint/type-check/2 tests/build pass; API build and 18 tests pass; agent runtime 9 tests pass; architecture 20 tests pass.  
**Gate 1 decision:** PENDING. An AI cannot close the gate.

## Gate 2 — canonical commercial foundation

The approved local work packet is implemented and verified locally. Brian Rabuthu recorded
the dated local Gate 2 completion GO on 2026-08-29. Publication, independent review and
production readiness remain separate blocked decisions.

| Capability | Status | Evidence / blocker |
|---|---|---|
| .NET 10/C# 14 API baseline | VERIFIED | ADR-0009; all C# projects target `net10.0`; Release builds and complete C# suite pass |
| Six canonical aggregate models | IMPLEMENTED | Tenant, User, Membership, ClientAccount, Agency and Contact domain/persistence models |
| Canonical commercial migration | VERIFIED | Disposable PostgreSQL empty apply, repeat apply, master-data bootstrap, protection and rollback pass |
| Tenant constraints and forced RLS | VERIFIED | Disposable PostgreSQL cross-tenant read/write/association negatives pass under the application role |
| Idempotency, audit and outbox consequences | VERIFIED | Persisted duplicate/conflict and atomic consequence acceptance tests pass |
| Deterministic development identity | VERIFIED | Production fails closed; expired/invalid identities deny; service identities cannot use interactive endpoints |
| Human-safe error contract | VERIFIED | Typed ProblemDetails code/correlation contract and production authentication denial pass |
| Dedicated migration runner | VERIFIED | Requires explicit apply and migration-only connection; validates least-privilege effective role; disposable apply/reapply passes |
| Versioned OpenAPI v1 | VERIFIED | Retained contract matches the running API contract semantically |
| DB-backed membership reads | VERIFIED | Application-role workspace and membership reads run with transaction-local user/tenant context; cross-tenant resolution returns no membership |
| Role-to-permission resolution | VERIFIED | Accepted 13-code Gate 2 registry, exact conservative role mappings, database-backed resolution and service-role denial |
| Aggregate commands and queries | VERIFIED | Tenant/User updates, ClientAccount/Agency/Contact creates, and tenant-scoped reads for all six aggregates pass through authorization, forced RLS and retained HTTP contracts |
| HTTP concurrency and retry contract | VERIFIED | Strong ETags, required `If-Match`, tenant-scoped `Idempotency-Key`, correlation, replay indication, changed-payload and stale-version conflicts pass end to end |
| Cursor query contract | VERIFIED | Opaque cursor and deterministic tenant-scoped sort return distinct pages in the API acceptance journey |
| Main local database migration | VERIFIED | Owner-authorised exact target applied through the dedicated runner; both migrations record EF Core 10.0.11; post-apply RLS/owner/privilege checks pass |
| Gate 2 completion decision | VERIFIED | Brian Rabuthu directed progression to Gate 3, recording local Gate 2 GO on 2026-08-29; publication and production reviews remain pending |

**Local executed evidence:** architecture 20 passed; .NET 10 Release API and migrator
builds pass with 0 warnings/errors; complete C# suite 29 passed; disposable database
migration and rollback pass. See `docs/evidence/gate-2/`.

## Gate 3 — authenticated application shell

| Capability | Status | Evidence / blocker |
|---|---|---|
| Gate 3 work packet | VERIFIED | Brian Rabuthu approved the exact bounded packet on 2026-08-29 |
| Gate 3 implementation authority | VERIFIED | Brian Rabuthu approved the exact local-only packet and dependencies on 2026-08-29 |
| Provider-neutral local browser session | VERIFIED | Opaque HttpOnly cookie, hashed process-local lookup, expiry, logout invalidation and production fail-closed tests |
| Browser request protection | VERIFIED | Antiforgery and same-origin denial for unsafe cookie-authenticated requests |
| Authenticated product shell | VERIFIED | Sign-in, real workspace selection, tenant-correct home and profile routes pass desktop/compact journeys |
| Browser contract validation | VERIFIED | Zod validates API, form and session-storage boundaries; malformed payloads fail closed |
| Human-safe notification boundary | VERIFIED | Stable codes map to safe wording; only NotificationService infrastructure imports React-Toastify |
| Real tasks, notifications and unsupported KPIs | ABSENT | Destinations remain disabled/truthful because no owning persisted workflow exists |
| Gate 3 completion decision | VERIFIED | Brian Rabuthu recorded local Gate 3 GO on 2026-08-29; publication and production reviews remain pending |

## Gate 4 — evidence and opportunity

| Capability | Status | Evidence / blocker |
|---|---|---|
| Canonical Opportunity and evidence records | VERIFIED | Expand-only migration, forced tenant RLS, immutable submitted artefacts and disposable PostgreSQL tests |
| Human-separated evidence and strategy approvals | VERIFIED | Assigned reviewer/approver flow and creator self-review denial in the Gate 4 acceptance journey |
| Deterministic four-agent sequence | VERIFIED | Strict Python contracts and 15 tests; C# validates and persists only evidence-bound zero-cost outputs |
| Durable run execution | VERIFIED | Persisted runs/steps/usage, leases, checkpoints, duplicate active-run denial and safe retry/recovery states |
| Opportunity lifecycle | VERIFIED | Multi-role journey reaches `BRIEF_READY` only after approved evidence, confirmed interpretation, selected angle, resolved objection and strategy approval |
| Gate 4 API/OpenAPI | VERIFIED | Versioned retained contract covers Opportunity, evidence, artefact, run and human-task query/command surfaces |
| Authenticated Gate 4 web journey | VERIFIED | Opportunity/list/detail, Strategy, Run and Task routes; desktop and compact Playwright flows pass |
| Live provider/network/commercial action | ABSENT | Deterministic fixtures only; zero AI cost, no crawl, publication, spend or external communication |
| Main local database migration | ABSENT | Migration `202608290003_EvidenceOpportunity` was verified only in disposable PostgreSQL and was not applied to the shared local database |
| Gate 4 completion direction | VERIFIED | Brian Rabuthu directed Gate 4 delivered on 2026-08-29; production and publication reviews remain pending |

See `docs/evidence/gate-4/` for commands and exact outcomes.

## Gates 5–13

Gate 5 is next under the standing sequential local-delivery direction, but implementation has
not started and its exact work packet has not been recorded. Gates 6–13 remain ABSENT and
sequence-blocked. A document, route label, container, contract, or scaffold is not a product
implementation.

| Gate | Status |
|---|---|
| 4 Evidence and Opportunity | VERIFIED locally — owner directed delivered; non-local review pending |
| 5 Canonical Brief | ABSENT — next packet not yet recorded |
| 6 Inventory truth | BLOCKED |
| 7 Planning | BLOCKED |
| 8 Proposal and client decision | BLOCKED |
| 9 Rapid OOH | BLOCKED |
| 10 Supplier marketplace | BLOCKED |
| 11 Campaign delivery and learning | BLOCKED |
| 12 Hardening and certification | BLOCKED |
| 13 Production launch | BLOCKED |

Local evidence is not remote CI evidence. No tests discovered is not a pass. A proposed ADR is not accepted. An AI cannot approve legal compliance, security, a gate, or production readiness.
