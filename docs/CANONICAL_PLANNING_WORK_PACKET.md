# Canonical planning work packet

**Recorded:** 2026-08-29  
**Authorised capability:** Canonical planning under Brian Rabuthu's standing sequential local-delivery direction  
**Prerequisite:** Published inventory truth plus the versioned Docling extraction boundary  
**Production/live-provider authority:** None

## Bounded requirement

Turn one exact approved `BriefVersion` into an evidence-classified audience set, an approved
budget-reconciling media mix, an eligible inventory shortlist with retained rejections and
reproducible OOH/DOOH benchmarks, and an approved immutable `MediaPlanVersion`. One assigned
agency operator may prepare and approve these internal planning artefacts. No advertiser approval
is introduced before the Proposal / Client Decision boundary.

## Included vertical slice

1. Generate deterministic typed audience definitions from the approved brief. Preserve facts,
   labelled inferences, hypotheses, exclusions, unknowns, evidence bindings and confidence.
2. Generate channel roles and allocations that exactly reconcile to the approved planning budget;
   reject briefs whose budget is unknown or whose currency is absent.
3. Present the media mix as an editable planning workspace before approval. The assigned operator
   can change each channel allocation, channel role and one or more channel-specific running periods.
   Allocations must continue to reconcile exactly to the approved planning budget; periods must be
   valid, non-overlapping within a channel and remain inside any hard Brief timing constraint that is
   structurally known. The UI uses a distinct accessible colour plus a recognisable media-type
   icon/logo treatment for each channel and renders the independent running periods on a timeline.
   Draft edits use optimistic concurrency. Once a mix is approved it is immutable; a later change
   creates a new draft version and materially affected downstream shortlist/plan artefacts become stale.
4. Approve the exact media-mix version with tenant, assignment, version, idempotency and audit
   enforcement. The creator may approve when they are the assigned agency operator.
5. Evaluate every current published inventory/rate/availability tuple against hard geography,
   channel, currency, price-freshness and supply constraints before scoring. Persist both eligible
   candidates and every rejected product with governed reason codes and detail.
5. For OOH/DOOH candidates, calculate an immutable benchmark snapshot from exact compatible
   published product/rate versions. Retain cohort IDs, exclusions, geography basis, median,
   quartiles, percentile, market position, freshness and confidence. AI does not perform maths.
6. Record operator selection decisions against the exact shortlist version. Ineligible inventory
   cannot be selected.
7. Generate plan lines only from selected eligible candidates and exact rate/availability records.
   Calculate supplier cost, configured brief fees, VAT and totals deterministically in minor units.
   Persist supply source, observed/expiry times and uncertainty without presenting unknown supply
   as confirmed.
8. Attach an immutable critic report. Material objections require an explicit operator resolution
   before the exact plan version can be approved. Changed rate/availability inputs make the draft
   stale and block approval.
9. Provide tenant-safe versioned API and authenticated browser flows from approved Brief to
   approved plan, including loading, empty, error, rejection, uncertainty and evidence states.

## Explicitly excluded

- Proposal generation, tiers, rendering, sending or client decision.
- RFQs, supplier communication, booking, spend, payment or publication.
- Live/paid AI or audience-provider calls; deterministic local agent fixtures exercise the typed
  contracts with zero incremental provider cost.
- Claims of reach, impressions, audience size, availability or performance not supplied by
  approved evidence.
- Shared/local canonical database migration, deployment, push or merge.

## Acceptance evidence

- Disposable PostgreSQL journey proves approved Brief → audience → mix → approval → shortlist →
  selection → plan → critic resolution → approval for a single assigned agency operator.
- Unknown budget, unapproved brief, allocation mismatch, stale rate/availability, ineligible
  selection, unresolved critic objection, cross-tenant access and optimistic-concurrency conflict
  all fail closed with stable problem codes.
- Every considered inventory product has a persisted eligibility outcome and exact version refs;
  OOH/DOOH selected candidates have reproducible immutable benchmark snapshots.
- Plan line arithmetic reconciles exactly in minor units, including fees and VAT; supply status
  exposes source and uncertainty.
- Audit/outbox/idempotency evidence binds actor, tenant, action, resource and exact version.
- Release builds, complete C# tests, architecture checks, web lint/type/tests/build, browser
  journey, OpenAPI contract and Compose validation pass with retained commands/results.
- Complete diff contains no unrelated changes or secrets; all authored file/function limits pass.

## Stop conditions

Stop only an affected action if it requires an unapproved external effect, live provider, invented
commercial fact, missing tenant/assignment authority, or a materially ambiguous owner decision.
Safe local implementation and verification continue without repetitive approval pauses.
