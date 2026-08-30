# Gate 11 work packet — funding, delivery and learning

**Owner direction:** 2026-08-30 fix remaining issues, commit coherent packets and continue toward production.

**Verified prerequisite:** local commit `c2b3e4e`; Gate 10 selected-option booking is implemented and
verified locally with Release API 63/63, architecture 23/23, runtime 26/26 and browser 24/24.
Gate 10 remains subject to owner and independent review; local evidence is not a production approval.

**Packet A result:** implemented and verified locally on 2026-08-30; reproducible evidence is
retained under `docs/evidence/gate11-funding-governance-20260830/`. Campaign activation remains
blocked until the later Gate 11 packets are implemented and verified.

**Packet B result:** implemented and verified locally on 2026-08-30; reproducible evidence is
retained under `docs/evidence/gate11-campaign-booking-readiness-20260830/`. Confirmed funding now
creates the exact planned Campaign, booking cannot precede it, and only complete confirmed booking
coverage permits the buyer-side transition to `BOOKED`. Creative and live delivery remain blocked
until the later Gate 11 packets are implemented and verified.

**Packet C result:** implemented and API-verified locally on 2026-08-30; reproducible evidence is
retained under `docs/evidence/gate11-creative-readiness-20260830/`. Exact booked-format requirements,
protected versioned files, separate buyer and supplier review, and client readiness approval are
implemented. Repository-wide architecture checks pass; web browser verification remains pending
integration of separately owned screen work. Live delivery remains blocked until later Gate 11 packets.

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

### Packet B — campaign creation and booking readiness

- confirmed payment deterministically creates one `PLANNED` Campaign for the exact selected option;
- booking creation now requires that confirmed funding and planned Campaign, preserving the canonical
  Funding → Booking order;
- supplier booking confirmation remains isolated to the supplier tenant and emits its canonical
  event; it never obtains buyer-tenant mutation rights;
- an authorised buyer-side `ConfirmBookings` command advances `PLANNED → BOOKED` only when every
  MediaPlanLine in the selected option has one exact `CONFIRMED` Booking;
- campaign creation and booking readiness have their own audit/outbox consequences even when campaign
  creation is an atomic consequence of payment confirmation;
- list/detail projections show exact source versions, funding state, required/confirmed counts,
  delivery dates and next action without exposing evidence object keys.

**Packet B exit evidence:** payment confirmation creates exactly one planned Campaign; booking is
impossible before confirmed funding; partial, draft or pending bookings cannot advance it; all exact
confirmed bookings allow one idempotent buyer-side transition to `BOOKED`; unrelated tenants and
suppliers cannot enumerate or mutate the Campaign.

### Packet C — booked-format creative production and readiness

- an authorised buyer request advances `BOOKED → CREATIVE_PENDING` only when it supplies exactly one
  immutable format requirement for every confirmed Booking in the Campaign;
- every requirement is bound to the exact Booking, MediaPlanLine, supplier, delivery dates and
  Campaign source versions; proposal-stage concepts can never satisfy it;
- creative files are signature-checked, malware-scanned, hashed and stored under tenant-scoped
  protected object keys that are never returned by the API;
- each replacement is a new immutable CreativeAsset version; the API derives the commercial snapshot
  from the Booking and never accepts price truth from the browser;
- buyer brand/legal/rights review and supplier technical review are separate, named, append-only human
  decisions against the exact current file version;
- only an authorised client approver may advance `CREATIVE_PENDING → READY`, and only when every exact
  requirement has a current version with approved rights, brand/legal review and supplier review;
- database triggers and forced RLS independently enforce lifecycle, tenant, current-version, actor and
  review boundaries while supplier projections exclude buyer copy, commercial and storage details.

**Packet C exit evidence:** missing/duplicate booking requirements, unsafe or mismatched files, old
version approvals, unauthorised reviewers, supplier access to buyer Campaign data, incomplete reviews
and direct invalid lifecycle changes fail closed without advancing readiness. A replacement version
invalidates prior reviews; exact current-version approvals permit one idempotent transition to `READY`.

### Later Gate 11 packets

1. Human-authorised `READY → LIVE → COMPLETED`, immutable delivery proof, sourced performance facts,
   measurement interpretation and client report.
2. Desktop and compact E2E-08 journey plus hardening/certification hand-off.

## Explicitly blocked or out of scope

- live VodaPay, pay-later credit decisions, bank verification, payment collection, refund or settlement;
- supplier/client communication, autonomous commercial approval, booking or campaign launch;
- production data, credentials, deployment, cloud mutation or live/paid AI calls;
- creative production or campaign launch before the later Gate 11 evidence exists;
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
