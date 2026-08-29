# ADR-0003: Opportunity-to-Brief agent separation

## Status

Accepted for local non-production Gate 4 implementation — Brian Rabuthu, 2026-08-29.
Independent Product, Strategy, AI and Risk review remains required before publication or
production.

## Context

The normative unbriefed workflow separates interpretation, opportunity selection, strategy, critique, and Brief drafting so evidence and human decisions remain auditable. Earlier text placed Strategy before Opportunity while also making Strategy depend on a selected opportunity; that was circular.

A supplied client Brief is a separate entry path and must never be forced through this workflow.

## Proposed sequence

1. Human/crawler captures permitted sources.
2. Human approves material evidence.
3. Business Interpretation proposes business/customer/context understanding.
4. Opportunity Intelligence proposes evidence-linked angles.
5. Human selects or rejects an exact angle set.
6. Strategy uses approved evidence, interpretation, and selected angle.
7. Critic records objections; a human resolves them and approves Strategy.
8. Brief Drafting proposes a BriefVersion while preserving unknowns and lineage.
9. A human edits/reviews and approves the exact BriefVersion.

Each specialist has one typed contract and cannot approve its own output. Deterministic services own validation, permissions, lifecycle transitions, calculations, and persistence.

## Required decision evidence

- typed input/output and evidence-binding schema per step;
- human task/approval and immutable version rules;
- retry/checkpoint/cost behavior;
- adversarial and named evaluation fixtures;
- explicit latency/operating-model acceptance;
- proof the supplied-Brief path remains independent.

This decision authorises only the deterministic, zero-cost local sequence bounded by
`docs/GATE4_WORK_PACKET.md`. It authorises no production workflow or live provider.

## Decision record

- Accountable owner: Brian Rabuthu
- Decision: accepted for local non-production Gate 4 implementation
- Decision date: 2026-08-29
- Production and publication review: pending
