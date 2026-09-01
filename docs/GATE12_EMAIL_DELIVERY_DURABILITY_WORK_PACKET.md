# Gate 12 email delivery durability work packet

> **Historical packet — superseded 2026-09-01.** Independent review found security and recovery
> defects after this packet's original verification. Its results remain retained as historical
> evidence only. Current acceptance is owned by
> `GATE12_EMAIL_AUTOMATION_SECURITY_RECOVERY_WORK_PACKET.md` and its superseding evidence pack.

**Owner direction:** 2026-08-31, continue local production hardening after the verified Commercial
API observability slice.

**Verified predecessor:** The final-source Release Commercial API suite passes 91/91, the Release
test graph builds without warnings or errors, architecture passes 23/23, the evidence manifest
contract passes, 962 tracked/non-ignored source files have zero gitleaks findings, and all six local
Compose dependencies are healthy. Gate 12 remains in progress.

**Authority:** Local non-production implementation and deterministic verification only. No live
Resend request, production data, cloud resource, external communication, deployment, commit or push.

**Historical status:** The original bounded matrix passed locally, but that result does not verify
the corrected current tree. Registry `2.11.0`, migration `202608310026_EmailDeliveryDurability`,
and the requested/accepted transition foundation remain in the working tree. See the superseding
correction packet for current defects, corrections and acceptance evidence.

## Bounded requirement

Make the configured inbound-OOH proposal delivery safe when an email provider accepts a request but
the caller receives a timeout or transport failure. Persist one immutable delivery intent before the
external call, freeze its provider and idempotency key, reconcile that same intent after restart, and
never automatically submit the email a second time.

This packet does not build the general durable worker, outbox publisher or provider operations stack.

## Normative alignment and recorded conflict

Sections 7.3.1, 18.5, 19.2/19.2.1, 25, 26, E2E-04 and E2E-09, plus accepted ADR-0010, require the
specific opted-in inbound-OOH path to complete without per-request input while preventing duplicate
external action. Generic Section 25 wording also says `ProposalApproved` never sends automatically
and external email follows send confirmation. That wording conflicts editorially with the explicit
inbound-OOH exception. This packet preserves only the already accepted, tenant-enabled exception and
does not broaden automatic communication. Live delivery and production certification remain blocked
until the owner reconciles that wording and the real provider recovery contract is independently
verified.

## Required implementation

1. Registry `2.11.0`, effective from 2026-08-31, adds stable `DELIVERY_REQUESTED` and
   `DELIVERY_ACCEPTED` checkpoints, `DELIVERY_AMBIGUOUS`, `DELIVERY_RECORDING_REQUIRED`, and
   distinct requested/accepted audit actions and outbox event types. Generated C#, TypeScript and
   Python projections must be regenerated from the one JSON registry.
2. Forward-only migration `202608310026_EmailDeliveryDurability` adds the frozen delivery provider,
   requested timestamp and accepted timestamp to `email_proposal_automation_runs`. It must preserve
   legacy rows without inventing acceptance timestamps, constrain complete new intent/acceptance
   shapes, reference `emailProviders`, and refuse a destructive rollback while durable evidence is
   present.
3. The provider-neutral port exposes `ACCEPTED`, `NOT_FOUND` and `UNKNOWN` reconciliation outcomes.
   `EmailDeliveryAcceptanceUnknownException` distinguishes post-dispatch ambiguity from a confirmed
   failure. The deterministic adapter retains and reconciles a stable receipt per idempotency key.
4. Resend timeout, transport failure, server failure or malformed success after dispatch is treated
   conservatively as acceptance unknown. Until an official lookup contract is verified, Resend
   reconciliation returns `UNKNOWN`; it never repeats the POST to infer state.
5. A version-fenced database transition writes the frozen provider, idempotency key,
   `DELIVERY_REQUESTED` and its timestamp plus one audit/outbox pair before `SendAsync`. Only the
   caller that wins that transition may send. Provider acceptance is immediately persisted as
   `DELIVERY_ACCEPTED`, provider receipt and accepted timestamp with one audit/outbox pair.
6. Resume never rewinds requested/accepted checkpoints. `DELIVERY_REQUESTED` performs reconciliation
   only. `ACCEPTED` stores the original receipt and completes canonical proposal delivery;
   `UNKNOWN` or `NOT_FOUND` remains `REVIEW_REQUIRED/DELIVERY_AMBIGUOUS` and never resends.
   `DELIVERY_ACCEPTED` completes locally without another provider call.
7. Authenticated `:process` may reconcile a reviewable ambiguous run. Ordinary `:retry` must reject
   `DELIVERY_AMBIGUOUS` and every requested/accepted intent. Repeating `:process` after `SENT` is a
   no-op representation read.
8. The run view and inbox screen show provider, requested/accepted timestamps and truthful ambiguous
   delivery language. They offer safe processing/reconciliation, never a blind retry or the false
   statement that nothing was sent.
9. A failure after provider acceptance remains at checkpoint `DELIVERY_ACCEPTED` with its receipt
   and timestamp intact. It becomes `REVIEW_REQUIRED/DELIVERY_RECORDING_REQUIRED`, and processing
   finishes the existing local record without another provider send.
10. Failure handling classifies from the latest version-fenced database row, not stale process
    context. A concurrent processor cannot downgrade a delivery intent another processor persisted.

## Acceptance evidence

