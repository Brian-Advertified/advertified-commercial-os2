**ADVERTIFIED**

**Advertified Unified**

Production Build Specification & AI Implementation Handoff

**Build authority:** v1.1 converts the approved product blueprint and historical Advertified decisions into one implementation specification. It defines the canonical domain, contracts, states, permissions, screens, agent boundaries, production controls, test gates, traceability and execution protocol required to build Advertified without silently inventing core behaviour.

| **Document** | **Detail**                                                                                            |
|--------------|-------------------------------------------------------------------------------------------------------|
| Audience     | AI implementation systems, engineering, product, commercial and operations teams                      |
| Status       | Build-ready master specification — repository evidence must still confirm implementation status       |
| Benchmark    | Omnicom Omni — adopt patterns, not scale claims or proprietary data assumptions                       |
| Scope        | Authenticated product, agent workflows, inventory, proposals, marketplace and production requirements |
| Version      | v1.1 — 28 August 2026                                                                                 |

*Controlled production build specification*

# Document map

| **Sections** | **Purpose**                                                                                 |
|--------------|---------------------------------------------------------------------------------------------|
| 1–3          | Decisions, benchmark and product scope                                                      |
| 4–5          | Users, access and the canonical commercial domain                                           |
| 6            | Agents, deterministic services and human gates                                              |
| 7            | End-to-end workflows                                                                        |
| 8            | Authenticated screen blueprint and dashboard direction                                      |
| 9–10         | Production architecture and inventory ingestion                                             |
| 11–12        | Security, non-functional requirements and roadmap                                           |
| 13–15        | Metrics, risks, locked build decisions and sources                                          |
| 16–20        | AI authority, stack, data model, lifecycle and authorisation                                |
| 21–25        | API, agents, inventory, screens and asynchronous operations                                 |
| 26–31        | Integrations, production, testing, backlog, AI execution prompt and historical traceability |

**Reading note:** Part I states the product and architecture. Part II is normative for implementation. Repository inspection determines what already exists, but legacy behaviour cannot override the clean build decisions in this document.

# 1. Executive decisions

**Canonical truth:** The Commercial API owns business state. Agents and deterministic services may propose or execute through explicit tools, but they do not mutate product databases directly.

| **Decision**                   | **Locked direction**                                                                                                                                                                          |
|--------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Brief remains canonical        | Every campaign has a CampaignBrief aggregate with immutable BriefVersions, evidence lineage, unknowns, assumptions and named human approval.                                                  |
| Evidence before interpretation | Website, file and supplier evidence is captured and approved before strategy, audience, inventory or proposal agents use it.                                                                  |
| Agents are not services        | Orchestration, crawling, extraction, eligibility, benchmarking, forecasting, document generation and notifications are deterministic services or tools unless judgment is genuinely required. |
| Approval before consequence    | Human approval is mandatory before client delivery, supplier commitment, budget spend, creative publication, external outreach or material commercial change.                                 |
| No identity-graph dependency   | Advertified begins with tenant, client, opportunity, brief, inventory and evidence truth. Omnicom-scale consumer identity and real-time attribution are optional future integrations.         |
| Clean redevelopment            | Legacy code and screens are read-only references for assets, business rules, contracts, migration data and known failure lessons. They are not the release gate for the new application.      |
| Customer-facing language       | Authenticated screens explain decisions and next actions in commercial language. Internal implementation wording never appears on human-facing pages.                                         |

# 2. Omni benchmark: adopt, adapt and defer

Omnicom describes Omni as an end-to-end marketing and sales intelligence platform connecting strategy, creativity, media, CRM, commerce, data and AI. The relevant lesson is the operating pattern: shared truth, workflow-spanning agents, interoperability and human control. Advertified should not claim equivalent identity scale, signals or activation coverage.

