# Gate 12 isolated recovery work packet

**Owner direction:** continue local hardening and certification preparation after committed Gate 11
measurement delivery.

**Verified predecessor:** local commit `8293aac`; Release Commercial API 70/70, deterministic runtime
28/28, registry 2.9.0, migration 024 apply/rollback, 80-table forced RLS, retained OpenAPI, focused
formatting and Compose validation pass. No push, deployment, production resource or live provider was
used. The separately owned screen work still blocks two repository architecture checks.

## Bounded requirement

Implement and exercise the local E2E-12 database recovery proof from Sections 27.4 and 28.2. Use a
real PostgreSQL 16 custom-format backup and restore into a separately created isolated database. The
drill must verify that restored canonical state remains usable and tenant-safe; copying a fixture file
or replaying application seed commands into the target is not a restore.

## Acceptance evidence

1. A fully migrated source database with governed master data contains one active deterministic local
   identity/membership, canonical commercial records, one pending outbox message and one protected
   object reference with a retained hash.
2. `pg_dump` creates a non-empty custom-format archive; `pg_restore` restores that archive into a
   separate empty PostgreSQL 16 target whose required extensions and least-privilege roles already
   exist.
3. The target preserves migration history, master-data registry version, exact canonical row counts,
   pending outbox identity/payload, object key/hash, and all 80 forced-RLS tables.
4. The restored deterministic identity authenticates through the normal HTTP boundary, sees exactly
   its restored workspace, resolves its governed permissions through the database membership source,
   and is denied access to an unrelated tenant.
5. The test is deterministic, CI-runnable, leaves containers disposable, uses no production data or
   provider, and records no plaintext production credential.
6. Recovery and incident runbooks identify prerequisites, evidence to retain, fail-closed steps,
   reconciliation, rollback/compensation limits, and the owner decisions still required before staging
   or production use.

## Explicitly out of scope

- AWS/RDS point-in-time recovery, S3 object-byte restoration, staging or production deployment;
- selecting backup encryption keys, production retention, incident owners or change windows;
- DNS/TLS, ECS/Fargate, CloudFront, ALB, EventBridge/SQS or cloud resource mutation;
- declaring the 15-minute RPO, four-hour RTO, Gate 12 or production greenlight achieved;
- editing or testing the separately owned screen implementation.
