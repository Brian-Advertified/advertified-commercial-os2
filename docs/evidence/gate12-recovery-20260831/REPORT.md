# Gate 12 isolated recovery evidence

**Evidence date:** 2026-08-31

**Base commit:** `8293aac83efd4b10051a553f82450a49deae1f5e`

**Live provider or production resource used:** No

## Implemented

- Added a deterministic recovery acceptance test that creates a migrated and governed PostgreSQL 16
  source, writes canonical identity/membership, tenant, client, inventory import, pending outbox and
  protected-object-reference state, and creates a real non-empty custom-format `pg_dump` archive.
- Transferred the archive bytes into a separately created PostgreSQL 16 target and restored it with
  `pg_restore`. The target is not populated by replaying the source seed commands.
- Verified the restored database through the normal deterministic HTTP authentication boundary. The
  restored identity reads its profile and exactly one workspace; an unrelated tenant route is
  forbidden. The database membership source independently resolves the governed Campaign permission
  for the expected tenant and returns no membership for the unrelated tenant.
- Verified migration 024, master-data registry 2.9.0, exact canonical counts, the pending outbox
  identity/payload, protected object key/hash and all 80 forced-RLS canonical tables. Application-role
  tenant switching returns the expected client only for the owning tenant.
- Added database/object recovery and incident-response runbooks covering prerequisites, fail-closed
  containment, evidence retention, reconciliation/replay limits and the seven required incident
  classes. The documents explicitly retain owner and production decisions as blockers.

## Verification

| Check | Result |
|---|---|
| Complete Release API/database/security suite | PASS - 71/71 in 2m30s |
| Isolated PostgreSQL recovery and authenticated access | PASS - 1/1; final run completed in 1m32s under shared Docker load |
| Focused C# formatting | PASS - recovery acceptance file has no formatting changes |
| Governed master-data projection | PASS - registry 2.9.0 projections are current |
| Compose, diff and artifact hygiene | PASS - Compose valid, no whitespace errors, `.artifacts/` tracked files = 0 |
| Complete architecture suite | BLOCKED - 21/23; separately owned screen work still contains a 521-line CSS file and inline governed `PENDING` |
| Web/browser regression | NOT RUN - another agent owns and is actively changing the screen tree |

## Recovery boundaries verified

1. Backup and restore are exercised against two disposable real PostgreSQL 16 dependencies. Archive
   presence alone is insufficient; the restored target must satisfy every state and isolation probe.
2. Authentication and authorisation use the normal API and database membership paths. No test-only
   permission bypass or broader role is introduced.
3. The restored `advertified_app` role remains subject to tenant session context and forced RLS.
4. Pending outbox state is preserved, not marked published or replayed. The test performs no external
   send or commercial side effect.
5. A protected object key and SHA-256 reference are retained. Object bytes are deliberately not
   fabricated; managed object-byte recovery remains a separate required exercise.

## Superseded infrastructure-contention attempt

The current full-suite attempt did not produce a complete pass/fail result. It was stopped after
`CommercialFoundationApiAcceptanceTests`, `PersistedCommandAcceptanceTests` and
`CanonicalPlanningAcceptanceTests` each timed out in
`DisposablePostgres.EnableRequiredExtensionsAsync`. At inspection time, three separately launched
complete API suites and their PostgreSQL containers were running concurrently. These were dependency
startup timeouts, not product assertions. After those processes completed, the exact current suite
was rerun from isolated build output and passed all 71 tests in 2m30s. The earlier attempt is retained
here as diagnostic history and is not an outstanding failure.

## Remaining boundary

This packet is local Gate 12 progress, not Gate 12 completion or production approval. Managed
PostgreSQL PITR, matching S3 object-byte restore, measured RPO/RTO, staging exercise, monitoring and
alerts, performance, POPIA/security review, dependency/SBOM evidence, release rollback, screen/web
verification and named independent owner/operations/security/privacy/legal/finance approvals remain
pending. No push, deployment, live provider call or production mutation was performed.
