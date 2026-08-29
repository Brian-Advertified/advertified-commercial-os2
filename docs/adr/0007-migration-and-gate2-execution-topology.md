# ADR-0007: Migration ownership and Gate 2 execution topology

## Status

Accepted for local non-production Gate 2 implementation — Brian Rabuthu, 2026-08-29. Remote publication, production and deployment remain prohibited.

## Context

Gate 2 introduces the first canonical commercial tables. Automatic startup migration, privileged application credentials and premature production topology would create avoidable risk. The owner has deferred branch publication and remote CI while allowing local decision preparation.

## Decision owner and reviewers

| Responsibility | Actual name | Decision/date |
|---|---|---|
| Accountable owner | Brian Rabuthu | Accepted, 2026-08-29 |
| Engineering/data reviewer | Not required for local-only implementation | Independent review before publication |
| Security/privacy reviewer | Not required for local-only implementation | Independent review before publication/production |
| Operations/recovery reviewer | Not required for local-only implementation | Independent review before deployment |

Brian Rabuthu is the sole required reviewer for this reversible local-only decision. Independent reviews remain mandatory before publication, production or deployment.

## Proposed decision

- EF Core migrations in the C# Infrastructure project are the canonical schema history.
- The running API and workers never auto-migrate and have no DDL ownership.
- A dedicated migration command/job uses a separate least-privilege migration role after explicit operator approval.
- The normal application role receives only required DML/function privileges and cannot disable row-level security.
- Migrations are expand-first and backward-compatible across one deployment window. Destructive contraction is a later explicit change after all consumers move.
- Each data migration defines forward behavior and either safe rollback or a compensating roll-forward. Irreversible transformations require a backup/restore plan and named approval.
- Local development uses the existing os2 Compose PostgreSQL, Redis, MinIO and MailHog services. Applications may run locally.
- Migration verification uses disposable PostgreSQL 16 databases. CI uses ephemeral infrastructure when publication resumes.
- Gate 2 makes no production cloud topology change. Production runtime, network and managed-service topology are explicitly deferred to hardening/launch ADRs and cannot be guessed from local Compose.
- The main local development database is migrated only after disposable apply/upgrade/recovery evidence passes and the owner authorises that exact migration.

## Consequences

This adds an explicit migration operation but prevents accidental schema mutation during API startup. Production architecture stays undecided without blocking local canonical-domain work.

## Verification

Required evidence is limited to:

- empty-database apply;
- representative prior-version upgrade;
- idempotent governed seed;
- tenant policy and role assertions;
- rollback or compensating recovery;
- one restore rehearsal when a migration can destroy or transform data;
- API startup proving it does not run migrations.

Framework migration mechanics are not redundantly tested.

## References

- Normative sections 18 and 21
- `docs/SETUP_GUIDE.md`
- `docs/evidence/GATE_REPORT_TEMPLATE.md`

## Decision record

- Proposed by: implementation agent for Brian Rabuthu
- Proposed date: 2026-08-29
- Accepted/rejected by: Brian Rabuthu
- Decision date: 2026-08-29
- Supersedes/superseded by: none
