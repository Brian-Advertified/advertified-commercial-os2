# Gate 12 outbox dispatch durability evidence

**Evidence date:** 2026-09-01

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this is not a release artefact

**Live provider or production resource used:** No

## Implemented

- Added migration `202609010027_OutboxDispatchDurability` with immutable event truth, coherent
  operational states, one-event `SKIP LOCKED` claims, database-clock leases, unique claim tokens,
  active-lease acknowledgement/failure fencing, exact retries and bounded dead-letter handling.
- Kept the local dispatcher explicitly tenant-bound under ADR-0006. Enabled deterministic dispatch
  requires one configured tenant. The application role has no global claim path and cannot execute
  the private lock/install/exhaustion helpers.
- Added a provider-neutral typed envelope/result/health port and deterministic Development/Test
  transport. The immutable event ID is the idempotency key. No broker SDK, credential or live
  provider was introduced.
- Added lease heartbeat, hard publish timeout signalling, privacy-safe logs and metrics, truthful
  enabled/disabled readiness, and a typed claim-time terminal observation so four consecutive
  crashes are visible without allowing a fifth attempt or another publish.

## Verification

| Check | Final result |
|---|---|
| Final Release API/test graph build | PASS - zero warnings and zero errors |
| Focused current outbox filter | PASS - 7/7 in 20 seconds |
| Contract, migration and readiness focus | PASS - 4/4 in 15 seconds |
| Affected model/health/command/tenant regressions | PASS - 5/5 in 18 seconds |
| Architecture suite | PASS - 31/31 |
| Scoped C# formatting | PASS - no formatter changes |
| Compose definition and local dependency health | PASS - six services healthy |
| Diff, staging, artifact and trailing-whitespace hygiene | PASS |
| Scoped current-source secret scan | PASS - 31 affected files, zero findings |
| Evidence JSON structural preflight | PASS - required roots/checks/enums and JSON parse |
| Full JSON Schema evaluation | BLOCKED - Python `jsonschema` is unavailable |
| Complete final-tree Release API suite | PASS - 128/128 in 3m57s |

The database acceptance test covers competing workers, heartbeat extension, a deterministic
publish/heartbeat-cancellation collision, stale and expired fences, restart idempotency, exact
30-second/2-minute/10-minute retry intervals, attempt-four and terminal dead letters, publish
timeout, four consecutive crash expiries, and claim-time dead-letter log/metric emission. It also
executes functions under `SET LOCAL ROLE advertified_app`, proves missing tenant context fails,
proves tenant A cannot claim or transition tenant B, and verifies function owner, fixed search path,
`PUBLIC` denial, helper denial and absence of caller-timestamp signatures.

The migration test covers empty apply/down, representative legacy unpublished/published rows,
forward reapply and guarded rollback after dispatch evidence. The readiness test proves disabled
startup leaves work untouched, healthy deterministic delivery is accepted, and unavailable
transport returns 503 while retaining an unpublished, non-dead-lettered event with durable retry
evidence.

## Findings closed during verification

1. Provider completion could cancel an in-flight heartbeat database call before acknowledgement.
   Internal heartbeat cancellation now completes cleanly; host cancellation is still rethrown by
   the processor before acknowledgement. A row-lock collision test proves the accepted event is
   acknowledged once.
2. The fourth expired claim originally dead-lettered only inside SQL and bypassed local telemetry.
   Claim now returns a typed terminal observation. The processor records the dead-letter counter and
   structured event/correlation log without publishing again.
3. Heartbeat validation originally multiplied two `int` values. It now uses a practical bound and
   overflow-safe half-lease comparison, with maximum-value and boundary tests.
4. The first claim SQL draft exceeded the repository's 60-line hard function limit. Lock, install
   and exhaustion responsibilities are split into private fixed-search-path helpers. The exposed
   claim function is 53 physical lines; no helper is granted to the application role or `PUBLIC`.
5. Unavailable readiness initially had no pending work after the healthy host completed the only
   event. A second event now proves unavailable transport retains durable retry evidence.
6. One intermediate build failed with two missing test namespace references after the telemetry
   assertion was added. The imports were corrected; the final Release build passes with zero
   warnings and errors.
7. Python full evidence-schema evaluation was unavailable because `jsonschema` is not installed. A
   first PowerShell structural command also used invalid line-leading Boolean operators and emitted
   `CommandNotFoundException`; the corrected fail-fast structural preflight passed. No full JSON
   Schema evaluator result is claimed.
8. The first run of the retained exact formatter script found twelve indentation errors in the
   outbox index expressions in the model configuration and snapshot. Both equivalent expressions
   were corrected with no model change; the retained formatter command then passed.

## Blocked before production

- Production cross-tenant scheduling/service identity is not designed or approved. ADR-0006 remains
  authoritative; a separate accepted ADR and dedicated least-privilege identity are required.
- EventBridge/SQS, managed credentials, consumer idempotency, backlog release policy, sandbox canary
  and live transport proof remain absent.
- Central metric/log export, alerts, named operations ownership, load/performance, managed recovery,
  remote CI, clean-checkout reproducibility and an immutable application image remain unverified.
- Opportunity-worker fencing, moving inbound email work off the webhook path and scheduled email
  reconciliation remain separate unfinished durability work.
- The repository-wide formatter still has known unrelated whitespace/EOL drift; only the bounded
  outbox scope is verified clean.

This evidence verifies only the bounded local Gate 12 slice. It does not approve Gate 12, a security
or operations review, deployment or production readiness. No commit, push, deployment, production
mutation, external communication or live provider call was performed.
