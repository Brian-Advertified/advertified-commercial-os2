# Advertified ordered implementation plan

**Plan version:** 3.1
**Evidence date:** 2026-09-01
**Normative product source:** `docs/spec/README.md`  
**Execution rules:** `AGENTS.md`  
**Current permission:** Sequential local non-production Gate 11 stabilisation, Gate 12 verification
and the named local developer database may continue under the standing owner direction; no live
provider, staging or production resource, deployment, commit, push or merge authority

This is the execution index, not a replacement for the full v1.1 specification. Every gate uses the applicable normative sections and the historical traceability/adversarial fixtures in Section 31.

## Current status

| Gate | State | Meaning |
|---:|---|---|
| 0 | Evidence passed locally | Baseline builds, tests, Compose, and extension health pass |
| 1 | Implemented locally; owner decision pending | Guardrails have repeatable local evidence; remote CI and final owner review remain pending |
| 2 | GO | Brian Rabuthu recorded local Gate 2 GO on 2026-08-29; publication and production reviews remain pending |
| 3 | GO | Brian Rabuthu recorded local Gate 3 GO on 2026-08-29; non-local reviews remain separate |
| 4 | Delivered locally | Repeatable evidence retained; Brian Rabuthu directed Gate 4 delivered on 2026-08-29 |
| 5 | Delivered locally | Canonical Brief implementation is retained; the latest connected clear-Brief journey reaches planning without a fabricated approval; latest-source full C# regression remains pending |
| 6 | Delivered locally | Inventory truth implementation and repeatable evidence complete; owner/independent review remains pending |
| 7 | Delivered locally | Canonical planning, editable media mix, PostGIS OOH benchmarking and the eleven-agent Inventory Intelligence explanation boundary are locally verified |
| 8 | Delivered locally | Proposal generation, approved-plan bindings, branded PDF and assigned client decision are verified locally |
| 9 | Verified locally; owner review pending | Canonical OOH-only mode and proposal inbox have repeatable local evidence; owner and independent review remain pending |
| 10 | Verified locally; owner review pending | Tenant-safe marketplace exchange, commercial policy, marketplace-to-plan lineage and selected-option Booking are implemented |
| 11 | Implemented locally; owner review pending | The canonical funding-to-measurement API is implemented; Campaign Delivery remains API-mocked browser evidence, while connected Brief/proposal paths now pass through the packaged local stack; independent review remains pending |
| 12 | In progress locally | Current Linux packaging, connected critical journeys and final-tree architecture pass; the latest full C# suite rerun, remote CI, staging and external/independent certification remain pending |
| 13 | Blocked | Gate 12, external launch evidence and a named human production GO remain unresolved |

There are fourteen gates numbered 0 through 13.

These rows distinguish authorised local implementation slices from formal gate exit. The recorded
directions for Gates 2–11 permitted and reviewed local work; they do not erase the still-pending
Gate 1 owner/remote-CI decision or satisfy the independent production exit conditions. No claim is
made that the sequence is formally closed end to end.

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

- Supplied Brief: preserve source → validate/extract/research → decide campaign mode when clear →
  ask a human only for materially unclear details → canonical BriefVersion. The current connected
  supplied-Brief journey proves that a clear Brief reaches planning without a fabricated approval;
  ambiguous material details remain the only human-correction boundary.
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

**Current status:** The immutable canonical Brief foundation remains delivered. The latest clear
supplied-Brief rule is now implemented in the current tree: the connected local journey preserves
and interprets a clear source, marks it ready and reaches planning without a fabricated approval or
second agency user. Material ambiguity remains the human-correction boundary. Historical Gate 5
evidence remains under `docs/evidence/gate-5/`; the complete latest-source C# regression is still
pending on a .NET 10.0.400-capable runner.

## Gate 6 — inventory truth

**Normative areas:** Sections 7.4, 10, 18.4, 21.4, 23  
**Outcome:** Supplier-agnostic files become reviewed, versioned, searchable inventory with source lineage.

**Current status:** Delivered and verified locally under `docs/GATE6_WORK_PACKET.md`.
Repeatable evidence is retained in `docs/evidence/gate-6/`. No live provider, production
resource, shared-database migration, external publication, or commercial commitment was used.

Pipeline: upload → quarantine → malware/type validation → classify → preserve → render → extract structure/assets/coordinates → normalise → validate → deduplicate/supersede → review → publish → evaluate.

No candidate becomes bookable without approved rates, availability, evidence, and freshness. Gate 6 must preserve the typed OOH/DOOH attributes and coordinates needed by later benchmarking, but it does **not** calculate market position or widen its bounded implementation into Inventory Intelligence.