| **Omni pattern**                          | **Advertified stance** | **Application**                                                                                                                    |
|-------------------------------------------|------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| Shared foundation                         | Adopt                  | One Commercial API and evidence model across opportunity, brief, planning, inventory, proposal and booking.                        |
| Agents run through workflows              | Adopt                  | Specialists produce typed artefacts and handoffs inside canonical lifecycle stages—not a floating chatbot.                         |
| Human expertise remains central           | Adopt                  | Named approval gates for strategy, brief, media mix, plan, proposal, supplier commitment and external action.                      |
| Interoperability                          | Adopt                  | Open adapters and versioned contracts for clients, suppliers, CRM, maps, measurement, payments and media systems.                  |
| Acxiom-style consumer identity            | Adapt                  | Start with account, evidence, audience hypothesis and inventory truth; connect licensed audience data when commercially justified. |
| Predictive ROI and real-time reallocation | Defer                  | Enable only where measurement quality, activation integrations and client permission support defensible automation.                |
| Personalized production at scale          | Defer                  | Creative concepts may be drafted now; automated production and publishing require brand, legal and asset pipelines.                |

*Public benchmark source: Omnicom, ‘Omni — Omnicom’s Marketing and Sales Intelligence Platform,’ https://www.omc.com/omni/ (accessed 28 August 2026).*

# 3. Product scope and current baseline

## 3.1 Product promise

**Advertified's job:** Turn a business opportunity or approved brief into an explainable, commercially viable media plan and proposal using verified inventory, while keeping people in control of judgment, negotiation and spend.

## 3.2 Known platform baseline

| **Capability**                | **Baseline direction**                                                                                                                            |
|-------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------|
| Authenticated web application | Role-scoped working surfaces for internal teams, agencies, advertisers, suppliers and influencers.                                                |
| Commercial API                | Canonical owner of tenants, opportunities, briefs, inventory, plans, proposals, bookings, approvals and audit state.                              |
| Agent runtime                 | Dispatches specialist work through typed tools; deterministic provider is the test default; paid models are explicitly enabled and cost-recorded. |
| Data                          | PostgreSQL/PostGIS for commercial and geographic truth; pgvector for retrieval where evidence-backed semantic search is useful.                   |
| Inventory corpus              | Multi-channel supplier files, rate cards, logos, images, terms, rates, availability and evidence requiring ingestion and human review.            |
| Deployment                    | Containerised development and production workflows; cloud deployment and operational controls must be release-gated.                              |

## 3.3 Explicit non-goals for the initial release

- Building an Omnicom-scale consumer identity graph before the core commercial workflow works end to end.

- Autonomous spend, publication, supplier commitment or outreach without a named human approval policy.

- Rehabilitating or certifying the complete legacy application and legacy test suite.

- Producing generic strategies that are not traceable to approved evidence, client context and available inventory.

- Loading inventory through manual one-off fixes that cannot be repeated for the next unseen supplier file.

# 4. Users and access model

Five external role families share the platform, but internal and agency permissions must be granular. Tenant isolation is enforced by the Commercial API and database policies—not by hiding menu items.

| **Role**                     | **Primary responsibilities**                                                   | **Visibility**                                                                        |
|------------------------------|--------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| Advertified Admin            | Tenant, roles, fees, integrations, agents and platform controls                | All authorised tenants; sensitive finance and security remain separately permissioned |
| Advertified Agent / Planner  | Opportunities, briefs, strategy, plans, proposals and client collaboration     | Assigned accounts and work queues                                                     |
| Inventory Operations         | Imports, extraction review, evidence, rates, availability and supplier quality | Inventory and supplier data required for assigned channels                            |
| Agency Admin / Campaign User | Manage assigned advertisers, submit briefs, review plans and proposals         | Only assigned advertiser workspaces                                                   |
| Advertiser Admin / Approver  | Submit briefs, approve strategy/plan/proposal, view campaigns and results      | Own organisation and approved shared data                                             |
| Supplier Admin / User        | Publish inventory, maintain rates/availability, respond to RFQs and bookings   | Own organisation, listings and transactions                                           |
| Influencer / Representative  | Maintain profile and rate card, respond to requests, manage deliverables       | Own or represented profiles, requests and earnings                                    |

