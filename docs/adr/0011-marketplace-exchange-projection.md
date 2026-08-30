# ADR-0011: Tenant-safe marketplace exchange projection

## Status

Proposed implementation detail; the normative Gate 10 marketplace contract already applies.

## Context

Supplier inventory truth is tenant-private, while buyers must discover published snapshots and exchange RFQs with the supplier that owns each snapshot. Giving buyers access to supplier inventory tables would expose private source and review history. Treating an RFQ as owned by only one tenant would prevent the counterparty from reading the exact exchange.

## Decision owner and reviewers

| Responsibility | Actual name | Decision/date |
|---|---|
| Accountable owner | Brian Rabuthu | Pending |
| Engineering/data reviewer | Not required for reversible local implementation | Independent review before publication |
| Security/privacy reviewer | Not required for reversible local implementation | Independent review before publication/production |

## Proposed decision

- Published marketplace listing versions are immutable, minimal projections of exact reviewed product, rate and availability versions.
- A buyer can read a published projection but never the supplier's source file, candidate review, private inventory row or object key.
- RFQs and responses retain both buyer and supplier tenant IDs. PostgreSQL policies grant read access only to those counterparties.
- Buyer-authored RFQs, supplier-authored responses and buyer-authored acceptances are separate tables so row-level security can enforce write ownership without trusting application-only column checks.
- Every consequence remains an authenticated, permissioned, idempotent, exact-version command with audit and outbox evidence.
- The local `send` transition exposes the request inside Advertified only. Live email, booking, payment and supplier-system mutation remain outside this decision.

## Consequences

The projection duplicates only the commercial facts required for discovery and exact-version exchange. Inventory truth remains canonical, and a changed product/rate/availability creates a new listing version. Cross-tenant access is explicit and testable rather than a broad bypass.

## Verification

- unpublished and archived listings are invisible outside the supplier tenant;
- buyer discovery reveals no private source or review fields;
- unrelated tenants cannot enumerate RFQs or responses;
- only the buyer can create/send/accept and only the addressed supplier can respond;
- duplicate/stale commands fail closed and every applied command records audit/outbox evidence.

## Decision record

- Proposed by: implementation agent for Brian Rabuthu
- Proposed date: 2026-08-30
- Accepted/rejected by: pending owner decision
- Decision date: pending
- Supersedes/superseded by: none
