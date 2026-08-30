# Gate 11 booked-format creative-readiness evidence report

**Evidence date:** 2026-08-30

**Base commit:** `00321faec3a3527cc88e516f2095655f6e4ab7ed`

**Live provider or production resource used:** No

## Implemented

- Added exact, immutable creative requirements derived from every confirmed Campaign Booking and
  advanced the canonical Campaign from `BOOKED` to `CREATIVE_PENDING` only when coverage is exact.
- Added signature-checked, malware-scanned, hashed and protected creative file versions. Object keys
  never appear in buyer or supplier API projections, and commercial snapshots are server-derived.
- Added separate buyer brand/legal/rights review and supplier technical review against the exact
  current file version. Replacement files invalidate prior review sufficiency without rewriting history.
- Allowed only an authorised client approver to advance `CREATIVE_PENDING` to `READY`, and only after
  every current version has both required approvals and approved rights.
- Added six commands and one query route, governed permissions/actions/events, optimistic concurrency,
  idempotency, audit/outbox records, forced RLS and database lifecycle/immutability triggers.

## Verification

| Check | Result |
|---|---|
| Release API build | PASS - zero warnings/errors |
| Complete C# suite | PASS - 64/64 |
| Creative production journey | PASS - exact requirements, replacement version, separate reviews and client approval reach `READY` |
| Negative boundaries | PASS - missing requirements, unsafe/mismatched files, stale versions, wrong actors, premature readiness and direct lifecycle mutation fail closed |
| Migration and forced RLS | PASS - all 76 protected tables verified in disposable PostgreSQL |
| Retained OpenAPI | PASS - generated contract matches the running API and version headers are asserted |
| Master data and packet architecture checks | PASS - generated registry 2.6.0 is current; 14 focused security/contract checks passed |
| Complete architecture suite | PASS - 23/23 |
| Agent runtime | PASS - Ruff and 26/26 deterministic tests |
| Web regression | BLOCKED - master-data, lint, 4/4 unit and build pass; 20/24 browser tests pass while another agent actively changes screen wording and controls |
| Compose validation | PASS |

## Corrected findings

1. A creative file aggregate and its immutable file versions need independent version sequences. The
   aggregate now advances for every command while file version numbers advance only for replacements;
   PostgreSQL reconciles the aggregate version to immutable version and review counts to prevent a
   supplier from replaying one review as multiple aggregate updates.
2. Supplier review initially failed because forced Campaign RLS hid the buyer Campaign from the
   validation trigger. The trigger now validates under a fixed-search-path database-owner context while
   still requiring the exact supplier tenant and human actor from the request.
3. Proposal-stage concepts are not accepted as production creative. Only protected files bound to the
   exact booked requirement can enter the readiness workflow.

## Remaining boundary

This packet does not launch a Campaign, communicate with a supplier or client, collect money, invoke a
live provider, deploy, push or mutate production. `READY` to `LIVE`, delivery completion, proof,
measurement, reporting and learning remain unimplemented. Owner approval and independent finance,
legal, security/privacy and operations review remain pending.
