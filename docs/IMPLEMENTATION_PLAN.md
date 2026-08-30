# Advertified ordered implementation plan

**Plan version:** 3.0  
**Evidence date:** 2026-08-29  
**Normative product source:** `docs/spec/README.md`  
**Execution rules:** `AGENTS.md`  
**Current permission:** Gate 9 canonical OOH-only campaign-mode verification may continue locally under the standing sequential-delivery direction; no live provider, shared-database, deployment, push or merge authority

This is the execution index, not a replacement for the full v1.1 specification. Every gate uses the applicable normative sections and the historical traceability/adversarial fixtures in Section 31.

## Current status

| Gate | State | Meaning |
|---:|---|---|
| 0 | Evidence passed locally | Baseline builds, tests, Compose, and extension health pass |
| 1 | Implemented locally; owner decision pending | Guardrails have repeatable local evidence; remote CI and final owner review remain pending |
| 2 | GO | Brian Rabuthu recorded local Gate 2 GO on 2026-08-29; publication and production reviews remain pending |
| 3 | GO | Brian Rabuthu recorded local Gate 3 GO on 2026-08-29; non-local reviews remain separate |
| 4 | Delivered locally | Repeatable evidence retained; Brian Rabuthu directed Gate 4 delivered on 2026-08-29 |
| 5 | Delivered locally | Canonical Brief implementation and repeatable evidence complete; owner/independent review remains pending |
| 6 | Delivered locally | Inventory truth implementation and repeatable evidence complete; owner/independent review remains pending |
| 7 | Delivered locally | Canonical planning, editable media mix, PostGIS OOH benchmarking and repeatable evidence complete |
| 8 | Delivered locally | Proposal generation, approved-plan bindings, branded PDF and assigned client decision are verified locally |
| 9 | Verified locally; owner review pending | Canonical OOH-only mode and proposal inbox have repeatable local evidence; owner and independent review remain pending |
| 10–13 | Blocked | No implementation until the preceding gate is verified and its exact work packet exists |

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
- Solo-agency continuity: one active agency administrator can own the operational path from
  supplied Brief through Proposal preparation. Internal same-agency hand-offs are optional;
  client decisions and consequential spend or booking controls remain explicit later boundaries.
- OOH-only campaign: the same Brief → Plan → Proposal lifecycle with selectable media restricted to OOH/DOOH; complete unambiguous inbound Briefs may use the governed proposal-inbox automation.
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
- API contract tests, migration upgrade/restore tests, and cross-tenant denial tests;
- versioned OpenAPI contracts with Zod validation whenever the browser first consumes an API boundary.

Tests are limited to acceptance rules, domain invariants, security boundaries, migrations, and real regressions. Equivalent cases are parameterised; framework behavior and test-count padding are excluded.

Authentication provider choice, browser session model, CSRF, logout, and service identity must be owner-approved before implementation.

## Gate 3 — authenticated application shell

**Normative areas:** Sections 8, 20, 24  
**Outcome:** Each supported role enters an accessible, responsive, tenant-correct shell.

Required behavior:

- approved authentication/session architecture;
- role/tenant context and server-authoritative navigation;
- dashboards and work queues backed by real API contracts;
- loading, empty, stale, forbidden, error, and recovery states;
- simple, task-led pages with the commercial detail needed to act;
- purposeful charts, graphs, icons, and reduced-motion-safe animation where they explain comparison, trend, status, or workflow;
- Zod at forms, route parameters, browser storage, and API-response boundaries;
- one NotificationService backed by the approved Toastr adapter; components never call the library directly;
- stable API error codes mapped to human-sensible content; no internal exceptions, provider messages, database wording, stack traces, or private workflow terminology;
- accessibility checks and authenticated Playwright journeys.

The current Gate 0 page is not this gate.

## Gate 4 — evidence and opportunity

**Normative areas:** Sections 6, 7.1, 10, 18.3, 21.2–21.3, 22–23  
**Outcome:** An unbriefed prospect reaches reviewed evidence, interpretation, selected opportunity angle, and approved strategy.

Required order is Business Interpretation → Opportunity Intelligence → Strategy plus Critic. Evidence approval, unknowns, citations, prompt-injection defenses, retries, checkpoints, and deterministic agent evaluations are mandatory.

**Current status:** Delivered and verified locally. The retained report and manifest are in
`docs/evidence/gate-4/`. Brief drafting and approval remain Gate 5.

## Gate 5 — canonical Brief

**Normative areas:** Sections 5.1–5.2, 7.1, 18.3, 19.2, 21.2, 24  
**Outcome:** Both supplied-Brief and Opportunity paths produce an immutable, comparable,
agency-confirmed BriefVersion without inventing missing facts. Advertiser approval begins at
Proposal / Client Decision, not at Brief interpretation.

Source facts, interpretation, hypotheses, planning assumptions, confirmations, and unknowns remain distinct. Preliminary audience direction is not completed audience research.

**Current status:** Delivered and verified locally. The retained report and manifest are in
`docs/evidence/gate-5/`. One assigned agency operator can take a supplied or Opportunity-backed
Brief through confirmation without an advertiser or second agency user.

## Gate 6 — inventory truth

