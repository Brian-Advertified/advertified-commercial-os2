# Gate 12 Bedrock provider and model-routing work packet

**Status:** IN PROGRESS — provider implementation and zero-live-call certification only
**Date:** 2026-09-01
**Normative sources:** ADR-0001; specification Sections 22, 26 and 27

## Bounded requirement

Complete the production provider boundary behind the existing eleven typed agents while preserving the deterministic provider as the certification default. No agent may gain authority to mutate canonical state, approve, spend, publish, book, invoice or communicate externally.

## Implementation

- Generalise the provider policy contract so an invocation can explicitly request either the deterministic fixture provider or Amazon Bedrock.
- Keep deterministic mode exactly zero-cost and fail closed; there is no automatic fallback from a failed Bedrock request to fabricated deterministic output.
- Add an allow-listed Bedrock runtime mode using the AWS Bedrock Runtime Converse API and IAM-managed credentials only.
- Route model IDs by agent code through configuration. Model choice is operational configuration, not hard-coded business logic, so lower-cost capable models can replace older models without changing campaign rules.
- Require exact model allow-list membership, bounded timeout/attempts, temperature zero, per-invocation cost ceiling and output schema validation.
- The model returns proposal content only. Runtime code constructs trusted provider usage from the Bedrock response and rejects model-supplied usage/cost claims.
- Calculate conservative incremental cost from configured input/output token prices and reject output whose calculated cost exceeds the invocation cap.
- Preserve approved evidence IDs, exact resource versions, hard commercial facts and tool policy. Provider output remains untrusted until Pydantic and Commercial API validation pass.
- Emit provider request ID/model/token usage in the typed usage/audit boundary without logging prompts or sensitive payloads.

## Certification boundary

- `ADVERTIFIED_AGENT_RUNTIME_MODE=deterministic` remains the local/CI/staging certification default.
- `ADVERTIFIED_AGENT_RUNTIME_MODE=bedrock` requires explicit allow-list, region and model pricing configuration plus invocation `allow_live=true`.
- No live or paid Bedrock call is executed during redevelopment or the zero-Bedrock production certification cohort.
- The first production canary remains a separate named owner action after the zero-live-provider release evidence is green.

## Verification after the release batch is complete

- Deterministic 11-agent tests remain green and zero-cost.
- Bedrock policy/configuration tests reject disabled mode, unknown model, zero/over-budget cap, malformed output and provider mismatch without making a network call.
- C# runtime adapters accept deterministic usage and validate configured live provider/model/cost against the exact invocation policy.
- Packaging includes the hash-locked AWS SDK dependency but no credential or live provider call.