**Authorisation invariant:** Every tool call carries tenant, actor, role, resource and correlation identifiers. The Commercial API independently re-authorises the requested action before reading or writing protected state.

# 5. Canonical commercial domain

<img src="media/image1.png" style="width:6.65in;height:2.82625in" alt="Diagram of Advertified&#39;s canonical commercial lifecycle from Opportunity through Campaign" />

*Figure 1. Each artefact is versioned and evidence-linked; blue gates represent named human approvals.*

| **Aggregate**                  | **Purpose**                        | **Required truth**                                                                                                               |
|--------------------------------|------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| Opportunity                    | Prospect or commercial opening     | Approved source evidence, business interpretation, owner, status                                                                 |
| EvidenceItem / EvidenceSet     | Immutable provenance               | Source, capture time, locator, extracted fact, confidence, reviewer decision                                                     |
| StrategyVersion                | Commercial growth thesis           | Problem, opportunity, audience hypotheses, objectives, recommendations, objections                                               |
| CampaignBrief / BriefVersion   | Canonical campaign intent          | Client/tenant, source, objective, audience, geography, timing, money/VAT, constraints, measurement, unknowns, evidence, approval |
| AudienceDefinition             | Reviewed segment interpretation    | Segments, rationale, evidence, exclusions, confidence, size source                                                               |
| MediaMixVersion                | Channel and budget direction       | Channel roles, allocations, assumptions, rationale, approval                                                                     |
| InventoryCandidate / Shortlist | Eligible supply                    | Product, evidence, rate, availability, score, rejection reasons, preference                                                      |
| MediaPlanVersion               | Executable planning recommendation | Placements, flighting, budget, VAT/fees, forecast, supply status, approval                                                       |
| ProposalVersion                | Client-facing commercial offer     | Three distinct tiers, scope, outcomes, inventory, fees, assumptions, expiry, approval                                            |
| Booking / Campaign             | Accepted commitment and delivery   | Supplier terms, schedule, creative, proof, delivery, measurement                                                                 |

## 5.1 CampaignBrief is a working aggregate

Briefs are not five-field CRUD. A CampaignBrief contains immutable BriefVersions. New evidence or a changed decision creates a new version; it never silently rewrites what was approved.

| **Field group**    | **Required content**                                                                           |
|--------------------|------------------------------------------------------------------------------------------------|
| Identity           | Tenant, client, owning team, source opportunity or tender                                      |
| Commercial problem | Business problem, objective, desired outcome and decision context                              |
| Audience           | Known segments, hypotheses, buying context, exclusions and confidence                          |
| Delivery           | Geography, routes/POIs where relevant, timing, flighting and channel constraints               |
| Money              | Typed currency, amount, VAT status, agency commission, management fees and payment constraints |
| Measurement        | Success measures, baseline, attribution limitations and reporting expectations                 |
| Knowledge state    | Known facts, unknowns, assumptions, conflicts, evidence lineage and reviewer decisions         |
| Governance         | Lifecycle status, routing attributes, named approver, approval time and superseded version     |

## 5.2 Domain invariants

- An unbriefed opportunity produces a reviewed draft BriefVersion from approved evidence; it never bypasses the Brief stage.

- A proposal cannot be approved if its media plan, pricing evidence or material assumptions have changed since review.

- Rates and availability are versioned with source and effective dates; stale supply is visible, not silently reused.

- Every rejected inventory candidate retains a structured rejection reason for audit, learning and user explanation.

- A failed or resumed run reuses approved artefacts and does not repeat a paid model call unless its inputs materially changed and policy permits it.

# 6. Agent operating model

**Separation rule:** Use an AI agent only where interpretation, synthesis or judgment is required. Use deterministic code for validation, calculations, permissions, state transitions, eligibility, document rendering and repeatable extraction controls.

