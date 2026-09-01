# Gate 12 object-byte recovery work packet

**Owner direction:** 2026-08-31, continue toward a production state after the locally green
Campaign Delivery and release-stabilisation pass.

**Verified predecessor:** Gate 11 and the connected browser journey are verified locally. The
Release API suite passed 87/87, including a real PostgreSQL 16 custom backup restored into a
separate isolated database. That recovery proof retains an object key and SHA-256 reference but
does not restore the referenced bytes. Gate 12 remains in progress.

**Authority:** Local non-production implementation and deterministic verification only. No cloud
resource, production data, live provider, external communication, deployment, commit or push.

## Bounded requirement

Complete the locally provable object-byte portion of E2E-12 for the canonical S3-compatible
inventory object boundary. Back up a deterministic protected object from a real isolated MinIO
source and restore it into a distinct empty MinIO target that is reconciled with the separately
restored PostgreSQL reference. The source must no longer be available when target validation runs.

## Acceptance evidence

1. Source and target use separate disposable MinIO containers pinned to the reviewed image digest,
   separate private buckets and local-only credentials. Both buckets have versioning enabled.
2. The source object is written and read through `MinioInventoryObjectStore`. Its database key,
   SHA-256, byte size and media type match a deterministic synthetic fixture.
3. The offline backup envelope retains only the object key, SHA-256, byte size, media type and
   version identifier plus the exact bytes. It validates every field before restore.
4. The source container is stopped before the backup is restored to the initially empty target.
   Anonymous HTTP access to source and restored object paths is denied.
5. The restored target bytes, SHA-256, size, media type and non-empty version identifier match the
   database reference and backup envelope exactly. A deterministic malware re-scan is clean.
6. Missing or corrupted backup bytes fail closed before a target write. Reusing an immutable object
   key with different bytes also fails closed, while an identical retry is idempotent. No database
   reference is changed to conceal missing or mismatched bytes.
7. Non-local production startup rejects an S3-compatible object-store configuration that does not
   require TLS. Local MinIO remains explicitly non-production.
8. The focused recovery tests, complete Release API suite, architecture checks, non-ignored source
   secret scan and Compose validation pass. CI builds the required custom PostgreSQL test image
   before the API suite. Valid-shaped test credentials are generated from explicit local fixture
   material rather than retained as source literals.
9. Recovery documentation states exactly what is locally verified and retains the remaining owner,
   managed-service, retention, encryption, PITR, RPO/RTO and production-exercise blockers.

## Verification status

Implemented and verified locally on 2026-08-31. The focused recovery test passed 1/1, the production
TLS guard passed 1/1, the complete Release API suite passed 88/88, the Release API build completed
with zero warnings and errors, the architecture suite passed 23/23, the custom PostgreSQL dependency
image rebuilt, and all six Compose services became healthy. Detailed reproducible evidence is in
`docs/evidence/gate12-object-recovery-20260831/`.

This is Gate 12 progress, not Gate 12 acceptance or production approval. Remote CI, managed-service
recovery, retention/encryption policy, RPO/RTO measurement, staging, independent review and owner
decisions remain unresolved.

## Explicitly out of scope

- AWS account, S3 bucket, RDS/PITR, KMS, IAM role, production credential or production mutation;
- selecting production version-retention, encryption, lifecycle, replication or deletion policy;
- claiming every future object family is covered by one representative inventory-object exercise;
- production traffic switching, provider calls, notification replay or external side effects;
- declaring Gate 12, security/privacy review or production readiness approved.
