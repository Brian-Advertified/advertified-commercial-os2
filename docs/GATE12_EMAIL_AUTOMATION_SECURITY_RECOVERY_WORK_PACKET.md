# Gate 12 email automation security and recovery corrections

**Owner direction:** 2026-09-01, fix every issue found while continuing local production hardening.

**Trigger:** Independent review of the email-delivery durability slice found two high, three medium
and one low issue after its initial 109-test evidence. That earlier evidence remains historical; it
must be superseded by corrected verification rather than silently rewritten as if the defects had
not existed.

**Verified predecessor:** Registry 2.11.0, migration 026, isolated Release API 109/109, web
Playwright 32/32, runtime 28/28, architecture 23/23 and a zero-finding 977-file source scan. The
immutable-input and outbox packets may proceed in parallel because they do not edit email automation
or its web presentation.

**Authority:** Local deterministic implementation and verification only. No Resend request,
production data, external communication, live provider, cloud mutation, deployment, commit or push.

**Current status:** Corrections are implemented. Focused provider/security checks pass 14/14,
focused email-workflow checks pass 11/11, and the complete current-tree Release API suite passes
128/128 in 3m57s. Web lint, type-check, unit 6/6, production build and the API-mocked Playwright
matrix 32/32 also pass. The combined final-tree architecture rerun and complete current-source
secret scan remain pending, so this packet does not claim complete release certification or
production approval.

## Bounded defects and required corrections

1. **Global kill switch and provider scope.** `EmailAutomation.Mode=Disabled` must block mailbox
   configuration, provider resolution, reconciliation and send. An active mode may resolve/configure
   only its matching provider code. An already accepted delivery may still complete its local record
   because that performs no provider action.
2. **Verified reply boundary.** Preserve every inbound source, but automatic delivery requires a
   trusted provider identity assessment plus a reply destination authorised by mailbox policy.
   Deterministic fixtures may provide trusted local evidence. Resend remains unverified and must
   enter review until an official trusted sender-authentication contract is implemented. A differing
   Reply-To is allowed only when its domain is explicitly allowed; an empty allowlist permits only
   the authenticated sender address itself.
3. **Confirmed rejection classification.** A provider-confirmed rejection remains
   `DELIVERY_FAILED`, never `DELIVERY_AMBIGUOUS`. Timeout/transport/server/malformed-success outcomes
   remain ambiguous. Neither path blindly resends.
4. **Manual command provenance and idempotency.** Authenticated `:process` and `:retry` transitions
   use the invoking operator as audit actor and persist their command/idempotency envelope before
   long processing. Replaying the same key resumes/reads canonical state without duplicating the
   command consequence. Automatic webhook processing continues to use the accountable mailbox owner.
5. **Interrupted processing recovery.** A run left `PROCESSING` at a durable checkpoint remains
   visibly recoverable through `:process`. Requested/accepted checkpoints reuse reconciliation or
   local completion and never issue a blind second send. Evidence wording must distinguish a
   classified restart from a true interruption after a committed checkpoint.
6. **Truthful presentation.** Server-provided safe detail takes precedence over a generic failure
   label. Confirmed rejection, ambiguous acceptance, accepted-but-unrecorded and interrupted
   processing have distinct headings/actions. Browser fixtures must use the same governed failure
   code in list and detail representations.

## Implementation constraints

- Do not invent sender authentication from raw header syntax. Provider-owned assessment is a typed
  port result; unknown remains unverified.
- The server, not React, owns provider/mode/trust/recovery rules.
- Reuse registry 2.11.0 codes. Do not add a schema migration merely to store a second source of truth;
  retain the trusted assessment in server-authored immutable source metadata and validate it before
  every automatic send.
- Preserve the frozen delivery provider/idempotency key and migration-026 evidence.
- Keep files under 400 lines and split by business responsibility.

## Acceptance evidence

1. Disabled mode and provider-mode mismatch cannot configure, resolve, reconcile or send; accepted
   local finalisation remains possible without provider access.
2. An authenticated deterministic sender replying to itself completes. A forged allowed-domain From
   with a different unapproved Reply-To is preserved but becomes `REVIEW_REQUIRED/INVALID_RECIPIENT`
   with zero delivery. Resend assessment is explicitly unverified without a network call.
3. A provider-confirmed client rejection becomes `FAILED/DELIVERY_FAILED`, retains requested evidence,
   is not offered blind retry/reconcile, and is not relabelled ambiguous. Timeout/transport ambiguity
   regressions remain green.
4. A second authorised administrator invoking `:process` appears as audit actor; the same idempotency
   key records one applied command consequence plus normal replay evidence and no duplicate send.
5. Cancellation after a committed delivery request leaves `PROCESSING/DELIVERY_REQUESTED`; a new host
   can invoke `:process`, reconcile the same key, and does not call `SendAsync` again. Accepted
   `PROCESSING` state completes locally without provider access.
6. Focused web tests/Playwright prove truthful list/detail copy and the recovery action for
   `PROCESSING`; list and detail fixtures agree.
7. Targeted and complete API/web suites, architecture, scoped formatting, Compose, secret and
   diff/artifact hygiene pass. Evidence records intermediate failures and no production approval.

## Still blocked after this packet

- official Resend sender-authentication and lookup/webhook reconciliation contracts plus sandbox
  proof;
- a background email worker, scheduled reconciliation and provider delivery-event handling;
- production OIDC/durable sessions, application images, remote CI/staging, managed recovery,
  performance, independent reviews and production greenlight.

## Current local verification - 2026-09-01

- Provider mode, resolver, deterministic adapter and fail-closed Resend behavior: PASS, 14/14.
- Verified reply, disabled-mode, confirmed-rejection, operator/idempotency, persisted-processing,
  restart/interruption and retained delivery-durability workflow cases: PASS, 11/11.
- Complete Release API suite after OpenAPI regeneration, planning fixture-clock correction and
  Docling redirect hardening: PASS, 128/128 in 3m57s.
- Web lint, type-check, unit tests 6/6 and production build: PASS.
- Playwright desktop/compact matrix: PASS, 32/32. These tests intercept API traffic and are
  UI/contract regressions, not genuine connected deployed-system E2E evidence.
- Deterministic agent runtime: Ruff PASS and pytest 28/28.
- Compose definition and dependency health: PASS, six local services healthy.
- Evidence manifest contract: PASS, 1/1.
- Complete current-source secret scan: PENDING the combined root verification; bounded earlier
  source scans are retained but are not substituted for the current-tree result.
- Combined final-tree architecture suite: PENDING the root verification after all current evidence
  and build-input changes settle.

Repeatable corrective evidence is retained under
`docs/evidence/gate12-email-automation-security-recovery-20260901/`. The historical 2026-08-31
delivery-durability pack remains unchanged except for its supersession notice.
