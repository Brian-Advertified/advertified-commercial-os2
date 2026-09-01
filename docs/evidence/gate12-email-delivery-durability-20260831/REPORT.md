# Gate 12 email delivery durability evidence

> **Historical evidence — superseded 2026-09-01.** Independent review subsequently found two high,
> three medium and one low security/recovery defects. This report truthfully preserves what passed
> on 2026-08-31; it is not evidence that the corrected current tree is verified. See
> `../../../GATE12_EMAIL_AUTOMATION_SECURITY_RECOVERY_WORK_PACKET.md` and the separate 2026-09-01
> corrective evidence pack.

**Evidence date:** 2026-08-31

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this is not a release artefact

**Live provider or production resource used:** No

## Created and implemented

- Added one durable, immutable outbound-email intent before the provider boundary. The provider,
  idempotency key and `DELIVERY_REQUESTED` timestamp are frozen before dispatch.
- Added explicit `DELIVERY_ACCEPTED` evidence and `DELIVERY_AMBIGUOUS` recovery. Restart recovery
  reconciles the original intent; it never infers provider failure by submitting the email again.
- Kept the existing, tenant-enabled inbound-OOH exception narrow. Ordinary retry is blocked once a
  delivery intent exists, while authenticated processing may reconcile an ambiguous run.
- Added forward-only migration `202608310026_EmailDeliveryDurability`, registry `2.11.0`, generated
  projections, provider-neutral reconciliation outcomes and truthful recovery data in the API and
  browser UI.
- Added governed failure reason `DELIVERY_RECORDING_REQUIRED`. Once provider acceptance is durable,
  a local finalisation problem remains an accepted delivery needing local recording; it is never
  relabelled as unknown acceptance.
- Failure classification now uses the transaction-current persisted run, so a stale concurrent
  failure cannot overwrite a delivery intent committed by another processor.
- Added deterministic restart, provider-adapter, migration, contract and browser coverage. No live
  Resend call, paid AI call, external communication or production data was used.

## Verification

| Check | Final result |
|---|---|
| Isolated final3 Release restore and API/test build | PASS - zero warnings and zero errors |
| Focused provider adapters | PASS - 10/10 |
| Restart recovery plus normal deterministic delivery | PASS - 3/3 |
| Accepted delivery with local-finalisation failure regression | PASS - accepted evidence and `DELIVERY_RECORDING_REQUIRED` retained |
| Concurrent failure-state regression | PASS - persisted delivery intent cannot be downgraded |
| Email durability migration, master-data migration/snapshot and retained API contract | PASS - 8/8 |
| Intermediate full API run before final corrections | FAIL - 107/109; both failures were corrected and rerun |
| Complete final3-source Release API suite | PASS - 109/109 |
| Architecture suite | PASS - 23/23 |
| Deterministic Python runtime | PASS - Ruff clean and 28/28 tests |
| Web master-data, type, lint, unit and production build | PASS - generated data current, type/lint/build clean and 6/6 unit tests |
| Desktop and compact browser journeys | PASS - 32/32 |
| Compose definition and dependency health | PASS - configuration valid and all six services healthy |
| Tracked/non-ignored source secret scan | PASS - final 977-file mirror, zero gitleaks findings |
| Scoped C# formatting and diff/artifact hygiene | PASS - no affected-file formatter change, whitespace error, staged file or tracked `.artifacts` output |
| Local migration runner | PASS - applied pending migrations 013 through 026 once, synchronised 71 registry collections, then applied 0 on the idempotent rerun |
| Updated final3 local API smoke | PASS - port 5000 returned HTTP 200 for liveness and readiness; the final3 binary is PID 18900 |
| Local frontend, API proxy and deterministic authenticated session | PASS - `/`, `/sign-in` and the proxied session endpoint returned 200; login, `/me` and workspaces returned 200; logout returned 204 |
| Evidence JSON structural preflight | PASS - JSON parses and required root/check shapes and enums match the repository schema; full `jsonschema` evaluation was unavailable |
| Repository-wide C# formatting | FAIL - pre-existing unrelated whitespace/EOL drift remains outside this bounded packet |

## Findings corrected during final verification

The first complete run after adding the two new regression cases passed 107/109. The two failures
were not left open:

