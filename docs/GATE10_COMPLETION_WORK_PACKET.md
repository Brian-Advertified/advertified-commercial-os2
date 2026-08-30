# Gate 10 completion work packet — commercial policy and booking confirmation

**Owner direction:** 2026-08-30 fix remaining issues, commit coherent packets and continue toward production.

**Verified prerequisite:** local commit `c1f1ceb`; Release API build has zero warnings/errors, API 56/56, deterministic runtime 26/26 and architecture 23/23.

**Packet A result:** implemented and verified locally on 2026-08-30; reproducible evidence is retained under `docs/evidence/gate10-commercial-policy-20260830/`. Packet B remains open.

**Packet B0 result:** implemented and verified locally on 2026-08-30; reproducible evidence is
retained under `docs/evidence/gate10-marketplace-planning-lineage-20260830/`. The selected-option
booking and supplier-confirmation workflow remains open.

## Bounded requirement

Complete the remaining local Gate 10 supplier-marketplace scope with one versioned tenant commercial policy and a booking-confirmation workflow derived only from a client-selected immutable ProposalOption/MediaPlan line. The Commercial API remains canonical. No command may create a supplier commitment from a marketplace response alone.

## Packet A — versioned commercial policy

- immutable tenant policy versions for markup, management fee, VAT treatment/rate, commission and booking-approval threshold;
- one current effective version per tenant, exact integer basis points/minor units and explicit currency;
- platform/agency administrators may read and create a new version for their authorised tenant; campaign users and supplier roles cannot mutate buyer policy;
- calculation service proves discount-before-commission and exact subtotal/fee/VAT/total reconciliation without floating point;
- authenticated `/admin/commercial` read/edit surface with concurrency and human-safe validation;
- migration, RLS, master-data, OpenAPI, API and browser evidence.

## Packet B — selected-option booking

### Packet B0 — canonical marketplace-to-plan lineage

The post-Packet-A audit found that buyer planning rows can reference only buyer-tenant inventory,
while every marketplace listing is supplier-tenant inventory. Before a supplier-addressed booking
can be implemented, the canonical planning path must retain the inventory-owning tenant and exact
published listing version.

- include only the current `PUBLISHED` marketplace listing projection in buyer shortlist inputs;
- retain `inventory_tenant_id` and `marketplace_listing_version_id` through shortlist candidate,
  recommendation, benchmark, MediaPlanLine and supply-coordination records;
- read product name/channel/geography from immutable buyer-owned planning snapshots, never by
  granting the buyer access to the supplier's private inventory tables;
- treat an archived/replaced listing, changed rate/availability or missing projection as stale;
- preserve the existing local tenant inventory path and prevent duplicate own-listing candidates;
- prove supplier-private source, review, address and coordinate fields do not cross the boundary.

**B0 exit evidence:** a supplier publishes a reviewed listing; a different buyer tenant sees it in
the canonical shortlist, selects it and generates an exact plan line carrying the supplier tenant
and listing-version IDs; replacement/archival invalidates the line without mutation.

- a booking draft can be created only from the exact selected ProposalOption and one of its frozen MediaPlanLines;
- snapshot exact proposal/option/plan/line/product/rate/availability IDs, supplier tenant, dates, quantity, supplier amount, currency, VAT/fee terms and policy version;
- stale/missing availability, changed rate/date/price, unselected line, wrong supplier, wrong tenant or missing commercial policy fails closed;
- `DRAFT → PENDING_SUPPLIER` requires an explicit authorised buyer command; `PENDING_SUPPLIER → CONFIRMED` requires the addressed supplier's explicit human confirmation and accepted terms;
- material supplier changes do not mutate the booking; they return review-required and require a new proposal/version flow;
- list/detail views expose only counterpart-safe snapshots; no private inventory source/review data;
- idempotency, optimistic concurrency, audit/outbox, RLS and duplicate-confirmation tests;
- authenticated buyer/supplier desktop and compact journey.

## Out of scope and blocked

- purchase orders, invoices, payment intents, holding client funds or live payment providers;
- campaign creation, creative, delivery proof, performance and measurement (Gate 11);
- live supplier email/system calls, production deployment/data or cloud mutation;
- automatic booking, substitution, repricing, acceptance or supplier response;
- ADR-0011 approval or any production-readiness decision.

## Acceptance evidence

1. Commercial calculations use integer minor units/basis points and property/edge tests; all components reconcile exactly.
2. Every booking binds the selected immutable option and exact current supply inputs; no free-floating or marketplace-response-only booking exists.
3. Only the buyer may request confirmation and only the addressed supplier may confirm; unrelated tenants cannot enumerate either side.
4. Each external-consequence transition requires a named human, exact expected version, idempotent command and audit/outbox event.
5. No live provider, supplier contact, payment, booking side effect outside the local database, or production resource is used.
6. Migration apply/reapply/RLS/rollback, Release API/full tests, architecture, web static/unit/build and affected browser journeys pass.
