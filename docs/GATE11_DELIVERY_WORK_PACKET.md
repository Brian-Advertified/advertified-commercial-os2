# Gate 11 work packet — funding, delivery and learning

**Owner direction:** 2026-08-30 fix remaining issues, commit coherent packets and continue toward production.

**Verified prerequisite:** local commit `c2b3e4e`; Gate 10 selected-option booking is implemented and
verified locally with Release API 63/63, architecture 23/23, runtime 26/26 and browser 24/24.
Gate 10 remains subject to owner and independent review; local evidence is not a production approval.

**Packet A result:** implemented and verified locally on 2026-08-30; reproducible evidence is
retained under `docs/evidence/gate11-funding-governance-20260830/`. Campaign activation remains
blocked until the later Gate 11 packets are implemented and verified.

## Bounded requirement

Implement the normative Gate 11 lifecycle in sequential local-only packets. The C# Commercial API
owns every commercial state change. A browser or Python agent may never confirm funding, approve
creative, start delivery, accept proof or create performance truth.

## Packet A — funding governance

- submit one signed purchase-order evidence reference against the exact client-selected immutable
  ProposalOption and ProposalDecision;
- reconcile PO amount/currency/option before a different authorised commercial reviewer approves it;
- issue an immutable invoice only from that approved PO, deriving subtotal, fees, VAT and total from
  the selected MediaPlanVersion rather than accepting calculated amounts from the browser;
- start only the locally supported `MANUAL_EFT` payment method from the issued invoice;
- require a different authorised human to reconcile immutable receipt evidence before payment becomes
  `CONFIRMED`; provider receipts must never be invented;
- enforce tenant isolation, creator/approver separation, optimistic concurrency, idempotency,
  append-only audit/outbox consequences and immutable commercial/evidence snapshots;
- expose tenant-scoped read and command contracts with deterministic API acceptance evidence.

**Packet A exit evidence:** an accepted option progresses through submitted PO, separate approval,
issued invoice, pending manual EFT and separately reconciled confirmed payment. Wrong amount,
creator self-approval/reconciliation, unsupported provider methods, duplicate commands and unrelated
tenant access fail closed without mutation.

## Later packets

1. Campaign creation and `PLANNED → BOOKED` only when funding is confirmed and all required bookings
   for the selected option are confirmed.
2. Versioned creative requirements/assets and human approval through `CREATIVE_PENDING → READY`.
3. Human-authorised `READY → LIVE → COMPLETED`, immutable delivery proof, sourced performance facts,
   measurement interpretation and client report.
4. Desktop and compact E2E-08 journey plus hardening/certification hand-off.

## Explicitly blocked or out of scope

- live VodaPay, pay-later credit decisions, bank verification, payment collection, refund or settlement;
- supplier/client communication, autonomous commercial approval, booking or campaign launch;
- production data, credentials, deployment, cloud mutation or live/paid AI calls;
- campaign activation before Packet A evidence exists;
- legal, finance, security, privacy, operations or production-readiness approval by the implementing AI.

## Acceptance evidence

1. All amounts use integer minor units; invoice components and PO/payment totals exactly reconcile to
   the client-selected plan and currency.
2. Every transition names its human actor and exact immutable input/evidence; consequential creators
   cannot approve or reconcile their own work.
3. Database constraints and forced RLS independently reject cross-tenant association, mutation and
   enumeration.
4. Every accepted command produces one correlated audit event and outbox message; replay is
   idempotent and a changed payload conflicts.
5. Release build, affected/full API tests, architecture checks, retained OpenAPI and credential/diff
   checks pass with reproducible evidence.
