# Gate 12 outbox dispatch durability work packet

**Owner direction:** 2026-09-01, continue local production hardening after verified email-delivery
durability. Immutable build-input hardening may proceed in parallel because it does not touch the
application, schema or outbox boundary.

**Verified predecessor:** Registry 2.11.0, migrations 001 through 026, isolated Release API 109/109,
web Playwright 32/32, deterministic runtime 28/28, architecture 23/23, six healthy Compose
dependencies and a zero-finding 977-file source secret scan. Gate 12 remains in progress.

**Authority:** Local non-production implementation and deterministic verification only. No broker,
cloud resource, production data, live provider, external communication, deployment, commit or push.

## Bounded requirement

Implement the canonical Commercial API transactional-outbox dispatch kernel. Claim one durable event
with a lease, publish it through a provider-neutral port using its immutable event ID, heartbeat the
lease, and record acceptance or a bounded retry/dead-letter result through claim-token-fenced
transitions. A crash or stale worker must never rewrite event truth or acknowledge another worker's
claim. This local kernel is explicitly tenant-bound: enabled deterministic dispatch requires one
configured tenant, and every database operation installs and validates that transaction tenant
context.

This packet does not add EventBridge/SQS, an email consumer, an opportunity worker retrofit or a
production application image.

## Normative alignment

Sections 17.4, 18.1/18.2, 25.1, 26/26.1, 27.1/27.4 and E2E-09/E2E-12 require one Commercial API
outbox, provider-neutral adapters, stable idempotency, `SKIP LOCKED` claims, leases/heartbeats,
30-second/2-minute/10-minute retries, poison/dead-letter retention and restart-safe recovery.

An earlier packet draft implied a global application-role dispatcher. That conflicted with
ADR-0006 and was corrected before verification: ADR-0006 is authoritative, so this vertical is
tenant-bound and production cross-tenant scheduling remains blocked pending its own accepted ADR.

## Required implementation

1. Add forward-only migration `202609010027_OutboxDispatchDurability`. Preserve every historical
   event and existing published timestamp; do not mark any row published during upgrade.
2. Add nullable operational metadata for next attempt, unique claim token, worker ID, lease expiry,
   attempt start, transport reference, safe last failure and dead-letter time. Enforce mutually
   coherent unclaimed, leased, published and dead-letter shapes.
3. Protect immutable event identity, tenant, causation, correlation, type, aggregate, payload and
   occurrence time from update/delete. Revoke direct application-role update/delete; owned
   security-definer functions may mutate delivery metadata only. The migration executor owns these
   functions; the application role never does.
4. A tenant-safe security-definer claim function selects one due unpublished/non-dead-letter event
   using `FOR UPDATE SKIP LOCKED`, installs a new claim token/lease, increments attempts and returns
   the exact immutable envelope. It reads the transaction tenant context, filters that tenant and
   exposes no arbitrary tenant selector. Cohesive lock/install/exhaustion helpers remain
   migration-owned, fixed-search-path and denied to both the application role and `PUBLIC`. There is
   no global application-role claim path.
5. Heartbeat, acknowledge and failure functions require the exact event ID and claim token.
   Heartbeat cannot revive an expired/lost/terminal claim. Acknowledge and failure require an active
   database-clock lease. A stale or expired worker cannot acknowledge or fail a reclaimed event.
6. The provider-neutral application port accepts a typed immutable envelope whose event ID is the
   transport idempotency key. It returns an accepted transport reference or a typed transient or
   terminal failure; provider SDK/status objects remain outside the contract.
7. Transient failures retry after exactly 30 seconds, 2 minutes and 10 minutes. Failure of the fourth
   claimed attempt dead-letters. A fourth claim that expires without an outcome records safe lease
   failure evidence and dead-letters instead of allowing attempt five. Terminal failure dead-letters
   immediately. No poison event spins.
8. The dispatcher is disabled by default. A deterministic in-memory transport is allowed only in
   Development/Test and requires one configured tenant ID. No live transport or credentials are
   introduced.
