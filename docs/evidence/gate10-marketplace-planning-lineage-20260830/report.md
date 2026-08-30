# Gate 10 marketplace-planning lineage evidence report

**Evidence date:** 2026-08-30

**Base commit:** `4e242852f83808df4007cede9124f8f2b48eb087`

**Live provider or production resource used:** No

## Implemented

- Projected only current published marketplace listing versions into a different buyer tenant's
  canonical media-planning inputs, including marketplace-only channels.
- Retained the inventory-owning tenant and exact marketplace listing version through shortlist,
  recommendation, benchmark, media-plan line and supply-coordination records.
- Stored buyer-owned product-name, channel and geography snapshots so buyer reads never join the
  supplier's private inventory tables.
- Included supplier/listing lineage in shortlist, plan and proposal signatures and current-input
  validation. Replacement, archival or a missing current projection fails closed as stale.
- Preserved the existing same-tenant inventory path and generated registry/OpenAPI projections.

## Verification

| Check | Result |
|---|---|
| Release API build | PASS - zero warnings/errors |
| Complete C# suite | PASS - 61/61 |
| Cross-tenant marketplace-to-plan journey | PASS - published listing became one exact buyer plan line; archival blocked approval as stale |
| Migration cycle and forced RLS | PASS - covered by the complete disposable-PostgreSQL suite |
| Architecture | PASS - 23/23 |
| Agent runtime | PASS - Ruff and 26/26 |
| Web lint/unit/build | PASS - lint/build and 4/4 |
| Browser journeys | PASS - 22/22 desktop/compact |
| Compose validation | PASS |

The cross-tenant response was checked for supplier-private source locator, address and coordinate
fields. Only the immutable marketplace projection and required supplier/listing identifiers crossed
the boundary. No buyer access to supplier-private inventory tables was added.

## Corrected findings

1. Buyer media-mix generation previously discovered channels only from buyer-owned inventory. A
   buyer relying solely on published marketplace supply could not begin planning; the governed
   channel projection now includes current published marketplace listings.
2. Planning persistence previously assumed the inventory owner was always the buyer tenant. The
   migration now uses explicit inventory/supplier tenant foreign keys while retaining buyer tenancy
   on canonical planning records.
3. Shortlist and plan readers previously joined mutable/private inventory versions for display.
   They now read immutable buyer-owned snapshots, preventing a cross-tenant read grant or leakage.
4. Plan and proposal staleness keys previously omitted the inventory owner and listing version.
   Exact marketplace provenance now participates in all relevant hashes and validation keys.

## Remaining boundary

This prerequisite does not create a booking, supplier commitment, purchase order, invoice, payment,
campaign or external communication. Gate 10 remains open until a booking is derived from the exact
client-selected immutable proposal option and receives separate authorised buyer and supplier human
actions. Owner and independent security/privacy/commercial/operations reviews remain pending. No
push or deployment was performed.
