# Agent runtime parity evidence report

**Evidence date:** 2026-08-30

**Repository:** advertified-commercial-os2

**Branch:** master

**Base commit:** `da315aa4196376e82903868bb2baede796098fef`

**Live provider used:** No

**Production resources used:** No

## Implemented

- Added strict Audience, Media Planning and Proposal Narrative request/artifact contracts to the single Python/FastAPI runtime.
- Separated Audience from Media Planning so clear Brief audience work no longer depends on available inventory channels.
- Added exact BriefVersion and MediaPlanVersion resource lineage, route/agent matching and truthful nine-of-eleven runtime capability reporting.
- Added C# HTTP adapters selected only by `AgentRuntime:Mode=HttpDeterministic`; local disabled/in-process operation keeps explicit deterministic adapters.
- Consolidated HTTP authentication, invocation policy, response-schema, evidence and zero-cost validation with the existing Opportunity adapter.
- Rejected unknown response fields, unapproved evidence, non-zero cost/tool use, invented sensitive audience attributes, disallowed/duplicate channels, budget mismatch and changed proposal facts.
- Preserved exact minor monetary units in deterministic narrative, HTTP validation and rendered proposal money formatting.

## Verification

| Check | Exact command | Final result |
|---|---|---|
| Runtime lint and complete suite | `cd agent-runtime; python -m ruff check .; python -m pytest` | PASS - Ruff clean and 26/26 tests |
| Focused HTTP adapter suite | `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~AgentRuntimeHttpAdapterTests` | PASS - 10/10 tests |
| Complete API suite | `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj -c Release --no-restore` | PASS - 56/56 tests |
| Architecture guardrails | `python -m pytest tests/architecture -q` | PASS - 23/23 tests |
| Diff integrity | `git diff --check` | PASS |

## Corrected checks and review findings

1. The initial Python patch used strings where the common contract requires UUID evidence identifiers. Contracts and services now retain UUID types end to end.
2. Audience and media planning initially shared one input shape, which unnecessarily made audience generation depend on inventory channels. The application/runtime interfaces are now separate.
3. Pydantic model-level validation initially included a non-serializable exception object in the HTTP 422 body. Validation context is now omitted from safe error responses.
4. The first .NET test build found CA1502 in common response validation and CA1861 in a test fixture. Validation was split by responsibility and the fixture allocation was made static; no analyzer was suppressed.
5. Diff review found Proposal Narrative pinned only the BriefVersion. Every selected MediaPlanVersion is now also pinned and covered by negative tests.
6. The final stricter audience evidence check rejected a test fixture's generic `artifact` binding. The fixture now binds the exact `artifact.audiences` material path; no validation was weakened.
7. The architecture check only enumerated Gate 1-6 manifests, leaving later packet evidence outside its closed-schema check. It now discovers every evidence manifest and still verifies numbered gate directories against their declared gate.

## Production boundary

This packet provides local AgentCore-compatible contract parity, not a live AgentCore or Bedrock deployment. ADR-0001 is still proposed and has no named approvals. Inventory Intelligence remains a separate deterministic shortlist/tool packet, Measurement remains Gate 11, Gate 10 booking/commercial settings are incomplete, and Gates 11-13 remain sequence-blocked. No provider SDK, live provider, push, deployment or production resource was used.
