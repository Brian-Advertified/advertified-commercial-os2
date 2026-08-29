# ADR-0001: AgentCore-compatible AI runtime boundary

## Status

Proposed — named owner and reviewers unassigned.

## Context

The normative specification requires a Python/FastAPI runtime with typed agent/tool contracts and an eleven-agent closed roster. The repository is in zero-live-provider redevelopment. Provider, regional availability, cost, privacy, deployment, and operational decisions are not yet approved.

## Proposed decision

Keep the runtime contract compatible with Amazon Bedrock AgentCore while isolating provider-specific code behind an adapter. The C# Commercial API remains canonical authority. A deterministic local provider implements the same typed contract for tests.

No Bedrock/provider SDK or live call is added before an approved gate packet defines identity, tool policy, budgets, data handling, timeouts, retry/idempotency, checkpoints, telemetry, evaluations, and a named owner. The deterministic provider is a test implementation, not a production fallback that fabricates results.

## Consequences

- Product/domain work can be tested without cost or external data transfer.
- Provider adoption remains an explicit reversible decision.
- Agent outputs cannot mutate commercial state or approve consequences.
- Live-provider unavailability produces a blocked/review state, not a silent alternate provider.

## Approval evidence required

- confirmed service/region availability and deployment design;
- security/privacy/legal assessment and processor terms;
- cost policy and alerts;
- provider contract/evaluation results;
- failure/recovery tests;
- named Product, Engineering, Security/Privacy, Finance, and Operations decisions.

Until accepted, this ADR authorises no AWS setup or provider package.
