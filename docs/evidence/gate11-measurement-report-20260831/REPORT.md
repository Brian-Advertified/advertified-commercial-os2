# Gate 11 deterministic measurement report evidence

**Evidence date:** 2026-08-31

**Base commit:** `9efb3a48611bf23faf83cda4f22ef2e95d9d6e39`

**Live provider or production resource used:** No

## Implemented

- Added the closed-roster `measurement` agent to the deterministic Python runtime and to both C#
  runtime adapters. The shared typed contract rejects missing or extra metric references, dropped
  limitations, unsupported causality, unknown response fields, tool use and non-zero cost.
- Added immutable, versioned MeasurementReport generation after a completed Campaign has an immutable
  measurement plan, approved DeliveryProof and approved sourced PerformanceMetric facts. A supplied
  Brief Campaign binds its run directly to the Campaign and does not fabricate an Opportunity.
- Retained the complete validated proposal and exact Campaign, proof, evidence and metric versions in
  the canonical agent run, step, usage and report trace. Provider, model, contract, prompt, validation,
  tool count and zero cost are recorded.
- Added a named different human reviewer and review task. Rejection is retained, correction creates a
  new report version, and only approved reports appear in the Campaign's client projection. Findings
  can only state `NOT_ESTABLISHED` causality and learning proposals require a later approval.
- Added migration 024 with forced RLS, fixed-search-path security-definer functions, immutable
  insert/review/delete triggers, exact evidence/metric/limitation and trace checks, guarded rollback,
  governed registry 2.9.0, audited/outboxed endpoints and retained OpenAPI.

## Verification

| Check | Result |
|---|---|
| Complete Release API/database/security suite | PASS - 70/70 in 2m52s |
| Canonical report journey | PASS - missing facts blocked; exact report generated, rejected, regenerated and approved; only approved version projected |
| Negative report boundaries | PASS - wrong tenant/reviewer, self-review, duplicate pending report, repeated review, direct mutation and deletion fail closed |
| HTTP/runtime contract | PASS - 16/16 adapter tests; route, exact evidence, cost, limitation, causality and strict schema checked |
| Migration, rollback, forced RLS and OpenAPI | PASS - 3/3 migration/isolation checks, 80 forced-RLS tables; 2/2 retained OpenAPI checks |
| Deterministic Python runtime | PASS - Ruff and 28/28 tests |
| Governed master data | PASS - registry 2.9.0 projections current |
| Focused C# formatting | PASS - all affected backend files |
| Compose, diff and artifact hygiene | PASS - Compose valid, no whitespace errors, `.artifacts/` tracked files = 0 |
| Complete architecture suite | BLOCKED - 21/23; separately owned screen work has a 521-line CSS file and inline governed `PENDING` |
| Web regression | NOT RUN - another agent owns and is changing the screen tree |

## Security findings closed

1. The old run schema assumed every agent run belonged to an Opportunity. Migration 024 now permits a
   Campaign-scoped run while requiring at least one canonical work scope, preserving the supplied Brief
   path without inventing an Opportunity.
2. The database independently re-derives every approved evidence version, metric identifier and source
   limitation, and verifies the single completed zero-cost run/step/usage trace before accepting a report.
3. Internal run identifiers, prompt metadata and protected evidence object keys are excluded from the
   client report projection; the approved report retains sourced facts, methodology, quality and limits.

## Remaining boundary

The separately owned screens must clear their two architecture failures and pass their web/browser
suite before repository-wide verification is green. Gate 12 certification preparation, operational
runbooks, backup/restore evidence, observability, deployment-environment decisions and independent
owner, security/privacy, legal, finance and operations reviews remain pending. No push, deployment,
live provider call or production mutation was performed.
