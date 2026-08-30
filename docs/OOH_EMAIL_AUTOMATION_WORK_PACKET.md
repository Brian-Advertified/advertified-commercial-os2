# OOH-only inbound proposal automation work packet

**Authorised:** Brian Rabuthu, 2026-08-30

**Depends on:** verified canonical Brief, Planning and Proposal capabilities

**Owner correction:** OOH uses the same STP and planning flow as every campaign. The only difference is that `OOH_ONLY` permits only OOH/DOOH. It can never be widened; changed scope starts a new CampaignBrief.

## Outcome

One configured inbound mailbox can receive a complete OOH request and, without per-message user input, preserve the email, create an OOH-only CampaignBrief, produce segmentation/targeting/positioning, media mix, eligible benchmarked inventory, an approved media plan, a proposal and branded PDF, then reply exactly once to the verified email address.

## In scope

- immutable `OOH_ONLY` / `FULL_CAMPAIGN` campaign mode on the canonical planning spine;
- complete STP for every campaign mode, including audience segments, targeting rationale and positioning statement;
- tenant-owned mailbox configuration with owner/client resolution and explicit automatic-send opt-in;
- Resend `email.received` webhook verification against the raw request and retrieval of the full received email;
- deterministic local provider for repeatable tests and a production Resend HTTP adapter behind validated configuration;
- immutable inbound email, attachment metadata and automation-run records;
- idempotent processing checkpoints and exactly-once delivery key;
- canonical Brief, STP, mix, shortlist, plan, proposal and PDF references on the run;
- plain-language monitoring screen with sent/review/failed states and no technical payload leakage;
- audit/outbox events, tenant RLS and strict permissions.

## Ready-path guards

All must pass before automatic delivery:

1. mailbox enabled and `autoSendEnabled=true`;
2. provider signature valid and event type supported;
3. provider email ID, message ID and content hash not already processed;
4. mailbox recipient and reply address match policy;
5. the mailbox has a configured default client, or the Brief clearly names a client that is created canonically during intake; no client pre-registration is required;
6. source contains objective, audience, geography, timing and typed budget/VAT state;
7. campaign mode is unambiguously `OOH_ONLY`; any non-OOH channel request is rejected from automation;
8. STP exists, is evidence-labelled and meets minimum policy confidence;
9. media mix contains only OOH/DOOH and reconciles to budget;
10. shortlist contains eligible selected inventory with current compatible rate evidence;
11. plan totals reconcile, supply confidence meets policy and no material objection remains;
12. proposal references the exact approved plan, remains unexpired and has a rendered PDF;
13. delivery destination is the verified sender/reply-to and the delivery key has not been used.

## Exception path

A failed readiness guard records one governed failure reason, sets the run to `REVIEW_REQUIRED`, sends nothing and exposes the smallest human action. When a Brief detail is unclear, the person answers only the outstanding question and the original email remains immutable. Provider/network ambiguity becomes retryable without re-running completed canonical stages or duplicating delivery.

## Explicit exclusions

- autonomous supplier contact, booking, rate acceptance, payment, invoice, spend or public creative;
- conversion of an OOH-only campaign into a full campaign;
- hidden fallback from live provider mode to deterministic data;
- invented client identity, demographics, inventory, availability, reach or performance;
- attachment OCR/document extraction not already supported by approved canonical services;
- production secret creation or live email during local verification.

## Required implementation artefacts

- master-data additions for campaign modes, decision sources, automation permissions, states, checkpoints, failures and policy;
- forward-safe migrations for STP/campaign mode and inbound automation tables;
- application contracts and provider boundaries;
- Commercial API endpoints and background-safe processor;
- Resend raw-webhook verifier, retrieval client and delivery adapter;
- web schemas, API client and monitoring page;
- bounded acceptance test proving ready, duplicate, non-OOH and incomplete-message paths;
- architecture, API, migration, web and Playwright verification;
- retained Gate 9 report/manifest and a commit only after all checks pass.

## Cost and environment

Local verification uses deterministic provider adapters and zero incremental AI cost. No production resource or external recipient is contacted.
