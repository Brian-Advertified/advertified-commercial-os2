# Database and object recovery runbook

## Status and scope

This runbook contains a verified local recovery exercise and an unverified staging/production
procedure; it grants no production authority. The repository currently proves a PostgreSQL 16
custom backup and isolated database restore plus a representative inventory-object backup and
restore between separate, private, versioned local MinIO stores. It does not yet prove managed
point-in-time recovery, managed S3 recovery across every object family, the 15-minute RPO, the
four-hour RTO, or a production change window.

Production execution remains blocked until the repository owner names the recovery commander,
database operator, security/privacy contact, application validator and change approver; approves the
backup retention and encryption policy; and identifies the exact managed services and accounts.

## Trigger and authority

Start recovery only for a declared database integrity or availability incident, an authorised restore
exercise, or an authorised release rollback. Record the incident/exercise identifier, requester,
authoriser, environment, suspected failure time, requested recovery point and affected tenant scope.

The recovery commander must approve the selected recovery point and target environment. A database
operator may perform the restore. An application validator who did not execute the restore must
confirm the restored state. Production traffic must not be directed to a target merely because the
restore command succeeded.

## Preconditions

- Fail writes closed or isolate the affected writer before selecting a recovery point.
- Preserve database, application, worker, outbox, object-store and deployment telemetry.
- Confirm that the restore target is isolated and cannot send messages, book inventory, publish,
  spend, invoice or call live AI/providers.
- Inventory the expected database migration, application commit, master-data registry version,
  tenant count, critical aggregate counts, oldest pending outbox item and protected object keys.
- Confirm required PostgreSQL extensions and least-privilege roles exist on the isolated target.
- Confirm the database backup and object-store recovery point are compatible. Database references
  without corresponding object bytes are incomplete recovery.
- Do not place production credentials, backup bytes or personal information in repository evidence.

Stop if the backup provenance, encryption key, selected recovery point, target isolation or operator
authority is unknown. Preserve the evidence and request the smallest missing owner decision.

## Local recovery exercise

From `api/`, run:

```powershell
dotnet test tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
  --configuration Release `
  --artifacts-path ../.artifacts/gate12-recovery-test `
  --filter "Category=Recovery" `
  --logger "console;verbosity=minimal"
```

The test must create two disposable PostgreSQL 16 instances. It migrates and seeds the source,
creates a real custom-format `pg_dump`, transfers that archive, and runs `pg_restore` against the
separate target. Replaying seed commands against the target is not a recovery pass.

The same test must create distinct digest-pinned MinIO source and target containers with private,
versioned buckets. It writes deterministic bytes through the application object-store adapter,
retains the exact source version in a validated backup envelope, proves corrupt or missing bytes do
not write to the target, stops the source, and restores into the initially empty target. The pass
then reconciles the restored bytes, SHA-256, size and media type with the restored database record;
checks a new target provider version and source-version provenance metadata; re-scans the bytes; and
proves anonymous reads remain denied. This is representative local S3-compatible evidence, not a
managed-service backup implementation or a complete production recovery exercise.

## Managed recovery sequence

1. Record the last known-good application commit, migration and object-store checkpoint. Freeze
   rollout and asynchronous workers that can mutate the affected scope.
2. Select the recovery point within the approved retention policy. Record why it precedes the first
   suspected corrupt or unavailable write.
3. Restore into a new isolated target. Do not overwrite the failed database in place.
4. Restore/version the matching object bytes into an isolated bucket or prefix with all outbound
   notifications and provider integrations disabled.
5. Apply only the documented forward-compatible application/migration combination. Do not edit
   migration history or force schema state to look current.
6. Run the verification matrix below. Record actual counts and identifiers, not screenshots alone.
7. Reconcile the recovery gap from immutable source evidence. Human approval remains mandatory for
   any external communication, booking, payment, invoice, publication or commercial replay.
8. Obtain independent application validation and security/privacy review for the affected scope.
9. The named change approver may authorise a controlled traffic switch. Observe health, errors,
   queue age and outbox processing; retain a switch-back point.
10. Close only after the old writer is fenced, delayed work is reconciled, evidence is retained and
    follow-up actions have named owners and dates.

## Verification matrix

| Area | Required check | Failure action |
|---|---|---|
| Backup | Archive is non-empty, restorable and from the selected source/recovery point | Stop; select a valid backup |
| Schema | Expected migration history exists; no migration was fabricated or skipped | Stop writes; resolve application/schema compatibility |
| Governance | Expected master-data registry version and immutable codes remain present | Stop; do not rewrite governed identities |
| Authentication | Restored deterministic or approved test identity resolves through the normal authentication/membership path | Keep traffic isolated |
| Authorisation | Expected role permissions resolve; unrelated-tenant lookup is denied | Treat as a security incident |
| Tenant safety | Every canonical table has the expected RLS posture and negative tenant probes return no rows | Treat as a security incident |
| Canonical state | Pre-recorded tenant and aggregate counts match the selected recovery point | Investigate the delta before replay |
| Outbox | Pending identities, payload hashes, ordering and publication state are retained | Keep dispatch disabled; reconcile duplicates/gaps |
| Object references | Protected keys and content hashes match database records | Keep affected artefacts unavailable |
| Object bytes | Restored bytes exist, hash correctly and are malware-safe in the isolated store | Restore matching version or mark blocked/unknown |
| Audit | Actor, tenant, correlation, causation and immutable version evidence remains queryable | Keep traffic isolated and preserve logs |

## Reconciliation and replay limits

- Never mark an outbox item published merely to reduce a backlog. Compare provider receipts and
  idempotency keys before any replay.
- Never recreate missing canonical facts from AI output, dashboards or memory. Use retained source
  artefacts, verified provider receipts and authorised human decisions.
- Never replay an external side effect until its original outcome is known. Unknown means blocked.
- Forward-only migrations are not rolled back by deleting history or restoring an old image over a
  newer schema. Use an approved compensating migration or restore a compatible isolated stack.
- Missing object bytes are not repaired by changing the stored key/hash. Restore the matching object
  version or keep the reference unavailable.
- Record every manual correction with actor, tenant, reason, before/after value and evidence source.

## Evidence to retain

Retain the authorised recovery point, source/target identifiers, backup metadata and checksums,
application commit and image digests, migration/master-data versions, command logs, start/end times,
verification counts, tenant-negative results, outbox reconciliation, object hashes, approvals,
traffic-switch decision and all deviations. Redact secrets and personal data.

For the local exercise, retain the exact test command and pass/fail output in the Gate 12 evidence
pack. Disposable container IDs and generated archives are transient evidence and must remain outside
Git under `.artifacts/`.

## Decisions required before staging or production

- Named recovery commander, operators, independent validator and security/privacy contact.
- Managed PostgreSQL backup/PITR configuration, tested retention and encryption-key ownership.
- S3 versioning/backup, retention, malware revalidation and object reconciliation procedure.
- Environment-specific RPO/RTO measurement method, alert thresholds and exercise calendar.
- Approved change window, traffic-switch authority and externally coordinated notification path.