1. Registry `2.11.0` initially had a future `effectiveFrom` relative to PostgreSQL's
   `CURRENT_DATE`, so `MigrationBootstrapsRegistryIdempotentlyAndProtectsStableCodes` failed closed.
   The effective date was corrected to `2026-08-31`.
2. `CustomBackupRestoresCanonicalTenantSafeStateIntoIsolation` still asserted hardcoded registry
   version `2.9.0`. It now asserts the generated `MasterDataCodes.RegistryVersion`, retaining the
   recovery invariant without pinning a stale version.

The isolated final3 restore/build and the complete rerun then passed 109/109. The two new behavior
regressions also pass together 2/2.

The least-privilege local migration run found fourteen pending forward migrations, not only the new
email migration. It applied `202608300013_SupplierMarketplace` through
`202608310026_EmailDeliveryDurability` to the local development database, then the same runner
reported `Applied 0 migration(s); synchronised 71 master-data collections.` Migration history was
verified through 026. This was local-only and did not touch production.

The final3 API readiness response checks the process, database and governed master data. A real local
cookie session then returned the configured user
`10000000-0000-0000-0000-000000000001` (`Local Developer`) and exactly one workspace:
`Advertified Local Development`, role `platform_admin`, tenant
`10000000-0000-0000-0000-000000000010`. The frontend `/` and `/sign-in` routes both returned HTTP
200, and session logout returned HTTP 204.
The Vite proxy also returned HTTP 200 from `/api/v1/session` with the expected unauthenticated state
and a non-empty antiforgery token before sign-in.

The scoped formatter passes for the email durability implementation. The repository-wide command
still reports unrelated existing formatting drift, including files in Docling inventory extraction,
inventory, marketplace funding, opportunity workflow, proposal and tenant-isolation tests. Those
files were not bulk-formatted because they contain work outside this packet.

## Acceptance result

The deterministic restart evidence proves both recovery branches: a provider-confirmed acceptance
reuses the original receipt and completes locally, while `UNKNOWN` remains review-required across a
host restart. Both branches retain one send attempt. Repeating processing after completion is a
no-op, and ordinary retry cannot cross an existing requested or accepted delivery intent.

The two medium consistency risks found during the read-only diff review are resolved. If provider
acceptance is persisted but canonical proposal finalisation cannot complete, the run remains
`REVIEW_REQUIRED` at checkpoint `DELIVERY_ACCEPTED` with
`DELIVERY_RECORDING_REQUIRED`; provider receipt and timestamps remain intact and retry stays
blocked. In the concurrent case, failure handling rereads the durable current row and cannot
downgrade another processor's persisted request to `FAILED/DELIVERY_FAILED`.

The complete API suite keeps the existing permission, tenant, audit, outbox, signed-webhook,
duplicate-message and no-send boundaries green. The browser evidence confirms that a requested
delivery uses safe processing/reconciliation and does not offer a blind retry or claim that nothing
was sent.

## Blocked before production

- Resend still has no verified lookup or webhook-reconciliation contract in this repository.
  Reconciliation deliberately returns `UNKNOWN`, no live provider proof exists, and automatic
  resend after `UNKNOWN` or `NOT_FOUND` remains forbidden without a separately authorised command.
- General worker lease/heartbeat/fencing, process/retry command replay, scheduled reconciliation,
  durable outbox publication/retry/dead-letter handling and queue recovery remain unimplemented.
- Central telemetry export and alerts, staging evidence, load/performance evidence, managed PITR and
  full object-recovery rehearsal, named independent reviews, remote CI and an immutable release
  artefact remain required.
- The generic send-confirmation wording conflicts editorially with the accepted inbound-OOH
  exception. The repository owner must resolve that wording before live delivery certification.
- Provider-accepted and locally requested timestamps use different clocks. Migration 026 correctly
  does not reject clock skew, but production detection and handling of materially implausible
  provider timestamps remains undefined.
- The repository-wide `dotnet format --verify-no-changes` failure from pre-existing unrelated
  whitespace/EOL drift remains open; the affected email durability scope itself passes.

This evidence verifies only the bounded local Gate 12 slice. It does not approve Gate 12, a security
or operations review, or production readiness. No commit, push, deployment, production mutation or
live provider call was performed.