| **Agent**                | **Question**                                                       | **Typed output**                                                               | **Human gate**                              | **Status**          |
|--------------------------|--------------------------------------------------------------------|--------------------------------------------------------------------------------|---------------------------------------------|---------------------|
| Opportunity Intelligence | What credible advertising opportunity exists?                      | Ranked opportunity angles linked to approved evidence                          | Human selects or rejects angle              | Current / near-term |
| Business Interpretation  | What does this business sell, to whom, and in what buying context? | Business model, customer groups, occasions, geography, unknowns and hypotheses | Human confirms material interpretation      | Current / near-term |
| Strategy                 | What growth and communications strategy follows from the evidence? | StrategyVersion with objectives, audience hypotheses and channel implications  | Strategy approval                           | Current / near-term |
| Brief Drafting           | How does approved evidence become a complete campaign brief?       | Draft BriefVersion preserving unknowns, assumptions and lineage                | Brief approval                              | Current / near-term |
| Audience                 | Which audiences are plausible and why?                             | Evidence-backed AudienceDefinitions with confidence and exclusions             | Audience / strategy review                  | Near-term           |
| Inventory Intelligence   | Which verified products are eligible and valuable?                 | Scored candidates, evidence and rejection reasons                              | Inventory selection / supplier confirmation | Current / near-term |
| Media Planning           | How should channels, budget and flighting work together?           | MediaMixVersion and MediaPlanVersion                                           | Media-mix and plan approval                 | Current / near-term |
| Critic & Readiness       | What is weak, unsupported, contradictory or unsafe?                | Immutable objections, severity, evidence gaps and readiness decision           | Human resolves and approves                 | Current / near-term |
| Proposal Narrative       | How should the approved plan be explained to the client?           | Client-ready tier narratives from approved structured facts                    | Proposal approval                           | Near-term           |
| Creative                 | What concepts and adaptations could support the approved plan?     | Draft concepts and format requirements                                         | Creative approval                           | Future              |
| Measurement              | What changed and what should be learned?                           | Evidence-backed performance interpretation and recommendations                 | Optimisation approval where consequential   | Future              |

## 6.1 Deterministic services and tools

| **Service or tool**               | **Responsibility**                                                                               |
|-----------------------------------|--------------------------------------------------------------------------------------------------|
| Commercial API                    | Canonical state, authorisation, versioning, audit and idempotent commands                        |
| Agent runtime / orchestrator      | Dispatch, health, retries, correlation, policy and tool routing; not a commercial decision-maker |
| Crawler and evidence capture      | Fetch allowed public pages, preserve source locators, deduplicate and respect policy             |
| Inventory ingestion               | Classify files, reconstruct tables, extract assets, normalize rows and create review tasks       |
| Eligibility and shortlist engine  | Apply hard constraints first; score only eligible candidates; retain rejection reasons           |
| Geography / route / POI resolvers | Resolve geographic requirements deterministically and expose uncertainty                         |
| Commercial benchmark engine       | Typed money, VAT, fees, rate comparison, freshness and comparable calculations                   |
| Supply / forecast service         | Availability, reach or outcome ranges with explicit source and confidence                        |
| Document assembler                | Generate branded proposals and PDFs only from approved structured artefacts                      |
| Notifications / human tasks       | Route named approvals and exceptions with clear next action                                      |
| Audit and AI cost ledger          | Record inputs, evidence, model/provider, token/cost, result, retry and approval outcome          |

## 6.2 Tool contract and memory rules

- All inputs and outputs use versioned schemas and stable identifiers; free-form text never becomes canonical state without validation.

- Agent memory is scoped to tenant and workflow. Approved commercial artefacts are read from the Commercial API, not hidden model memory.

- The audit log stores decision rationale, evidence references and structured outputs—not private chain-of-thought.

- Live provider use is policy-controlled, cost-capped and disabled in deterministic regression tests.

