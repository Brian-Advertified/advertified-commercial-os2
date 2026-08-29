# Proposal and client decision work packet

**Recorded:** 2026-08-29  
**Authorised capability:** Proposal/client-decision local delivery under standing sequential direction  
**Prerequisite:** Gate 7 canonical planning committed as `0fab89b028a70af186985c670b0f5c9b2eefaf14`  
**Production/live-provider authority:** None

## Bounded requirement

Turn one approved Brief and one to three genuinely different approved `MediaPlanVersion` records into an immutable, client-facing `ProposalVersion`, named proposal options, an approved branded PDF deliverable and an audited client decision. The agency operator prepares and internally approves the exact proposal. The first mandatory advertiser/client action is selecting or declining the client-visible proposal.

Platform packages Launch, Boost, Scale and Dominance remain separate master data and are not proposal-option names.

## Included vertical slice

1. Generate a draft ProposalVersion only from approved plans belonging to the same approved Brief. The requested plan IDs are unique and exact.
2. A proposal contains one to three options. When more than one option is supplied, options must be materially different: a duplicate plan reference is invalid, and the bound approved plans must differ by total budget or selected inventory/quantity/flighting signature. No superficial copy with changed wording only is allowed.
3. Each option stores its own client label, outcome statement, exact approved plan ID/version, budget, currency, channels, running periods and inventory summary. Supplier cost/margin is not exposed to the client surface.
4. Proposal narrative is generated from structured approved facts only. The deterministic local narrative provider has zero incremental cost and cannot invent reach, performance, discounts, supply or terms.
5. Draft proposal editing is limited to client wording, option labels/outcomes, terms and expiry. Bound plan IDs/pricing are immutable within that version. Changing the commercial route requires a new ProposalVersion.
6. Submission/approval checks exact plan freshness and approval status again. A changed/superseded plan blocks approval.
7. Rendering is deterministic and runs only after internal approval. Persist an immutable branded PDF document record with content hash, media type and exact proposal version. Re-rendering the same approved version must produce the same bytes/hash.
8. Local proposal delivery is provider-neutral. No live email or external network send is authorised. The local deterministic delivery adapter records a successful delivery to the verified client recipient and then permits the proposal to enter SENT. Production delivery remains disabled until separately configured/reviewed.
9. A client/advertiser actor may read a SENT proposal and either select exactly one non-expired option or decline it. The decision is append-only and changes the proposal lifecycle to SELECTED or DECLINED.
10. Selecting an option records the exact selected proposal-option ID and approved plan ID, but does not create bookings, invoices, payments, POs or supplier commitments. Those remain later gates.
11. Provide authenticated browser flows for agency proposal preparation/approval/render/local-share and client option comparison/selection, with loading, expired, stale, unavailable-document, forbidden and declined states.
12. Proposal UI remains premium and uncluttered: concise executive summary, up to three option cards, channel/running-period visuals, inventory summary, assumptions/terms behind disclosure, PDF preview/download, and one context-correct primary action.

## Explicitly excluded

- Platform package selection or checkout semantics.
- RFQ/supplier communication, booking, purchase orders, invoices, payments or campaign creation.
- Live email/SMS/WhatsApp or other external send.
- Live/paid Proposal Narrative model calls.
- Invented reach, impressions, performance, discounts, supply or commercial terms.
- Shared/local canonical DB deployment, cloud changes, push, merge or production resources.

## Acceptance evidence

- Disposable PostGIS PostgreSQL journey proves three distinct approved plans → draft proposal → approval → deterministic PDF → local deterministic delivery → advertiser selects exact option.
- Duplicate plan option, cross-Brief plan, unapproved plan, stale plan, expired proposal, cross-tenant read, unauthorised client decision and double decision fail closed.
- Option totals equal the exact referenced approved plan totals; plan IDs and versions are retained.
- Document hash is stable for the same approved proposal version and the rendered PDF begins with a valid PDF signature.
- Client-visible API/browser data never includes supplier-cost/margin fields.
- Audit/outbox/idempotency bind exact proposal version and decision actor.
- Complete C# tests, architecture guardrails, runtime tests, web lint/unit/build, Playwright and retained OpenAPI contract pass.
- Complete diff contains no secrets, gate-number business classes, hard-coded governed commercial codes, god files or unrelated product changes.

## Stop conditions

Stop only an affected external action if it requires a live provider, production resource, unverified recipient or missing authority. Safe local implementation and verification continue without repetitive approval pauses.
