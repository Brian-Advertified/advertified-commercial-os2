# Gate 11 protected performance evidence report

**Evidence date:** 2026-08-30

**Base commit:** `661434b0db7761c5f9fc92e93c41f4d993f1d453`

**Live provider or production resource used:** No

## Implemented

- Added protected, signature-checked, deterministically malware-scanned and hashed PDF, JSON or CSV
  performance evidence after Campaign completion. The protected object key remains server-only.
- Added exact immutable PerformanceMetric facts with governed metric, unit and quality codes, decimal
  values, campaign-bounded periods and source locators. Methodology and at least one limitation are
  mandatory and remain visible beside the facts.
- Added a named different reviewer, optimistic concurrency, immutable approval/rejection and a dedicated
  review task. `UNUSABLE` evidence cannot be approved, and reviewed Campaign projections retain both
  approved and rejected sets while excluding pending evidence.
- Added independent database enforcement for tenant, Campaign completion, approved delivery proof,
  submitter and reviewer roles, actor separation, exact object keys, fact immutability, lifecycle and
  task consequences. A direct `advertified_app` insert by an advertiser role is rejected.
- Added audited and outboxed submit/review commands, API/OpenAPI contracts, governed registry 2.8.0,
  forced RLS on both new tables and a guarded forward/rollback migration.

## Verification

| Check | Result |
|---|---|
| Release API build and complete C# suite | PASS - zero warnings/errors; 64/64 |
| Canonical measurement journey | PASS - completed Campaign through reviewed facts |
| Negative application boundaries | PASS - pre-completion, wrong tenant, self-review, empty facts, missing method/limitations, invalid value/period/type/unit, unsafe file, unusable approval and repeated review fail closed |
| Negative database boundaries | PASS - unauthorised direct insert and direct evidence/metric mutation fail closed |
| Migration, rollback and forced RLS | PASS - 5/5 combined migration/OpenAPI checks; all 79 protected tables verified |
| Retained OpenAPI | PASS - generated contract matches the running API and exposes the three measurement routes with correct concurrency semantics |
| Governed master data | PASS - registry 2.8.0 projections are current |
| Focused C# formatting | PASS - all new measurement files |
| Deterministic agent runtime | PASS - Ruff and 26/26 |
| Compose validation | PASS |
| Diff and artifact hygiene | PASS - no whitespace errors; `.artifacts/` has zero tracked files |
| Complete architecture suite | BLOCKED - 21/23; screen agent's `web/src/brief-intake.css` is 521 lines and `PlanningWorkbench.tsx` embeds governed `PENDING` inline |
| Repository-wide C# format baseline | BLOCKED - older endpoint whitespace/EOL failures outside this packet |
| Web regression | NOT RUN - another agent is actively changing the web tree |

## Security findings closed in this packet

1. API permission checks are backed by the database trigger's independent active-role checks for both
   submitter and reviewer. An advertiser reviewer cannot use the application database role to submit.
2. Unreviewed AI/user-supplied facts remain directly reviewable by the assigned human but are excluded
   from the Campaign's reviewed evidence projection until a decision exists.
3. Exact metric rows and their source metadata cannot be updated or deleted; rejection is retained and
   correction requires a separately attributable evidence set.

## Remaining boundary

This packet does not interpret performance, infer attribution, calculate ROI, generate or approve a
client report, optimise spend, communicate externally, deploy, push or touch production. Packet E2 must
bind deterministic typed interpretation to approved facts and the approved measurement plan, then put
the exact report through separate human approval. The separate screen work and older C# formatting
baseline must also return green before repository-wide verification can pass. Owner approval and
independent finance, legal, security/privacy and operations review remain pending.
