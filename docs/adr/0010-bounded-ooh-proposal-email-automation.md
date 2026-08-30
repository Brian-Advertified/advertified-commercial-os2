# ADR-0010: Bounded OOH proposal email automation

## Status

Accepted by repository owner instruction.

Decision date: 2026-08-30

## Context

Advertified needs a dedicated mailbox that can receive a complete OOH request, run the same canonical campaign flow used by an interactive planner, render a proposal and reply without a person acting on each message. The existing no-autonomy rule remains correct for spend, supplier commitments, booking, payment, invoices, public creative and general external communication.

## Decision

A tenant may explicitly opt one mailbox into a narrowly bounded OOH proposal automation policy.

The automation:

- accepts only cryptographically verified inbound provider events for the configured mailbox;
- preserves the original email metadata and retrieved body as immutable source evidence;
- deduplicates provider email ID, message ID and canonical content hash;
- creates a new CampaignBrief with immutable `OOH_ONLY` campaign mode;
- runs the same segmentation, targeting and positioning, media mix, inventory eligibility, benchmark, supply, plan, proposal and PDF services as the interactive flow;
- allows only OOH and DOOH allocations;
- sends only to the verified sender or reply-to address and replies in the original thread;
- records exact artefact versions, policy version, delivery idempotency key, provider receipt and incremental AI cost;
- performs no live or paid model call unless a later separately approved provider policy enables it.

The automation sends nothing when any material fact is missing or conflicting, the request includes another media channel, the client/sender cannot be resolved under tenant policy, inventory or supply is insufficient/stale, totals do not reconcile, an objection remains unresolved, the proposal is expired, the PDF is unavailable, or provider delivery is ambiguous. Such cases become `REVIEW_REQUIRED` with one human-readable reason and a safe retry checkpoint.

Changing an OOH-only request into a full campaign always starts a new CampaignBrief and new downstream lineage. No STP, mix, shortlist, supply confirmation, plan or proposal is promoted from the OOH-only run.

## Consequence boundary

This decision permits only the client-facing delivery of an informational proposal under the configured policy. It does not authorise autonomous spend, rate acceptance, supplier contact, inventory reservation, booking, contract, payment, invoice, creative publication or client acceptance.

A tenant administrator may disable the mailbox or automatic delivery at any time. Live Resend credentials and webhook secrets remain secret-store configuration and are never committed.

## Required evidence

- valid and invalid webhook signature tests using the raw request body;
- duplicate/replay tests proving one canonical run and at most one delivery;
- OOH-only channel and campaign-mode immutability tests;
- STP presence and confidence checks;
- no-send tests for incomplete, non-OOH, stale-supply and unresolved-objection cases;
- exact PDF attachment and reply-thread metadata in the deterministic delivery adapter;
- tenant isolation, permissions, audit, outbox and restart-safe checkpoint tests;
- live-provider mode disabled in local certification.