## Gate 7 — canonical planning

**Normative areas:** Sections 7.2, 18.5, 21.3, 22  
**Outcome:** An approved Brief becomes approved audience, media mix, eligible shortlist, and MediaPlanVersion.

Hard constraints run before scoring. Money/VAT/fees are deterministic. Every rejection is retained. OOH/DOOH Inventory Intelligence must calculate transparent local peer benchmarks from exact published product/rate versions using governed spatial and commercial compatibility rules before an agent interprets value. It exposes cohort size, geography/radius, median/quartiles/percentile, above/below-market position, freshness, confidence and exclusions; any benchmark bound into a shortlist or plan is immutable and reproducible. Supply forecasts expose source and uncertainty. Critic objections must be resolved by a human.

The assigned agency operator may carry the work through planning without a fabricated second
agency user. Any required confirmation binds that exact operator and artefact version.

**Current status:** Delivered locally. Historical planning evidence is retained in
`docs/evidence/gate-7/`, and the latest Inventory Intelligence boundary is separately evidenced in
`docs/evidence/inventory-intelligence-agent-20260901/`. The planning workspace supports editable
allocations, media-specific multi-period flighting, schedule-aware pricing and PostGIS-backed
OOH/DOOH comparative intelligence. The deterministic runtime now exposes all eleven approved
handlers, and the connected proposal journey proves the validated Inventory Intelligence rationale
is persisted and visible before inventory selection without changing deterministic eligibility or
commercial calculations.

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

**Current status:** Verified locally; owner and independent review remain pending. Clear supplied Briefs receive an automatic campaign-mode decision; only ambiguous material details are presented to a human. A clearly named client is created during Brief intake, so client pre-registration is not required. The OOH-only selection stays on the canonical planning lifecycle and cannot expand in place. With explicit tenant-administrator opt-in, the configured Proposal inbox proves bounded provider submission of complete OOH-only proposals, idempotent no-duplicate handling, no-send review paths and correction of only the unresolved Brief field. It does not claim inbox delivery without provider delivery evidence. Repeatable evidence is retained under `docs/evidence/gate-9/`. No live provider, production resource, supplier contact, booking or financial commitment was used.

## Gate 10 — supplier marketplace

**Normative areas:** Sections 7.4, 18.4–18.5, 20.4, 21.4, 24, 26  
**Outcome:** Suppliers manage only their own listings, rates, availability, RFQs, and booking responses with freshness and audit.

Supplier silence produces an explicit unavailable/review state—never a fabricated response or one-off data correction.

**Current status:** The local Gate 10 vertical is implemented and verified. Suppliers publish
immutable projections of reviewed inventory; buyers discover only the public projection; RFQs and
responses remain visible only to their two tenant counterparties. Versioned commercial policy and
exact money/VAT calculations drive marketplace-to-plan lineage. A client-selected immutable plan
line can create a Booking only through an explicit buyer request and the addressed supplier's human
confirmation. No local send transition contacts a supplier or creates an external commitment.
Owner and independent review remain pending. Evidence is retained under `docs/evidence/gate-10/`,
`docs/evidence/gate10-commercial-policy-20260830/`,
`docs/evidence/gate10-marketplace-planning-lineage-20260830/` and
`docs/evidence/gate10-selected-option-booking-20260830/`.

## Gate 11 — campaign delivery and learning

**Normative areas:** Sections 5, 7, 18.5, 19.3, 21.4, 22, 25–26  
**Outcome:** Accepted work moves through funding, booking, readiness, live delivery, proof, measurement, and learning.

Every consequential transition requires the correct approval and immutable input version. Worker restarts, duplicate events, late proof, and partial measurement must be tested.

**Current status:** The canonical API implements funding, Booking coverage, versioned creative
production and separate brand/supplier review, creative readiness, explicit launch and completion,
supplier delivery proof, buyer proof review, performance evidence, measurement interpretation and
client report review. A bounded `GET /delivery-proof-requests` surface is implemented for authorised
supplier roles; it exposes only completed Campaign Bookings with confirmed supply and an explicit
proof request. Booking and Planning projections remove supplier-private cost and notes, plus internal
planning assumptions and objections, from client-facing responses. The API-mocked Campaign
Delivery browser UI/contract regression passes on desktop and compact viewports; it is not a
connected deployed-system E2E journey or a complete release matrix. Real integrated journey
evidence, owner review and independent review remain pending. No live provider, production payment,
supplier contact or production resource was used.

## Gate 12 — hardening and certification preparation

