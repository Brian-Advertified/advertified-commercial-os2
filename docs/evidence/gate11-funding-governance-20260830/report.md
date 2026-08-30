# Gate 11 funding-governance evidence report

**Evidence date:** 2026-08-30

**Base commit:** `c2b3e4e6f343a963b658adbfca103a92efab8430`

**Live provider or production resource used:** No

## Implemented

- Added the canonical PurchaseOrder, Invoice and PaymentIntent records and routes in the C#
  Commercial API.
- Bound every funding record to the exact client-selected ProposalOption, ProposalDecision and
  approved MediaPlanVersion.
- Derived invoice subtotal, fees, VAT and total from canonical plan data. Browser requests cannot
  supply invoice calculations.
- Protected signed PO and receipt bytes with file signature, size, SHA-256 and malware checks, then
  stored them under tenant-scoped immutable object keys without exposing keys in API projections.
- Enforced a separate human PO approver and payment reconciler. Only deterministic local manual EFT
  is enabled; VodaPay and pay-later routes fail closed.
- Added forced RLS, cross-tenant foreign keys, immutable snapshots, database lifecycle triggers,
  optimistic concurrency, idempotency and correlated audit/outbox events.

## Verification

| Check | Result |
|---|---|
| Release API build | PASS — zero warnings/errors |
| Complete C# suite | PASS — 64/64 |
| Funding journey | PASS — selected option → submitted PO → separate approval → invoice → pending manual EFT → separate reconciliation |
| Funding negatives | PASS — wrong amount, unassigned approval/reconciliation, provider method and unrelated tenant fail closed |
| Migration and forced RLS | PASS — all 71 protected tables verified in disposable PostgreSQL |
| Retained OpenAPI | PASS — generated contract matches the running API |
| Architecture | PASS — 23/23 |
| Agent runtime | PASS — Ruff and 26/26 |
| Web regression | PASS — lint/build, 4/4 unit and 24/24 desktop/compact journeys |
| Compose validation | PASS |

The database independently rejects funding rows that do not reconcile to the selected option,
non-current lifecycle states, a creator approving/reconciling their own record, commercial snapshot
mutation, invoice mutation and cross-tenant access. Five accepted commands produce exactly five
funding audit events and five outbox messages.

## Corrected findings

1. The capability ledger still described Gate 10 booking as incomplete. It now reports the retained
   local evidence and truthfully marks Gate 11 in progress.
2. Funding provider codes existed without a safe activation boundary. Only manual EFT can start;
   provider and credit methods return a stable unavailable result.
3. Application checks alone would leave a direct database shortcut. Purchase-order submission and
   approval, invoice issuance and payment start/reconciliation are now also guarded by PostgreSQL.
4. Returning internal object keys would disclose storage topology. Public funding projections now
   expose evidence hashes and metadata, never the protected key.

## Remaining boundary

This packet does not collect, hold, refund or settle money and does not call a bank, VodaPay, credit
provider or external party. Campaign creation, readiness, creative, live delivery, proof,
measurement and learning remain unimplemented Gate 11 packets. Owner approval and independent
finance, legal, security/privacy and operations review remain pending. No push or deployment was
performed.
