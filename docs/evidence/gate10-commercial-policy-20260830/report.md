# Gate 10 commercial-policy evidence report

**Evidence date:** 2026-08-30

**Base commit:** `c1f1cebf7ace77a850ff790ec80b56b62affe75b`

**Live provider or production resource used:** No

## Implemented

- Added immutable, tenant-scoped commercial policy versions for markup, management fee, commission, VAT treatment/rate, price basis, currency and booking approval threshold.
- Added platform/agency administrator read/manage permissions, forced RLS, idempotent writes, optimistic concurrency, audit/outbox records and a human-safe not-configured state.
- Added exact checked minor-unit/basis-point calculation with discount-before-commission, exclusive/inclusive VAT and component reconciliation.
- Added the authenticated `/admin/commercial` screen, with no invented default policy, exact decimal conversion and desktop/compact coverage.
- Retained the API contract and generated master-data projections across C#, TypeScript and Python.

## Verification

| Check | Result |
|---|---|
| Release API build | PASS - zero warnings/errors |
| Complete C# suite | PASS - 61/61 |
| Architecture | PASS - 23/23 |
| Agent runtime | PASS - Ruff and 26/26 |
| Web lint/unit/build | PASS - lint/build and 4/4 |
| Browser journeys | PASS - 22/22 desktop/compact |

The database acceptance proves first write, idempotent replay, immutable next version, stale-write rejection, role denial, cross-tenant denial, complete rollback/reapply and forced-RLS coverage. Calculation tests cover discount ordering and a deterministic range of VAT-inclusive/exclusive totals.

## Additional defects corrected

1. Commercial policy creation needs an explicit expected version of zero. The browser request boundary previously omitted zero-valued `If-Match`; it now distinguishes zero from an absent precondition.
2. Marketplace response acceptance locked by response ID while supplier submission locked by RFQ ID. Both now serialize on the RFQ, and a concurrent duplicate-acceptance regression proves exactly one acceptance succeeds.
3. Gate evidence checks validated manifest fields but not the declared `workingTreeState` enum. Every discovered manifest is now checked, and two invalid historical values were corrected.

## Remaining boundary

This packet does not implement or approve a booking, supplier commitment, purchase order, invoice, payment, campaign delivery or production deployment. Gate 10 remains open until a booking is derived from the exact client-selected immutable proposal option and receives the required separate buyer/supplier human actions. Owner and independent reviews remain pending.
