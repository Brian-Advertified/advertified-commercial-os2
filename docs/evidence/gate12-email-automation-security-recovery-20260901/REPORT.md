# Gate 12 email automation security and recovery evidence

**Evidence date:** 2026-09-01

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this is not a release artefact

**Live provider or production resource used:** No

**Supersedes:** `../gate12-email-delivery-durability-20260831/` for current security and recovery
claims. The earlier pack remains historical evidence of its dated implementation and test results.

## Corrected

- Made `EmailAutomation.Mode=Disabled` a fail-closed gate for configuration, provider resolution,
  reconciliation and provider send. An active mode resolves only its matching provider.
- Added a typed provider-owned inbound identity assessment and reply-address policy. Deterministic
  local fixtures can be trusted; Resend remains unverified and cannot auto-send without an official
  sender-authentication contract. Source email data is preserved even when delivery is denied.
- Kept provider-confirmed rejection distinct from ambiguous provider acceptance. A confirmed
  rejection is `DELIVERY_FAILED`; timeout, transport, server and malformed-success outcomes remain
  ambiguous and cannot trigger a blind resend.
- Persisted authenticated `:process`/`:retry` command provenance and idempotency before long work,
  retained the invoking operator as audit actor, and made command replay resume/read canonical state.
- Made `PROCESSING` runs resume from their persisted checkpoint. A host interruption after the
  durable delivery request reuses reconciliation and never issues a second send.
- Aligned list/detail browser presentation with governed failure codes and server-provided safe
  detail. Confirmed rejection, ambiguous acceptance, accepted-but-unrecorded and interrupted work
  have distinct copy and actions.

## Verification

| Check | Final result |
|---|---|
| Release API/test graph build | PASS - zero warnings and zero errors |
| Provider mode, resolver and provider-security focus | PASS - 14/14 |
| Email workflow security/recovery focus | PASS - 11/11 |
| Complete current-tree Release API suite | PASS - 128/128 in 3m57s |
| Web static checks, unit tests and production build | PASS - lint/type/build and unit 6/6 |
| Browser UI/contract matrix | PASS - 32/32 API-mocked Playwright checks |
| Deterministic agent runtime | PASS - Ruff and pytest 28/28 |
| Compose definition and dependency health | PASS - six local services healthy |
| Evidence manifest contract | PASS - 1/1 |
| Complete current-source secret scan | PENDING - owned by the root verification |
| Complete combined final-tree architecture suite | PENDING - owned by the root verification |

The 14 provider cases cover exact provider-mode resolution, disabled/mismatched denial,
deterministic stable receipt/reconciliation, Resend ambiguity, confirmed client rejection,
fail-closed identity assessment and zero-network reconciliation. The 11 workflow cases cover the
verified-reply boundary, disabled manual processing, confirmed rejection, operator/idempotency,
persisted `PROCESSING` recovery, true host interruption, accepted and unknown restart branches,
accepted-local-finalisation recovery, concurrency fencing and the normal deterministic OOH path.

The complete API suite initially passed 123/126 and therefore remained failed evidence. Two failures
were generated OpenAPI drift; one was a deterministic planning fixture using the machine clock for
rate eligibility. The retained OpenAPI contract was regenerated, and only the affected planning
journey hosts now use the fixture clock. A later Docling redirect-security test increased the final
suite denominator. The complete rerun then passed 128/128 in 3m57s.

The 32 Playwright checks intercept API responses. They verify browser presentation, actions and
contract-shaped states at desktop and compact viewports; they are not evidence of authentication,
database, API, worker and provider operating together in a deployed environment. No genuine
connected deployed-system E2E journey is claimed.

## Acceptance result

The focused and complete API evidence proves the six independently reported defects are corrected
in the deterministic local boundary:

1. Disabled or mismatched modes cannot reach provider actions.
2. Untrusted reply data is retained as evidence but cannot become an automatic delivery target.
3. Confirmed provider rejection remains failed while ambiguous acceptance remains review-required.
4. Manual processing records the actual operator and one idempotent command consequence.
5. A committed delivery request survives host interruption and resumes without another send.
6. Browser list/detail states use consistent governed codes and truthful recovery wording.

The current-tree architecture rerun and complete current-source secret scan are still pending.
Therefore this pack records implemented and API/web-tested corrections, not a complete Gate 12
exit, independent security approval or production readiness decision.

## Blocked before production

- Official Resend sender-authentication, delivery lookup/webhook reconciliation and sandbox proof
  are absent. No Resend request or other external communication was made.
- Inbound email processing still lacks a tenant-safe background worker, scheduled reconciliation
  and provider delivery-event handling.
- Production OIDC and durable sessions, genuine integrated E2E, application release images, remote
  CI, staging, central telemetry/alerts, managed recovery, performance/load evidence and named
  Operations ownership remain absent or unverified.
- Security, Privacy, Legal, Accessibility and independent production certification decisions remain
  pending. An AI cannot approve them.
- No commit, push, deployment, production mutation, live provider call or paid AI call was performed.
