# ADR-0002: Human approval for material consequences

## Status

Proposed implementation decision — the normative no-autonomy rule already applies.

## Context

Advertified handles client money, public creative, supplier commitments, external communication, pricing, payments, and invoices. These effects require accountable human authority and immutable evidence.

## Proposed decision

The following always require a named authorised human acting on the exact immutable version:

- budget/spend or material pricing change;
- proposal/external communication;
- supplier commitment, booking, contract, or cancellation;
- payment or invoice;
- creative/public publication;
- client acceptance/decline;
- legal/privacy/security notification or production release.

Creation, editing, submission, review, and approval are separate permissions. A creator cannot self-approve. Delegation is explicit, scoped, time-bound, revocable, and audited. No “emergency,” “fast-track,” bulk, agent, worker, or configuration path may bypass the consequence check.

Deterministic low-risk notifications may later operate under a narrowly approved policy that fixes audience, template, trigger, opt-out, rate limit, and audit. That is not general external-communication authority.

## Required enforcement evidence

- API and persistence invariants;
- negative permission and stale-version tests;
- tenant/assignment/delegation tests;
- durable idempotent command/audit/outbox trail;
- UI wording showing the exact consequence and version;
- named owner approval.

No implementation may treat this Proposed ADR as permission to weaken the normative rule.
