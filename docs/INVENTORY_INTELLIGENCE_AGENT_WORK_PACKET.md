# Inventory Intelligence agent work packet

**Owner direction:** 2026-09-01 continue development toward production and complete the accepted cost-controlled agent approach without unnecessary containers, images or live provider spend.

**Prerequisite evidence:** the current `advertified-dev` stack is healthy; the connected Proposal inbox journey passes; the connected Brief-to-approved-PDF-and-client-share proposal journey passes; Gate 7 already persists governed hard eligibility and immutable OOH benchmark snapshots.

## Bounded requirement

Complete the approved eleven-agent roster by adding the missing `inventory_intelligence` interpretation boundary. The Commercial API remains authoritative for inventory truth, hard eligibility, prices, availability, benchmark peer selection and calculations. The agent receives only exact-version shortlist facts and produces a typed, non-consequential commercial explanation for each candidate. The explanation is validated by C# and persisted against the existing recommendation binding.

## In scope

- strict provider-neutral Inventory Intelligence request and artifact contracts;
- exact BriefVersion and InventoryShortlistVersion resource references;
- zero-cost deterministic runtime handler and truthful eleven-handler capability reporting;
- equivalent in-process and HTTP planning adapters;
- C# validation that the response covers exactly the supplied candidates and cannot alter eligibility, scores, prices, benchmarks or IDs;
- durable per-candidate rationale stored through the existing recommendation binding;
- shortlist API/UI projection of the rationale;
- focused runtime, adapter, planning acceptance, web type/lint and connected proposal evidence.

## Out of scope and blocked

- AWS SDKs, AgentCore/Bedrock deployment, live or paid calls, credentials or cloud mutation;
- model selection, fallback or routing activation before ADR-0001 approval and production security/cost controls;
- direct Python database access or any inventory, planning, approval, booking, publication or spend mutation;
- changing deterministic eligibility, commercial arithmetic, benchmark cohort selection or supplier availability;
- new Docker images, containers or services;
- broad inventory-ingestion redesign, production data import or release approval.

## Acceptance evidence

1. Runtime deterministic mode advertises all and only the eleven approved handlers.
2. Inventory Intelligence rejects route mismatch, unknown fields, missing exact resource versions and malformed candidate facts.
3. The artifact covers every supplied candidate exactly once and only explains supplied deterministic facts.
4. The C# HTTP adapter rejects missing, duplicate or unknown candidate IDs and any non-zero provider cost.
5. The Commercial API persists the validated explanation without changing eligibility, rejection reasons, scores or benchmark snapshots.
6. The shortlist UI displays the explanation while retaining visible deterministic benchmark and rejection details.
7. Targeted runtime/API/web tests and the connected proposal journey pass with zero live-provider calls and no new Docker resources.

## Verification record — 2026-09-01

Implemented and locally verified at the runtime/browser boundary. Evidence is retained under
`docs/evidence/inventory-intelligence-agent-20260901/`.

- Deterministic runtime passes 31/31 and advertises all eleven approved handlers.
- Master-data registry 2.12.0 projections match across C#, TypeScript and Python.
- Web lint/type/unit/build pass; the connected proposal journey proves the persisted Inventory
  Intelligence rationale is visible before inventory selection and then completes approved PDF/share.
- The current rebuilt `advertified-dev` stack passes all 3 connected critical journeys.
- Final-tree architecture passes 42/42.
- No new Compose project, live provider, production resource or paid AI call was used.

The complete current-source C# test suite remains a verification blocker because this Windows host
has .NET SDK 10.0.103 while the repository requires 10.0.400 with roll-forward disabled. The current
API does compile and publish in the pinned 10.0.400 Linux image; a 10.0.400-capable runner or remote
CI is still required before the C# result can be called current.