- Tool failures are classified as retryable, review-required or terminal; the user sees a business-safe recovery action.

# 7. End-to-end workflows

## 7.1 Unbriefed opportunity to approved brief

| **Step** | **Action**                                            | **Owner**                      | **Output**                   |
|----------|-------------------------------------------------------|--------------------------------|------------------------------|
| 1        | Capture prospect and permitted sources                | Human / crawler                | Opportunity + EvidenceSet    |
| 2        | Review, deduplicate and approve material evidence     | Human reviewer                 | Approved evidence            |
| 3        | Interpret business, customers, occasions and unknowns | Business Interpretation Agent  | Interpretation artefact      |
| 4        | Generate evidence-backed opportunity angles           | Opportunity Intelligence Agent | Ranked angles                |
| 5        | Create strategy and critic objections                 | Strategy + Critic agents       | StrategyVersion + objections |
| 6        | Resolve objections and approve the strategy           | Human strategist               | Approved StrategyVersion     |
| 7        | Draft a CampaignBrief without inventing unknown facts | Brief Drafting Agent           | Draft BriefVersion           |
| 8        | Review, edit and approve the brief                    | Human owner                    | Approved BriefVersion        |

## 7.2 Full campaign: approved brief to proposal

| **Step** | **Action**                                           | **Owner**                   | **Output**                     |
|----------|------------------------------------------------------|-----------------------------|--------------------------------|
| 1        | Define evidence-backed audiences and exclusions      | Audience Agent              | AudienceDefinitions            |
| 2        | Draft channel roles and budget ranges                | Media Planning Agent        | MediaMixVersion                |
| 3        | Approve media mix before expensive supply work       | Human planner               | Approved mix                   |
| 4        | Search inventory and apply deterministic eligibility | Inventory Agent + tools     | Candidates + rejection reasons |
| 5        | Resolve supply, availability, rates and forecasts    | Supply services + human ops | Confirmed shortlist            |
| 6        | Build media plan and run critic/readiness            | Media Planning + Critic     | PlanVersion + objections       |
| 7        | Resolve objections and approve plan                  | Human planner               | Approved MediaPlanVersion      |
| 8        | Build three materially different tiers               | Planning + Proposal Agent   | ProposalVersion                |
| 9        | Approve client wording, pricing and assumptions      | Named human approver        | Approved proposal              |
| 10       | Render branded PDF and record incremental AI cost    | Document + cost services    | Immutable deliverable + ledger |

## 7.3 Rapid OOH

- Resolve digital/static format, geography, routes, POIs, dates, budget and hard exclusions from the approved BriefVersion.

- Search verified OOH inventory, apply deterministic eligibility and preserve every rejection reason.

- AI ranks only eligible products; a human may replace selections and must see the rationale and evidence.

- Supplier availability and current rates are confirmed, versioned and attached before the shortlist becomes a media plan.

- If the brief expands beyond rapid OOH, create a full-campaign child workflow without losing the approved evidence or brief lineage.

## 7.4 Inventory import and supplier operations

| **Stage** | **What happens**                                                | **Output**                                |
|-----------|-----------------------------------------------------------------|-------------------------------------------|
| Upload    | Supplier or internal user uploads PDF/XLSX/CSV/images           | Immutable source file and import run      |
| Classify  | Detect supplier, channel, document type and extraction strategy | Routing decision with confidence          |
| Extract   | Reconstruct tables and assets with coordinates/evidence         | Normalized candidates, logos and images   |
| Validate  | Apply deterministic schema, money, date and geometry checks     | Errors, warnings and review tasks         |
| Review    | Human confirms material facts and resolves low confidence       | Approved facts and rejected suggestions   |
| Publish   | Create versioned inventory, rates and availability              | Searchable catalogue and evidence lineage |
| Maintain  | Supplier updates availability/rates or responds to RFQ          | Fresh versioned commercial state          |

