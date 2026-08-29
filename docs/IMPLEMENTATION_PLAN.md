# Advertified ordered implementation plan

**Plan version:** 3.0  
**Evidence date:** 2026-08-29  
**Normative product source:** `docs/spec/README.md`  
**Execution rules:** `AGENTS.md`  
**Current permission:** Gate 1 guardrails only

This is the execution index, not a replacement for the full v1.1 specification. Every gate uses the applicable normative sections and the historical traceability/adversarial fixtures in Section 31.

## Current status

| Gate | State | Meaning |
|---:|---|---|
| 0 | Evidence passed locally | Baseline builds, tests, Compose, and extension health pass |
| 1 | Active | Finish architecture, tenancy, command, decision, and evidence guardrails |
| 2–13 | Blocked | No implementation until prior gate evidence and an approved work packet exist |

There are fourteen gates numbered 0 through 13.

## Universal entry and exit rules

Before a gate starts:

1. The prior gate has repeatable evidence and no unresolved NO-GO.
2. A real named owner is assigned; a role label alone is not an approval.
3. The work packet names exact specification sections, in/out scope, contracts, risks, tests, recovery, evidence, and cost ceiling.
4. Conflicts and unknown decisions are explicitly blocked.
5. No unrelated or legacy code is copied into this clean repository.

A gate exits only when:

1. code, migrations, contracts, UI states, and documentation agree;
2. unit, contract, integration, architecture, security, and affected journey tests pass;
3. tenant isolation and negative permissions are tested;
4. commercial actions remain human-approved and audited;
5. no live provider is used unless that gate explicitly authorises it;
6. operational, recovery, accessibility, and telemetry evidence exists;
7. the capability ledger points to exact evidence;
8. the named owner records GO; an AI cannot approve the gate.

## Non-negotiable system flow

- Supplied Brief: preserve source → validate/extract → human review → approved BriefVersion.
- Unbriefed Opportunity: evidence → Business Interpretation → Opportunity angles → Strategy plus Critic → human resolution → Brief drafting → approved BriefVersion.
- Delivery: Brief → Plan → Proposal → Client Decision → Funding → Booking → Readiness → Live → Proof → Measurement → Learning.
- Rapid OOH: approved Brief → OOH path → geography/routes/POIs → verified eligible inventory → supplier confirmation → recalculation → human selection → approved plan → proposal.
- AI proposes. Deterministic services validate and calculate. Humans approve consequences. The C# API owns canonical state.

## Gate 0 — reproducible repository baseline

**Normative areas:** Sections 16–17, 28, 30–31  
**Outcome:** Any developer can install, build, test, and start isolated local foundations.

Required outputs:

- locked dependency manifests and truthful runtime descriptions;
- web lint/type-check/test/build;
- API Release build and real health tests;
- Python tests with provider disabled and no DB SDK;
- PostgreSQL 16 image containing PostGIS and pgvector;
- healthy loopback-only Compose services;
- executable architecture tests and real CI steps;
- complete normative specification in-repo;
- clean-parent and changed-file evidence.

**Current evidence:** `docs/GATE0_VERIFICATION_STATUS.md`.

## Gate 1 — architecture and change-control guardrails

**Normative areas:** Sections 16, 17, 19, 20, 22, 25, 28, 31  
**Outcome:** Code cannot bypass ownership, tenant, command, evidence, or AI containment boundaries.

Work packet must include:

- project/module dependency direction and analyzers;
- tenant context, deny-by-default authorisation test harness, and negative cases;
- command envelope, idempotency, audit, outbox, and optimistic-concurrency contracts;
- aggregate-specific state-machine representation;
- master-data database model and migration policy;
- deterministic provider interface, invocation envelope, budgets, and disabled live adapter;
- fixture/evidence naming and gate report templates;
- ADR status/owner process.

No product journey is implemented in this gate.

## Gate 2 — canonical commercial foundation

**Normative areas:** Sections 5, 18–21, 25  
**Outcome:** Authenticated tenant-scoped commands persist the first canonical aggregates safely.

Minimum scope:

- Tenant, User, Membership, ClientAccount, Agency, Contact;
- PostgreSQL migrations, tenant constraints, indexes, audit and outbox;
- typed money, IDs, timestamps, pagination, errors, correlation;
- command/idempotency/version contracts;
- master/reference tables seeded from the governed registry;
- API contract tests, migration upgrade/restore tests, and cross-tenant denial tests.

Authentication provider choice, browser session model, CSRF, logout, and service identity must be owner-approved before implementation.

## Gate 3 — authenticated application shell

**Normative areas:** Sections 8, 20, 24  
**Outcome:** Each supported role enters an accessible, responsive, tenant-correct shell.

Required behavior:

- approved authentication/session architecture;
- role/tenant context and server-authoritative navigation;
- dashboards and work queues backed by real API contracts;
- loading, empty, stale, forbidden, error, and recovery states;
- no internal implementation jargon in customer-facing surfaces;
- accessibility checks and authenticated Playwright journeys.

The current Gate 0 page is not this gate.

