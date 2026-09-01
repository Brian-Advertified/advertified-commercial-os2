# Gate 12 local object-recovery evidence

**Evidence date:** 2026-08-31

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this packet does not claim a release artefact

**Live provider or production resource used:** No

## Implemented

- Extended the real PostgreSQL 16 recovery acceptance test with separate digest-pinned MinIO source
  and target containers. Both object stores use private, versioned buckets and local-only fixtures.
- Wrote deterministic inventory bytes through `MinioInventoryObjectStore`, retained an exact source
  version in a validated backup envelope, stopped the source, and restored into the empty target.
- Reconciled restored bytes, SHA-256, size and media type with the separately restored database
  record. The test verifies a distinct target version, source-version provenance metadata, a clean
  malware re-scan and denied anonymous reads.
- Added negative proof for missing/corrupt backup bytes, changed-content immutable-key reuse and
  idempotent identical retries. Invalid backup bytes do not create a target object.
- Hardened the MinIO adapter with verified writes and immutable-key retries, made non-local MinIO
  configuration fail closed without TLS, pinned the Compose MinIO image digest, and made CI build
  the required custom PostgreSQL integration image before API tests.
- Removed a valid-shaped webhook credential literal found in a deterministic test fixture. The same
  fixture value is now assembled at runtime from explicit local-only test material.

## Verification

| Check | Result |
|---|---|
| Focused local database and object recovery | PASS - 1/1 in 26 seconds |
| Production object-storage TLS guard | PASS - 1/1 in 900 ms |
| Complete final-state Release Commercial API suite | PASS - 88/88 in 2 minutes 36 seconds |
| Release Commercial API build | PASS - zero warnings and zero errors |
| Focused C# formatter checks | PASS - API and test sources unchanged by formatter |
| Complete final-state architecture suite | PASS - 23/23 in 29.82 seconds |
| Compose definition and custom PostgreSQL image | PASS - configuration valid and image rebuilt |
| Local infrastructure health | PASS - PostgreSQL, MinIO, ClamAV, Docling, Redis and MailHog healthy |
| Tracked/non-ignored source secret scan | PASS - zero findings after the test-fixture remediation |
| Webhook test-fixture regression | PASS - corrected exact-method filter passed 1/1 in 22 seconds |
| Runtime smoke checks | PASS - API live/ready and web `/` plus `/sign-in` returned HTTP 200 |

The first unscoped `gitleaks dir .` diagnostic scanned 222 MB, including ignored build/dependency
outputs and local ignored configuration, and returned 601 matches. It is not used as source evidence.
An isolated mirror of `git ls-files -co --exclude-standard` scanned 6 MB and identified one
valid-shaped deterministic test credential. After remediation, the final clean source mirror covered
6.03 MB and returned zero findings. No finding was suppressed or allow-listed.

An initial test command filtered on the source filename `EmailProposalAutomationAcceptanceTests`
rather than the actual partial-class test name. The runner exited zero but reported that no test
matched, so that command is explicitly not counted as evidence. The corrected exact-method filter
matched and passed the webhook automation test 1/1.

## Recovery boundaries verified

1. Database and object targets are distinct from their sources. The object source is stopped before
   restore validation, so the pass cannot read through to the source accidentally.
2. The database record is not changed to conceal absent or mismatched bytes. Backup metadata and
   content must match the canonical key, hash, size and media type before a target write.
3. Provider version identifiers are store-local. The target receives a new provider version while
   the source version is retained only as private recovery provenance metadata.
4. The production adapter rejects changed content for an existing immutable key and verifies bytes,
   size and media type after a write. Managed object lock and cross-instance conditional-write policy
   remain production decisions.
5. No notification, booking, payment, publication, provider call or other external side effect is
   replayed by the exercise.

## Remaining boundary

This is representative local S3-compatible recovery evidence, not Gate 12 acceptance or production
approval. The backup envelope is an in-test representation, not an encrypted durable backup format.
Managed PostgreSQL PITR, managed object-store recovery for every object family, IAM/KMS, retention,
replication, object lock, measured RPO/RTO, staging recovery, remote CI, deployable image/SBOM
evidence, performance, full observability/alarms, on-call ownership and independent
Security/Privacy/Legal/Operations review remain pending. No commit, push or deployment was performed.