**Normative areas:** Sections 27–28, 31  
**Outcome:** Security, privacy, performance, recovery, observability, accessibility, and operations are independently evidenced.

This gate certifies controls introduced earlier; it cannot introduce missing tenancy, audit, approval, privacy, or recovery foundations at the end.

**Current status:** Local preparation now uses registry `2.12.0`, effective from 2026-09-01, with
matching generated C#, TypeScript and Python projections. Current hardening includes warning-free
explicit Vite 8 vendor splitting on both host and pinned Linux builds; fail-closed API readiness;
privacy-safe correlated telemetry; dependency audits and hash-locked Python graphs; isolated
PostgreSQL recovery; representative MinIO object-byte reconciliation; recovery/incident runbooks;
and reproducible Linux packaging for the API, migrator, agent runtime and web under non-root final
processes. Packaging evidence is retained in
`docs/evidence/gate12-reproducible-application-packaging-20260901/`.

Email proposal automation retains immutable requested/accepted delivery evidence and enforces the
global mode gate, exact provider-mode resolution, provider-owned sender trust, safe reply addressing,
confirmed-rejection classification, operator command provenance/idempotency and restart from a
persisted `PROCESSING` checkpoint. It never blindly resends an ambiguous or already requested
delivery. The tenant-bound transactional-outbox kernel claims one event with a database-clock lease,
heartbeats, fences acknowledgement/failure by claim token, applies bounded retry/dead-letter policy
and uses only a deterministic Development/Test transport. Production cross-tenant scheduling,
service identity and broker transport remain blocked. Docling transport rejects unsafe production
URLs and automatic redirects. GitHub action and dependency-image references are content-addressed;
application packaging is now locally exercised, while remote SBOM/vulnerability results, signing and
release provenance remain absent.

The latest retained complete Release API suite passed 128/128 in 3m57s, with provider/security
14/14 and email workflow recovery 11/11 on that source. Those counts predate the latest Inventory
Intelligence, Brief-readiness and packaging changes and remain historical evidence rather than a
claim about the newest source. On the latest source, the API and migrator publish successfully with
the pinned Linux .NET SDK 10.0.400; web lint, type-check, unit 6/6 and host/Linux production builds
pass; the retained API-mocked Playwright matrix is 32/32; and 4/4 connected local critical journeys
pass through the packaged web/API/PostgreSQL/deterministic-runtime stack, including keyboard/focus
accessibility semantics. The deterministic runtime
passes 31/31 and the complete final-tree architecture suite passes 42/42. The complete latest-source
C# suite remains locally blocked because the Windows host has SDK 10.0.103 while the repository
requires 10.0.400 with roll-forward disabled. Complete current-source CI secret scanning and remote
final-image SBOM/vulnerability results remain pending.

The first current-tree API run passed 123/126 and remained failed evidence. Regenerating the
OpenAPI contract corrected two failures, and pinning only the affected planning journey hosts to
their deterministic fixture clock corrected the third. A later Docling redirect-security case
increased the denominator; the full rerun then passed 128/128. An older dated run passed 107/109 and
also remained failed until its registry-date and stale-version assertions were corrected; the
historical 109/109 evidence pack is retained rather than rewritten.

The last exact retained migration-history capture records migrations 001 through 026. On 2026-09-01
the existing `advertified-dev` migration job exited successfully after rebuilding against the
current migration graph, and bootstrap/seed completed before healthy API startup. This pass did not
retain a direct post-run migration-history query, so it does not invent an exact 027/028 applied
history claim. No staging or production database changed. PostgreSQL-backed browser-session
durability is now restart-verified locally; production Cognito/OIDC code flow, provider logout/refresh,
MFA policy and provider-token protection remain pending. Official Resend trust and reconciliation,
a background email worker, production outbox scheduler/broker/consumers, production-shaped staging
E2E, remote CI, immutable signed release
artefacts, central telemetry/alerts, staging, managed recovery and measured RPO/RTO, full automated
WCAG/manual accessibility certification, performance/load evidence, Security/Privacy/Legal decisions, named Operations ownership and
independent certification remain blocked or unverified.

## Gate 13 — production launch

**Normative areas:** Sections 27.5, 28.3–28.4, 31  
**Outcome:** A named cross-functional team—not an AI—records production GO after all release evidence passes.

Required evidence includes the thirty-case zero-live-provider certification, migration/restore proof, runbooks, alarms, security/privacy/legal decisions, exact release artefacts, rollback/roll-forward decision, and a stabilisation/handover plan. One unresolved NO-GO means no launch.

**Current status:** BLOCKED. No production deployment or production greenlight is authorised.

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