## Gate 4 — evidence and opportunity

**Normative areas:** Sections 6, 7.1, 10, 18.3, 21.2–21.3, 22–23  
**Outcome:** An unbriefed prospect reaches reviewed evidence, interpretation, selected opportunity angle, and approved strategy.

Required order is Business Interpretation → Opportunity Intelligence → Strategy plus Critic. Evidence approval, unknowns, citations, prompt-injection defenses, retries, checkpoints, and deterministic agent evaluations are mandatory.

## Gate 5 — canonical Brief

**Normative areas:** Sections 5.1–5.2, 7.1, 18.3, 19.2, 21.2, 24  
**Outcome:** Both supplied-Brief and Opportunity paths produce an immutable, comparable, human-approved BriefVersion without inventing missing facts.

Source facts, interpretation, hypotheses, planning assumptions, confirmations, and unknowns remain distinct. Preliminary audience direction is not completed audience research.

## Gate 6 — inventory truth

**Normative areas:** Sections 7.4, 10, 18.4, 21.4, 23  
**Outcome:** Supplier-agnostic files become reviewed, versioned, searchable inventory with source lineage.

Pipeline: upload → quarantine → malware/type validation → classify → preserve → render → extract structure/assets/coordinates → normalise → validate → deduplicate/supersede → review → publish → evaluate.

No candidate becomes bookable without approved rates, availability, evidence, and freshness.

## Gate 7 — planning

**Normative areas:** Sections 7.2, 18.5, 21.3, 22  
**Outcome:** An approved Brief becomes approved audience, media mix, eligible shortlist, and MediaPlanVersion.

Hard constraints run before scoring. Money/VAT/fees are deterministic. Every rejection is retained. Supply forecasts expose source and uncertainty. Critic objections must be resolved by a human.

## Gate 8 — proposal and client decision

**Normative areas:** Sections 7.2, 18.5, 19.2, 21.3, 24, 27–28  
**Outcome:** An approved plan becomes immutable, commercially reconciled options, an approved branded document, and an audited client decision.

Proposal tiers use governed codes LAUNCH, BOOST, SCALE, and DOMINANCE. Exact plan, rates, assumptions, wording, and pricing versions are bound before rendering. No changed plan may reuse approval.

## Gate 9 — Rapid OOH

**Normative areas:** Sections 7.3, 10, 21.3–21.4, 23–24, 31.2  
**Outcome:** The specialised OOH planning path produces an approved OOH plan and only then a proposal.

It depends on approved Brief and inventory truth. It may reuse generic Gate 8 proposal infrastructure, but never creates a proposal before supplier-confirmed inventory, recalculation, human selection, and plan approval.

## Gate 10 — supplier marketplace

**Normative areas:** Sections 7.4, 18.4–18.5, 20.4, 21.4, 24, 26  
**Outcome:** Suppliers manage only their own listings, rates, availability, RFQs, and booking responses with freshness and audit.

Supplier silence produces an explicit unavailable/review state—never a fabricated response or one-off data correction.

## Gate 11 — campaign delivery and learning

**Normative areas:** Sections 5, 7, 18.5, 19.3, 21.4, 22, 25–26  
**Outcome:** Accepted work moves through funding, booking, readiness, live delivery, proof, measurement, and learning.

Every consequential transition requires the correct approval and immutable input version. Worker restarts, duplicate events, late proof, and partial measurement must be tested.

## Gate 12 — hardening and certification preparation

**Normative areas:** Sections 27–28, 31  
**Outcome:** Security, privacy, performance, recovery, observability, accessibility, and operations are independently evidenced.

This gate certifies controls introduced earlier; it cannot introduce missing tenancy, audit, approval, privacy, or recovery foundations at the end.

## Gate 13 — production launch

**Normative areas:** Sections 27.5, 28.3–28.4, 31  
**Outcome:** A named cross-functional team—not an AI—records production GO after all release evidence passes.

Required evidence includes the thirty-case zero-live-provider certification, migration/restore proof, runbooks, alarms, security/privacy/legal decisions, exact release artefacts, rollback/roll-forward decision, and a stabilisation/handover plan. One unresolved NO-GO means no launch.

## Dependencies and parallelism

Gate order is controlling. A later gate may prepare research or fixtures only when it cannot create code, schema, alternate truth, or premature integration. Gate 6 design/fixtures may be prepared alongside Gates 4–5 after Gate 2, but publication remains gate-bound. Observability, security, accessibility, recovery, and test fixtures are continuous acceptance work.

## Timeline

No delivery estimate is authorised. A credible estimate requires named people, available capacity, work-package estimates, external approval lead times, uncertainty ranges, and a stabilisation period. See `docs/TIMELINE_CAPACITY.md`.

## Completion reporting

For every change report:

- exact changed paths;
- created versus implemented versus verified capability;
- exact commands and outcomes;
- migration range and data impact;
- live-provider and production-resource confirmation;
- unresolved failures, owners, and smallest next action;
- diff/commit state.

No percentage-complete claim is permitted without an explicit denominator.
