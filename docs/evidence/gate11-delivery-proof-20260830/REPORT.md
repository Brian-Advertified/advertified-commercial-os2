# Gate 11 campaign delivery and proof evidence report

**Evidence date:** 2026-08-30

**Base commit:** `3dea534e47fe13fd64f2900ffb190f65f7df9011`

**Live provider or production resource used:** No

## Implemented

- Added human-controlled `READY` to `LIVE` and `LIVE` to `COMPLETED` transitions on the one
  canonical Campaign lifecycle. PostgreSQL revalidates the booked flight, confirmed funding,
  exact confirmed bookings and current approved creative before launch.
- Completion is blocked until the booked flight has closed and records the human actor, completion
  reason and explicit delivery-proof request without inventing delivery or performance facts.
- Added immutable, malware-scanned, signature-checked and hashed delivery proofs bound to the exact
  Campaign Booking. Only that Booking's supplier tenant may submit captured evidence from inside its
  flight window; protected object keys are never exposed by API projections.
- Added buyer review with optimistic concurrency, immutable rejection history and separate replacement
  proofs. A supplier cannot review its own evidence, including through a direct database-role attempt.
- Added one buyer-owned review task for every submitted proof; the exact task completes only when the
  proof is reviewed. Submission and review remain audited and outboxed commercial commands.
- Added governed proof types, permissions, actions, events and resource codes, forced RLS, forward and
  guarded rollback migration behavior, API/OpenAPI contracts and negative end-to-end evidence.

## Verification

| Check | Result |
|---|---|
| Release API build and complete C# suite | PASS - zero warnings/errors; 64/64 |
| Canonical delivery journey | PASS - booking through approved replacement proof |
| Negative security boundaries | PASS - wrong tenant/actor, premature completion, proof before request, outside-flight evidence, mismatched file, supplier self-review, repeated review and direct mutation fail closed |
| Migration, rollback and forced RLS | PASS - 3/3; all 77 protected tables verified |
| Retained OpenAPI | PASS - 2/2; generated contract matches the running API |
| Governed master data | PASS - registry 2.7.0 projections are current |
| Deterministic agent runtime | PASS - Ruff and 26/26 |
| Compose validation | PASS |
| Diff and artifact hygiene | PASS - no whitespace errors; `.artifacts/` has zero tracked files |
| Complete architecture suite | BLOCKED - 21/23; screen agent's uncommitted `web/src/brief-intake.css` is 514 lines against the 400-line hard limit, and `PlanningWorkbench.tsx` embeds governed `PENDING` inline |
| Web regression | NOT RUN - screen agent is actively changing the web tree; backend packet does not claim those files |

## Security findings closed in this packet

1. API permission checks alone were not treated as sufficient evidence against supplier self-review.
   The database trigger independently rejects the submitting actor as reviewer, and a direct
   `advertified_app` regression test proves the boundary.
2. Proof metadata cannot claim a file shape inconsistent with its governed type. Both the application
   signature policy and a database type/media constraint enforce the mapping.
3. Evidence rejection never mutates or replaces the original proof. A replacement is a new protected,
   attributable record with its own review task and decision.

## Remaining boundary

This packet does not create performance facts, calculate outcomes, render or send a client report,
learn from results, communicate externally, spend money, deploy, push or touch production. Measurement,
reporting and learning require a separate sequential packet. The separate screen work must satisfy the
400-line and governed-code guards and complete its own web/browser regression before repository-wide
verification can be green. Owner approval and independent finance, legal, security/privacy and operations review remain
pending.
