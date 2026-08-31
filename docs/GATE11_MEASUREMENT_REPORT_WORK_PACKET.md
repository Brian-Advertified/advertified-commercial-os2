# Gate 11 measurement interpretation and client-report work packet

**Owner direction:** 2026-08-30 continue fixing issues and advance the project toward production.

**Verified predecessor:** local commit `9efb3a4`; Release Commercial API 64/64, deterministic
runtime Ruff and 26/26, retained OpenAPI, migration apply/rollback, 79-table forced RLS and Compose
checks pass. The E1 report at `docs/evidence/gate11-performance-evidence-20260830/REPORT.md`
records the two separately owned screen architecture failures and the older C# formatting baseline.

## Bounded requirement

Complete packet E2 from `docs/GATE11_MEASUREMENT_WORK_PACKET.md` on the existing canonical Campaign
lifecycle. Use the existing closed-roster `measurement` agent and durable agent-run/usage structures.
The C# Commercial API remains the only canonical writer. Python returns a typed proposal and never
connects to PostgreSQL, approves, sends, publishes, spends, books or changes a Campaign.

## Exact implementation boundary

- generation requires a `COMPLETED` Campaign, a non-empty immutable measurement plan, approved
  DeliveryProof, and at least one approved performance evidence set with exact metric rows;
- the agent invocation identifies the exact Campaign, DeliveryProof and evidence-set versions and
  treats the exact PerformanceMetric identifiers as its approved evidence bindings;
- both the in-process deterministic client and HTTP runtime use the same typed
  `MeasurementInterpretation` contract, zero tools, zero cost and no live provider;
- every finding references one or more supplied metric identifiers, together the findings reference
  every supplied metric exactly once, and the output cannot add or omit an identifier;
- every source limitation is retained exactly; causality is governed and restricted to
  `NOT_ESTABLISHED` because the current measurement-plan contract proves no causal design;
- recommendations are typed learning proposals requiring a later human-approved command and have no
  autonomous workflow consequence;
- a successful validated invocation writes one completed canonical AgentRun, one completed step, one
  zero-cost usage row and one immutable report version in `REVIEW_REQUIRED`;
- one explicitly assigned different human reviews the exact report version. Rejection remains
  retained, correction creates a new report version, and only approved reports appear in the Campaign
  client projection;
- database constraints, forced RLS and fixed-search-path triggers independently enforce inputs,
  trace linkage, immutable output, actor separation, review transition and task consequences.

## Acceptance evidence

1. Missing plan/proof/facts, stale or cross-tenant inputs and self-review fail closed.
2. Route mismatch, malformed output, non-zero cost/tool use, missing/extra metric references, dropped
   limitations and unsupported causality fail contract validation without creating a report.
3. Pending and rejected reports never appear as approved client reports; rejection is retained and a
   separately generated version can be approved by its assigned human.
4. Direct report mutation/deletion and forged database insertion fail independently of the API.
5. The approved projection contains canonical metrics plus source, quality, methodology, limitations
   and the approved interpretation, without protected object keys or internal agent metadata.
6. Migration apply/rollback, forced RLS, retained OpenAPI, Release/full API, deterministic runtime,
   architecture and Compose checks produce reproducible evidence.

## Explicitly out of scope

- live/paid model or measurement-provider calls, credentials, AWS/Bedrock/AgentCore deployment;
- causal attribution, ROI, inferred metrics, baseline fabrication or unsupported comparisons;
- autonomous optimisation, budget reallocation, spend, booking, publication or external send;
- PDF/download rendering and all screen implementation while another agent owns the web tree;
- production data, cloud mutation, deploy, push or production-readiness approval;
- owner, finance, legal, security/privacy and operations decisions.
