# Gate 11 sourced measurement and client-report work packet

**Owner direction:** 2026-08-30 continue fixing issues and advance the project toward production.

**Verified predecessor:** local commit `661434b`; Release Commercial API 64/64, deterministic
runtime Ruff and 26/26, retained OpenAPI, migration apply/rollback, 77-table forced RLS and Compose
checks pass. The separately owned screen work remains outside this backend packet and currently blocks
two repository architecture checks.

## Bounded requirement

Complete the remaining evidence-backed portion of Gate 11 without treating delivery proof as
performance, inventing a metric, claiming unsupported causality, sending a report externally or
optimising spend. The C# Commercial API remains the sole canonical writer. The existing closed-roster
`measurement` agent may propose a typed interpretation only from exact approved inputs through the
same zero-cost deterministic runtime contract used by the other implemented agents.

## Packet E1 — performance evidence and exact facts

- an authorised buyer-side human submits one immutable protected source file and one or more typed
  PerformanceMetric facts only after the Campaign is `COMPLETED`;
- each metric records governed metric and unit codes, decimal value, period, source locator and the
  exact evidence-set identifier; the evidence set records source reference, capture time, methodology,
  required limitations and governed quality status;
- files are signature-checked, malware-scanned, hashed and stored under a protected tenant/campaign
  object key that is never returned by the API;
- one explicitly assigned, different buyer-side human reviews the exact evidence-set version;
  `UNUSABLE` quality cannot be approved, rejection is retained, and correction is a new set;
- database constraints, forced RLS and fixed-search-path triggers independently enforce Campaign
  completion, tenant, actor, exact facts, review separation, immutability and task consequences;
- submission and review are idempotent audited commands with outbox events.

**E1 exit evidence:** facts before completion, empty facts, unsafe or mismatched files, unsupported
metric/unit codes, invalid periods/values, missing methodology/limitations, unrelated tenants,
self-review, approval of unusable evidence, repeated review and direct SQL mutation fail closed.
Approved exact facts remain visible with their source, quality and limitations without implying an
outcome beyond the supplied evidence.

## Packet E2 — typed interpretation and approved client report

- report generation requires at least one approved DeliveryProof and one approved performance
  evidence set plus the Campaign's immutable approved measurement plan;
- the closed-roster `measurement` runtime handler receives exact Campaign/evidence/report input
  versions, has no external tools or live provider, and returns a strict MeasurementInterpretation;
- the Commercial API validates that every finding references an exact approved PerformanceMetric,
  preserves canonical values separately, retains all limitations and uses governed
  `NOT_ESTABLISHED` causality unless an approved measurement design supports more;
- generation persists an immutable report version in review and creates one task for a named,
  different human approver; rejected reports remain retained and correction creates a new version;
- approval exposes the exact structured report to authorised agency/advertiser participants. It does
  not send, publish, change spend, alter a Campaign, or create a recommendation consequence;
- report recommendations are learning proposals only. Any material optimisation requires a later
  named human command against an exact approved artefact version.

**E2 exit evidence:** no approved proof/facts, stale or cross-tenant inputs, runtime route mismatch,
malformed output, missing/extra metric references, dropped limitations, non-zero cost, unsupported
causality, self-approval, repeated approval and direct mutation fail closed. The client-role projection
contains canonical facts, visible source/quality/limitations and the approved interpretation without
storage keys or internal runtime metadata.

## Explicitly blocked or out of scope

- live measurement-provider access, credentials, webhooks, AWS/Bedrock/AgentCore deployment or paid AI;
- inferred reach, impressions, response, footfall, conversion, attribution, baseline or ROI;
- autonomous budget optimisation, reallocation, spend, booking, publication or external communication;
- PDF/download rendering and screen implementation while the separate screen agent owns the web tree;
- production data, cloud mutation, deployment, push or production-readiness approval;
- owner, finance, legal, security/privacy and operations decisions.

## Acceptance evidence

1. Every client-visible number is a canonical reviewed PerformanceMetric linked to one immutable source.
2. Quality, methodology and limitations are mandatory and remain visible through interpretation/report.
3. AI output is untrusted proposal data, schema-validated, zero-cost, evidence-bound and human-approved.
4. Every command is tenant-scoped, versioned, idempotent, audited and protected by independent database
   constraints/triggers; no submitting or generating actor may approve their own artefact.
5. Empty/upgrade/rollback migrations, forced RLS, retained OpenAPI, Release/full API, runtime and
   architecture checks produce reproducible evidence; separate screen failures remain separately owned.
