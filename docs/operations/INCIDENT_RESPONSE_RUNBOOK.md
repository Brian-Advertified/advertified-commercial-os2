# Incident response runbook

## Status and authority

This runbook defines the minimum fail-closed response for the incidents required by the normative
specification. It does not assign people or grant production authority. Before staging or production,
the repository owner must name an incident commander, operations lead, security/privacy owner,
commercial decision owner and communications owner, with on-call paths and deputies.

Use the organisation-approved severity scheme once it is supplied. Do not invent a severity label,
privacy conclusion, customer impact, recovery time or external notification obligation.

## Common first response

1. Create an immutable incident record with detection time, reporter, environment, tenant scope,
   symptoms, last known-good commit/configuration and current operator.
2. Preserve logs, traces, metrics, audit/outbox records, deployment metadata and relevant source
   artefact hashes. Restrict access to incident evidence.
3. Fence the smallest affected mutation or external-action boundary. Unknown tenant scope requires
   the broadest safe isolation, not an assumption that one tenant is affected.
4. Disable live provider, message, booking, payment, publication or AI calls where their outcome or
   authority cannot be proven. Do not broaden permissions or spend caps to restore throughput.
5. Page the named incident roles. Security/privacy events require the named security/privacy owner;
   only authorised humans decide whether and how to notify external parties.
6. Establish an update cadence and decision log. Record facts, unknowns, hypotheses, actions,
   authorisers and results separately.
7. Restore through an approved runbook, then reconcile canonical state and external side effects
   before resuming normal work.

## Required incident procedures

### API unavailable

- Detect: authenticated synthetic/API checks, health probes, edge errors, saturation and database
  connectivity. A process being alive is not proof that authenticated commands work.
- Contain: stop rollout; keep unsafe writes closed; present the approved degraded/read-only state if
  available; preserve idempotency data for failed or timed-out commands.
- Diagnose: trace edge to API to database, secret/config resolution, dependency health and the last
  deployment/change. Do not test by issuing a real commercial side effect.
- Restore: recover the failed dependency or roll back compatible image/configuration. Validate
  authentication, tenant membership, a safe read and an idempotent non-external command.
- Reconcile: classify timed-out commands by idempotency/correlation ID and compare audit/outbox state.
  Unknown external outcomes remain blocked for human review.

### Worker backlog

- Detect: oldest queue/outbox age, pickup latency, retry rate, dead-letter/poison class, saturation and
  tenant distribution.
- Contain: pause admission of heavy work and provider side effects; do not delete queued messages or
  raise retry/spend limits without approval.
- Diagnose: separate capacity pressure from deterministic poison messages, dependency failure and
  code regression. Preserve representative payload hashes with secrets/personal data redacted.
- Restore: quarantine the poison class, recover the dependency, or scale within approved limits;
  resume from durable checkpoints with the same idempotency keys.
- Reconcile: prove no duplicate canonical mutation or external action, then account for every queued,
  completed, quarantined and failed item.

### Database failure

- Detect: connection/transaction errors, replica/backup health, corruption signals, migration drift
  and write latency.
- Contain: fail canonical writes closed and fence workers/API instances that may target divergent
  primaries. Preserve transaction, audit and deployment evidence.
- Restore: follow [Database and object recovery runbook](RECOVERY_RUNBOOK.md) into an isolated target.
  Never overwrite the failed database in place merely to shorten downtime.
- Validate: migration and master-data versions, authentication/membership, canonical counts, forced
  RLS, tenant negatives, outbox and object key/hash/byte consistency.
- Reconcile: compare the recovery gap with immutable source/provider evidence. External replay and
  manual corrections require the correct named human approval.

### Provider outage or cost spike

- Detect: provider errors/timeouts, circuit state, usage ledger, account cap, anomalous unit cost and
  missing receipts.
- Contain: open the circuit, preserve run/checkpoint state and stop new billable attempts. Do not
  silently select a different provider or increase budgets.
- Diagnose: distinguish provider availability, credential/contract access, rate limiting, malformed
  requests and internal retry amplification.
- Restore: use only a pre-approved deterministic fallback or route to human continuation. A provider
  switch that changes data location, terms, quality or cost requires owner approval.
- Reconcile: match every attempted call to usage/cost and run trace; classify unknown outcomes and
  retain them for human disposition.

### Cross-tenant or security event

- Detect: tenant-negative failure, IDOR, unexpected membership/permission, suspicious export,
  credential exposure, injection/file-abuse signal or unauthorised audit/outbox access.
- Contain: revoke affected sessions/credentials, fence the affected endpoints/workers and isolate
  evidence. Do not delete or rewrite access/audit records.
- Escalate: immediately involve the named security/privacy owner. Only that owner and authorised
  counsel decide whether personal data was involved and which notification duties apply.
- Investigate: identify the earliest possible exposure, actor, tenant/resource scope, data classes,
  code/configuration path and whether exports or external actions occurred. Treat unknown scope as
  unresolved.
- Restore: correct the boundary, rotate authorised secrets, run tenant-negative and security tests,
  and obtain independent security/privacy approval before reopening.
- Reconcile: provide affected canonical IDs and evidence hashes without expanding access to incident
  data. Never claim absence of access from absence of a known complaint.

### Bad deployment

- Detect: health/SLO regression, contract mismatch, migration error, security boundary failure or
  business-error increase correlated with image/configuration/flag rollout.
- Contain: stop rollout and record exact image, commit, migrations, configuration and flags. Preserve
  failed-pod/task logs and traces.
- Restore: roll back a compatible image/configuration or disable an approved reversible flag. Never
  roll an application behind an incompatible forward-only schema.
- Data limit: preserve forward-only migrations and use an reviewed compensating migration where
  required. Do not edit migration history, delete rows or restore an old database over newer state.
- Reconcile: verify authenticated critical paths, outbox/jobs, external idempotency and canonical
  counts before resuming rollout.

### Extraction regression

- Detect: labelled-corpus precision/recall change, schema rejection, unsupported-claim increase,
  field/evidence mismatch or human correction spike by parser/prompt/model/config version.
- Contain: stop publish and downstream commercial use; retain raw imports, hashes, extracted output,
  evidence bindings and version trace. Quarantine suspicious files.
- Diagnose: reproduce with deterministic fixtures and the labelled/held-out corpus. No live/paid AI
  provider call is permitted during redevelopment certification.
- Restore: roll back to an approved parser/prompt/config version or correct the implementation;
  rerun regression, adversarial and tenant-isolation checks.
- Reconcile: mark affected proposed outputs invalid/stale and regenerate from retained source. Do not
  rewrite approved canonical facts without authorised human review and a new immutable version.

## Recovery validation and closure

Recovery is complete only after the original failure is understood or explicitly recorded as
unknown, safe service checks pass, canonical and external outcomes reconcile, tenant boundaries are
verified, monitoring is stable for the approved observation window and follow-up work has named
owners/dates. Closing an incident does not approve production readiness.

Retain the timeline, evidence manifest, exact commands, before/after configuration, image and commit
identities, approvals, verification output, reconciliation ledger and unresolved risks. Record failed
checks as failed; do not replace them with confidence statements.

## Decisions required before production

- Named incident roles, deputies, on-call/contact paths and authority matrix.
- Approved severity definitions, declaration thresholds and evidence access/retention policy.
- Security/privacy assessment and external-notification decision process.
- Environment-specific degraded modes, provider fallbacks, scaling limits and circuit thresholds.
- Rollback/change approvers, observation windows and incident exercise schedule.