9. When enabled, readiness includes transport health. Local structured logs and metrics identify
   accepted, retry and dead-letter outcomes using event/correlation identifiers without payload or
   PII. A claim-time crash-exhaustion transition returns a typed terminal observation for logging
   and metrics without reopening publication.

## Acceptance evidence

1. Two workers race for one event and exactly one claim token wins.
2. Heartbeat extends an active lease. Expiry permits reclaim with a new token, and the old token is
   rejected by heartbeat, acknowledge and failure transitions. Database time owns claims,
   heartbeats, acceptance, failure and retry evidence; caller-supplied timestamp signatures do not
   exist. If publication completes while a heartbeat database call is being cancelled, internal
   cancellation completes cleanly and the active-lease acknowledgement remains the final fence.
3. The transport accepts, the host is simulated to stop before acknowledgement, and restart
   republishes the same event ID. A deterministic idempotent sink records one consequence before the
   event is acknowledged once.
4. Transient failures schedule 30 seconds, 2 minutes and 10 minutes, then dead-letter on attempt four;
   terminal failure dead-letters on its first attempt. Four consecutive lease expiries also
   dead-letter with safe evidence and no fifth claim. Exact safe failure data and attempts remain,
   and the claim-time terminal observation increments the dead-letter metric and emits the
   privacy-safe structured event/correlation log.
5. Published and dead-letter states are mutually exclusive and cannot be claimed again.
6. Immutable event fields and cross-tenant rows cannot be changed through the application role.
   Tenant A context cannot claim tenant B. Claim/heartbeat/acknowledge/fail execute under
   `advertified_app` only through their constrained, fixed-search-path security-definer functions;
   `PUBLIC` has no execute privilege, and the migration executor rather than the application role
   owns each function. Tenant A transition calls against an active tenant B claim return false and
   leave every tenant B dispatch field unchanged.
7. Migration tests cover empty apply, representative legacy unpublished and published rows,
   constraints, function grants, immutability and guarded rollback.
8. Disabled startup claims nothing. Enabled healthy/unhealthy deterministic transport produces
   truthful readiness. Unavailable transport leaves its event unpublished and non-dead-lettered
   with durable transient failure/retry evidence.
9. Targeted tests, complete Release API, architecture, scoped formatting, Compose, source-secret and
   diff/artifact hygiene checks pass with repeatable evidence and zero live provider use.

## Current local verification — 2026-09-01

Implemented and locally verified on the uncommitted `master` working tree:

- Release API/test graph build: PASS, zero warnings and zero errors;
- current outbox filter: PASS, 7/7;
- focused contract, migration and readiness cases: PASS, 4/4;
- affected model snapshot, health, persisted-command and tenant-isolation regressions: PASS, 5/5;
- architecture: PASS, 31/31;
- Compose definition and dependency health: PASS, six configured services healthy;
- scoped formatting, diff/staging/artifact/trailing-whitespace hygiene: PASS;
- scoped current-source gitleaks scan: PASS, 31 affected files and zero findings.
- complete current-tree Release API suite: PASS, 128/128 in 3m57s.

Repeatable commands, findings and blockers are retained in
`docs/evidence/gate12-outbox-dispatch-durability-20260901/`. The combined final-tree architecture
rerun and complete current-source secret scan after the latest documentation/build-input changes
remain pending. This packet does not claim Gate 12 delivery or production approval.

## Explicitly out of scope and still blocked

- A production cross-tenant scheduler/service identity. ADR-0006 remains authoritative: this local
  vertical does not create a global `advertified_app` claim function. Cross-tenant scheduling needs
  a separately accepted ADR, dedicated least-privilege identity and independent security evidence;
- EventBridge/SQS adapter, managed credentials, sandbox canary and live transport proof;
- consumer idempotency for each canonical event and historical backlog release policy;
- opportunity-worker lease fencing/ambiguous paid-call recovery;
- moving inbound email processing off the webhook path and scheduled email reconciliation;
- production OIDC/durable sessions, application containers, remote CI, staging, managed recovery,
  performance/load, independent security/privacy/operations review and production greenlight.