**Normative areas:** Sections 7.4, 10, 18.4, 21.4, 23  
**Outcome:** Supplier-agnostic files become reviewed, versioned, searchable inventory with source lineage.

**Current status:** Delivered and verified locally under `docs/GATE6_WORK_PACKET.md`.
Repeatable evidence is retained in `docs/evidence/gate-6/`. No live provider, production
resource, shared-database migration, external publication, or commercial commitment was used.

Pipeline: upload → quarantine → malware/type validation → classify → preserve → render → extract structure/assets/coordinates → normalise → validate → deduplicate/supersede → review → publish → evaluate.

No candidate becomes bookable without approved rates, availability, evidence, and freshness. Gate 6 must preserve the typed OOH/DOOH attributes and coordinates needed by later benchmarking, but it does **not** calculate market position or widen its bounded implementation into Inventory Intelligence.

## Canonical planning

**Normative areas:** Sections 7.2, 18.5, 21.3, 22  
**Outcome:** An approved Brief becomes approved audience, media mix, eligible shortlist, and MediaPlanVersion.

Hard constraints run before scoring. Money/VAT/fees are deterministic. Every rejection is retained. OOH/DOOH Inventory Intelligence must calculate transparent local peer benchmarks from exact published product/rate versions using governed spatial and commercial compatibility rules before an agent interprets value. It exposes cohort size, geography/radius, median/quartiles/percentile, above/below-market position, freshness, confidence and exclusions; any benchmark bound into a shortlist or plan is immutable and reproducible. Supply forecasts expose source and uncertainty. Critic objections must be resolved by a human.

The assigned agency operator may carry the work through planning without a fabricated second
agency user. Any required confirmation binds that exact operator and artefact version.

**Current status:** Delivered and verified locally. Repeatable evidence is retained in
`docs/evidence/gate-7/`. The planning workspace supports editable allocations, media-specific
multi-period flighting, schedule-aware pricing and PostGIS-backed OOH/DOOH comparative intelligence.

## Gate 8 — proposal and client decision

**Normative areas:** Sections 7.2, 18.5, 19.2, 21.3, 24, 27–28  
**Outcome:** An approved plan becomes immutable, commercially reconciled options, an approved branded document, and an audited client decision.

A full campaign may present one to three materially different proposal options, each bound to a distinct approved MediaPlanVersion. Platform packages LAUNCH, BOOST, SCALE and DOMINANCE remain separate master data and are never used as cosmetic proposal-option identities. Exact plan, rates, assumptions, wording and pricing versions are bound before rendering. No changed plan may reuse approval.

The same assigned agency operator may prepare and internally confirm the proposal. The first
required advertiser/client decision is the explicit Proposal / Client Decision boundary.

**Current status:** Delivered and verified locally. Repeatable evidence is retained in
`docs/evidence/gate-8/`. One to three genuinely different approved plan routes can be prepared,
approved, rendered as a deterministic branded PDF, shared through the local delivery boundary,
and selected or declined by an authorised client user. No live provider or production resource
was used.

## Gate 9 — OOH-only campaign mode and proposal inbox

**Normative areas:** Sections 5–8, 10, 18.5, 21.3–21.4, 22–24, 31.2
**Outcome:** OOH uses the same approved Brief → STP → media mix → verified inventory → approved plan → proposal lifecycle, with the selected media permanently restricted to OOH/DOOH.

The `OOH_ONLY` selection is immutable. It cannot be widened into a full campaign; adding another media type requires a completely new campaign from the beginning and no planning artefact is carried across. A configured inbound mailbox may execute the same canonical flow and send the approved PDF without per-request user input only when the source is complete and unambiguous, STP is explicit, every selected site has current confirmed supply, all material objections are resolved, the reply address is safe and the sender policy allows it. Every other message stops in a visible review-required state and nothing is sent.

**Current status:** Verified locally; owner and independent review remain pending. Clear supplied Briefs receive an automatic campaign-mode decision; only ambiguous material details are presented to a human. A clearly named client is created during Brief intake, so client pre-registration is not required. The OOH-only selection stays on the canonical planning lifecycle and cannot expand in place. The configured Proposal inbox proves complete zero-touch OOH delivery, exactly-once reply, no-send review paths and correction of only the unresolved Brief field. Repeatable evidence is retained under `docs/evidence/gate-9/`. No live provider, production resource, supplier contact, booking or financial commitment was used.

## Gate 10 — supplier marketplace

**Normative areas:** Sections 7.4, 18.4–18.5, 20.4, 21.4, 24, 26  
**Outcome:** Suppliers manage only their own listings, rates, availability, RFQs, and booking responses with freshness and audit.

Supplier silence produces an explicit unavailable/review state—never a fabricated response or one-off data correction.

**Current status:** The first local marketplace exchange vertical is implemented and verified:
suppliers publish immutable projections of reviewed inventory, buyers discover only the public
projection, RFQs are visible only to their two tenant counterparties, suppliers submit an
attributable immutable response and buyers accept its exact unexpired version. The local send
transition performs no external communication, and acceptance creates no booking. Booking,
commercial settings and the remaining Gate 10 scope are not implemented; owner and independent
review of this vertical remain pending. Evidence is retained under `docs/evidence/gate-10/`.

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
