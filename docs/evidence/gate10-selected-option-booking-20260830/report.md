# Gate 10 selected-option booking evidence report

**Evidence date:** 2026-08-30

**Base commit:** `8dcb799ce726fcf8a4701694594870ab8253360f`

**Live provider or production resource used:** No

## Implemented

- Added one canonical booking aggregate derived only from a client-selected immutable
  ProposalOption and one exact marketplace-backed MediaPlanLine.
- Snapshotted buyer/supplier tenants, proposal/plan/line and marketplace lineage, schedule,
  quantity, supply identifiers, supplier amount, buyer price, fees, VAT, currency, terms,
  commercial-policy version and booking threshold.
- Enforced `DRAFT -> PENDING_SUPPLIER -> CONFIRMED` in PostgreSQL. The first transition requires
  the authorised buyer and the second requires the addressed supplier, accepted frozen terms and
  exact optimistic version.
- Revalidated the current published listing, rate dates and availability before each consequence.
  Withdrawn or changed supply and changed commercial policy return review-required without
  mutating the booking.
- Added idempotent commands, audit/outbox evidence, forced cross-party RLS, immutable commercial
  snapshots and duplicate-confirmation protection.
- Added authenticated `/bookings` buyer/supplier screens. Buyer pricing and internal
  proposal/plan lineage are masked from supplier responses.

## Verification

| Check | Result |
|---|---|
| Release API build | PASS - zero warnings/errors |
| Complete C# suite | PASS - 63/63 |
| Selected-option booking journey | PASS - client selection, buyer request and addressed-supplier confirmation |
| Negative booking journey | PASS - missing policy, withdrawn supply, wrong supplier, duplicate confirmation and unaccepted terms fail closed |
| Migration cycle and forced RLS | PASS - covered by the complete disposable-PostgreSQL suite |
| Retained OpenAPI | PASS - generated contract matches the running API |
| Architecture | PASS - 23/23 |
| Agent runtime | PASS - Ruff and 26/26 |
| Web static/unit/build | PASS - lint/build and 4/4 |
| Browser journeys | PASS - 24/24 desktop/compact |
| Compose validation | PASS |

The booking journey also proves unrelated-tenant reads are denied, a supplier cannot see the
buyer's client price, fee, VAT, proposal, decision, plan or plan-line identifiers, and a direct
commercial snapshot mutation is rejected by the database trigger.

## Corrected findings

1. Marketplace response acceptance did not establish a client-selected proposal line and could
   not safely create a supplier commitment. Booking now starts only from the selected immutable
   proposal option and its exact current marketplace-backed plan line.
2. A mutable application-only lifecycle check would leave a direct database update path. The
   allowed buyer and supplier transitions, immutable columns and actor/tenant checks are also
   enforced by the database.
3. A supplier-facing shared projection could disclose buyer pricing and internal proposal lineage.
   Those fields are now nullable and emitted only in the buyer tenant context.
4. A policy or supply change after draft creation could otherwise silently reprice or confirm stale
   media. Requests now fail review-required and require a new plan/proposal path.

## Remaining boundary

This packet does not create a purchase order, invoice, payment, campaign, creative, publication,
supplier email/system call or production deployment. Gate 11 campaign activation/readiness remains
unimplemented. Owner approval and independent security/privacy, commercial and operations review
remain pending. No push or deployment was performed.
