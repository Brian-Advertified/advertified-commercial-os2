# Supplier marketplace delivery packet

**Normative scope:** specification Sections 7.4, 18.4–18.5, 20.4, 21.4, 24 and 26

**Dependency:** verified inventory truth, planning, proposal and Gate 9 canonical OOH-only campaign-mode capabilities

**External boundary:** local deterministic send/response records only; no live supplier email, booking, payment or production action

## Outcome

A supplier can publish exact versions of its reviewed canonical inventory into a discoverable marketplace, refresh rates and availability through append-only inventory observations, receive RFQs, and submit attributable commercial responses. A buyer can discover only published marketplace snapshots, issue an RFQ, review the response and accept the exact response version. Supplier silence and expiry remain visible; no response is fabricated.

## Architectural boundary

Marketplace discovery is an explicit projection. It does not grant buyers direct database access to another tenant's private inventory, source files or review history. A listing version binds the supplier tenant, supplier, product version, rate and availability IDs that were current when published. Changes create a new version.

RFQs are exchange records with both buyer and supplier tenant identities. Database policy permits only those two tenants to read the exchange. Application permissions and ownership checks decide which party may create, send, respond or accept.

## In scope

- supplier-owned draft marketplace listings from reviewed canonical products;
- immutable listing versions and exact rate/availability references;
- publish/archive lifecycle with freshness and terms;
- authenticated marketplace search over active snapshots;
- buyer RFQ create/send lifecycle;
- supplier inbox and one attributable response per RFQ version;
- buyer acceptance of an unexpired response;
- explicit pending/overdue/expired states without invented supplier behavior;
- tenant, supplier-ownership and role-negative tests;
- plain-language desktop/compact browser journeys;
- audit/outbox, idempotency and optimistic concurrency.

## Out of scope

- live external email or supplier portal federation;
- purchase orders, invoicing, payment, booking or campaign delivery;
- buyer access to source inventory files or private supplier evidence;
- automatic response, acceptance or substitution;
- cross-tenant write permissions outside the explicit exchange record;
- supplier-specific hard-coded fields or workflows.

## Exit evidence

- disposable PostgreSQL migration with RLS and cross-tenant negatives;
- supplier publish → buyer discovery → RFQ send → supplier response → buyer acceptance journey;
- stale/expired response and wrong-supplier denial;
- API/OpenAPI, architecture, browser lint/build and desktop/compact Playwright;
- zero live-provider, zero spend and zero incremental-AI-cost proof;
- dedicated clean commit.
