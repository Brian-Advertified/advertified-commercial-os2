# Agent runtime parity remediation work packet

**Owner direction:** 2026-08-30 continue toward production after the committed codebase hardening review.

**Prerequisite evidence:** commit `da315aa`; Release API build 0 warnings/errors; API 46/46; architecture 23/23; deterministic runtime 18/18; web journeys 20/20.

## Bounded requirement

Move the already-delivered Gate 7/8 Audience, Media Planning and Proposal Narrative proposal boundaries behind the one Python/FastAPI AgentCore-compatible runtime contract when `AgentRuntime:Mode=Http`. Preserve the current C# deterministic implementations only as explicit local/test adapters. The Commercial API remains the only canonical writer and validates every returned proposal before persistence.

## In scope

- strict Pydantic request/artifact contracts for `audience`, `media_planning` and `proposal_narrative`;
- zero-cost deterministic handlers using only supplied approved inputs;
- the common invocation/output envelope, exact resource versions, evidence binding, tool policy and provider usage contract;
- C# HTTP adapters selected only by the existing HTTP runtime mode;
- route/agent mismatch, unknown field, unapproved evidence, non-zero cost and malformed response failures;
- truthful runtime capability reporting and capability-ledger correction;
- deterministic FastAPI, C# adapter, architecture and affected planning/proposal acceptance evidence.

## Out of scope and blocked

- AWS SDKs, Bedrock/AgentCore deployment, live or paid calls, production credentials or cloud mutation;
- provider mode in production before ADR-0001 has named approval, security/privacy/legal review, finance cost policy and operations evidence;
- `inventory_intelligence`, which needs a separate shortlist/tool packet so deterministic eligibility and benchmark ownership are not duplicated;
- `measurement`, which remains Gate 11;
- direct Python database access, commercial mutation, approval, send, spend, booking or publication.

## Acceptance evidence

1. The runtime advertises exactly the handlers it can execute and keeps absent agents absent.
2. Audience facts are evidence-bound or explicitly labelled inference/unknown; sensitive attributes are not invented.
3. Media allocations use only allowed channels and reconcile exactly to the supplied budget.
4. Proposal narrative cannot alter option totals, inventory, terms or approved plan facts.
5. HTTP adapters reject mismatched agent routes, unapproved evidence references, non-zero cost and invalid schemas.
6. In-process deterministic and HTTP modes honour the same application interfaces without adding a second business-state owner.
7. Runtime tests, API Release build/full tests, architecture tests and affected browser journeys pass with zero live-provider cost.
