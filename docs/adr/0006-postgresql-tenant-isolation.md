# ADR-0006: PostgreSQL tenant isolation with defence in depth

## Status

Accepted for local non-production Gate 2 implementation — Brian Rabuthu, 2026-08-29. Remote publication, production and deployment remain prohibited.

## Context

Every protected operation is tenant-scoped. Application-only filters are vulnerable to omitted predicates, incorrect joins and background-process mistakes. PostgreSQL must prevent cross-tenant associations while the C# API remains the primary authorisation owner.

## Decision owner and reviewers

| Responsibility | Actual name | Decision/date |
|---|---|---|
| Accountable owner | Brian Rabuthu | Accepted, 2026-08-29 |
| Engineering/data reviewer | Not required for local-only implementation | Independent review before publication |
| Security/privacy reviewer | Not required for local-only implementation | Independent review before publication/production |
| Operations/recovery reviewer | Not required for local-only implementation | Independent review before deployment |

Brian Rabuthu is the sole required reviewer for this reversible local-only decision. Independent reviews remain mandatory before publication, production or deployment.

## Options considered

1. EF query filters only: rejected as the security boundary because they can be bypassed by raw queries or incorrect joins.
2. One database/schema per tenant: strong separation but excessive operational cost and migration complexity for the current product.
3. Shared schema with direct tenant keys, composite constraints and PostgreSQL row-level security: selected for layered isolation and manageable operations.

## Proposed decision

- Every protected business row carries `tenant_id` directly unless an accepted exception proves database-enforced ownership through a tenant-safe parent.
- Tenant-owned identities use typed IDs. Cross-tenant foreign keys use composite constraints containing `tenant_id`.
- Each request/command resolves one active tenant membership before protected data access. Route tenant IDs are untrusted inputs.
- High-risk tenant tables enable and force PostgreSQL row-level security. Policies use both `USING` and `WITH CHECK`.
- The normal application role is not a superuser, table owner or `BYPASSRLS` role.
- A transaction sets the validated tenant context with transaction-local PostgreSQL configuration before protected queries. Connection pooling cannot retain tenant context across transactions.
- EF query filters may improve safety and readability but are not treated as the database security boundary.
- Platform administrators still operate against one explicit tenant per normal command. Any future cross-tenant reporting or support operation requires a separate audited contract and accepted ADR.
- Global records are explicitly classified. User identity and governed master data do not silently become tenant-owned or tenant-visible.
- Object keys, audit queries, jobs and agent tool calls carry tenant context and are independently authorised.

## Consequences

The model requires composite keys, transaction discipline, migration-owned policies and separate migration/application database roles. It materially reduces the blast radius of an omitted application predicate.

## Implementation boundary

Gate 2 applies this design to Tenant, Membership, ClientAccount, Agency, Contact, audit, outbox and idempotency records. Later aggregates inherit the same policy.

## Verification

Use the smallest parameterised evidence set that proves:

- cross-tenant read, write and enumeration denial;
- cross-tenant foreign-key association rejection;
- missing tenant context default denial;
- pooled connections do not retain a previous tenant;
- the application role cannot bypass row-level security;
- same-tenant authorised commands still work.

Do not duplicate identical tests for every entity unless a different storage path creates a distinct risk.

## References

- Normative sections 18.1–18.2, 20 and 21.1
- [PostgreSQL row security policies](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- [PostgreSQL CREATE POLICY](https://www.postgresql.org/docs/current/sql-createpolicy.html)
- `docs/adr/0002-no-autonomous-spend-or-publication.md`

## Decision record

- Proposed by: implementation agent for Brian Rabuthu
- Proposed date: 2026-08-29
- Accepted/rejected by: Brian Rabuthu
- Decision date: 2026-08-29
- Supersedes/superseded by: none