1. A zero-network fake provider accepts exactly once, records one stable receipt, then throws
   acceptance-unknown. Host one ends at `REVIEW_REQUIRED/DELIVERY_AMBIGUOUS` with checkpoint
   `DELIVERY_REQUESTED`; proposal state is not falsely marked sent.
2. Host one is disposed. Host two uses the same disposable PostgreSQL database and the same external
   fake ledger. `:process` reconciles `ACCEPTED`, reuses the original provider receipt, completes the
   proposal/run once, and does not invoke `SendAsync` again.
3. Repeating `:process` does not mutate canonical state. Exactly one run, proposal, provider
   acceptance, delivery-requested transition, delivery-accepted transition and sent transition
   exist; keys, provider IDs and timestamps remain stable.
4. A separate `UNKNOWN` reconciliation case remains review-required across host restart with one
   provider acceptance attempt and zero resend. Ordinary `:retry` is rejected.
5. Migration tests prove empty apply, representative legacy-row upgrade, constraints, master-data
   bootstrap and guarded rollback without fabricated legacy timestamps.
6. Existing signed-webhook, duplicate-message, no-send, tenant, permission, audit, outbox and normal
   deterministic-send cases remain green. UI schema/unit/Playwright coverage proves truthful recovery
   wording and action selection.
7. Focused formatting/tests, the complete Release API suite, web lint/type/unit/build, architecture,
   Compose validation, source secret scan and diff/artifact hygiene pass with retained commands.
8. A forced local-finalisation failure after persisted acceptance retains the accepted checkpoint,
   receipt and timestamp, records `DELIVERY_RECORDING_REQUIRED`, blocks ordinary retry and records no
   second provider attempt.
9. A barrier-controlled concurrent failure proves the losing processor rereads the durable row,
   preserves `DELIVERY_REQUESTED/DELIVERY_AMBIGUOUS`, emits no failed downgrade and sends once.

## Observed local verification

- The isolated `gate12-email-durability-final3` Release test graph passes 109/109 with zero skipped;
  its restore/build reports zero warnings and zero errors. Architecture passes 23/23.
- Deterministic provider/reconciliation tests pass 10/10. The restart and normal-delivery slice
  passes 3/3, including an accepted-after-restart case and a persistently unknown case with no
  second provider send. The migration and retained-contract slice passes 8/8. The two independent
  review regressions described above pass together 2/2 and in the complete 109-test suite.
- Web lint, type-check, master-data generation check, unit 6/6 and production build pass. The full
  configured Playwright matrix passes 32/32. The deterministic Python runtime passes Ruff and
  28/28 tests.
- The scoped .NET formatting check for the delivery-durability files passes. The repository-wide
  formatting check still fails on pre-existing whitespace/line-ending drift in unrelated test
  files; those unrelated owner changes were not bulk-reformatted.
- Compose validates and all six local dependencies are healthy. The final source secret scan inspected
  977 tracked/non-ignored files with zero findings; diff checks pass and `.artifacts/` remains
  ignored and untracked.
- On the named local developer database only, the dedicated least-privilege runner applied the 14
  pending migrations `202608300013_SupplierMarketplace` through
  `202608310026_EmailDeliveryDurability`. The history now records migrations 001 through 026, and
  the immediate idempotency rerun applied 0 migrations; both runs synchronised 71 master-data
  collections. No staging or production database changed.
- The updated API binary returned HTTP 200 for liveness and dependency readiness on an isolated
  canary and after the local port-5000 restart. The local frontend returned HTTP 200 for `/` and
  `/sign-in`; deterministic login and `/me` returned HTTP 200, the workspace list returned exactly
  `Advertified Local Development` with `platform_admin` in tenant
  `10000000-0000-0000-0000-000000000010`, and logout returned HTTP 204.
- An intermediate full-suite run passed 107/109 and therefore failed. One test exposed a temporary
  registry `effectiveFrom` of 2026-09-01 that was ahead of PostgreSQL `CURRENT_DATE`; the other
  exposed a recovery assertion hard-coded to registry version `2.9.0`. The registry date was
  restored to 2026-08-31 and the recovery assertion now uses the generated registry version. Both
  cases then passed independently and in the isolated 109/109 suite.
- `deliveryRequestedAtUtc` is an Advertified-clock timestamp and `deliveryAcceptedAtUtc` is provider
  evidence from a different clock. Migration 026 intentionally retains both exact values without
  enforcing cross-clock ordering; this is documented policy, not an unresolved schema defect.

Repeatable evidence is retained under
`docs/evidence/gate12-email-delivery-durability-20260831/`.

## Explicitly out of scope and still blocked

- live Resend credentials, request, sandbox canary, verified provider lookup or provider webhook
  reconciliation;
- automatic resend after `UNKNOWN` or `NOT_FOUND`; only a future separately authorised command may
  define that consequential decision;
- general worker lease/heartbeat/fencing, process/retry command replay, scheduled reconciliation,
  outbox publication/retry/dead-letter handling and queue recovery;
- remote CI, central telemetry export/dashboards/alerts, staging/provider canaries, managed
  point-in-time and complete object-family recovery, measured RPO/RTO, performance/load evidence,
  named operations ownership and Security/Privacy/Legal review;
- the repository-wide formatting failure caused by unrelated pre-existing whitespace/line-ending
  drift, despite the passing scoped delivery-durability format check;
- the recorded Section 25 generic-send wording conflict, which still requires an owner editorial
  decision before live delivery certification;
- claiming E2E-09, Gate 12, Operations review or production readiness approved.
