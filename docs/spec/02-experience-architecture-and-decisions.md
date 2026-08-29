# 8. Authenticated experience blueprint

**Experience principle:** Users start with the commercial outcome and the next decision. They submit a brief—not implementation details. Every page has one primary action, visible status, evidence where relevant and a clear recovery path.

<img src="media/image2.png" style="width:6.65in;height:4.87667in" alt="Five role-specific Advertified dashboard wireframes" />

*Figure 2. Dashboard direction: role-specific KPIs, a priority queue and one obvious primary action. Navy and electric blue remain the core visual system; dark green is not part of the interface theme.*

## 8.1 Global authenticated surfaces

| **Route**      | **Page purpose**                               | **Primary action**         | **Required states**                                      |
|----------------|------------------------------------------------|----------------------------|----------------------------------------------------------|
| /sign-in       | Sign in securely                               | Sign in                    | Invalid credentials, locked account, service unavailable |
| /invite/:token | Accept invitation and set up access            | Join workspace             | Expired, already used, wrong account                     |
| /workspaces    | Choose an authorised organisation              | Open workspace             | No access, pending invitation                            |
| /home          | Show role-specific outcomes and priority queue | Open highest-priority task | Empty queue, stale metrics, partial service              |
| /tasks         | Review approvals and exceptions                | Open task                  | Overdue, reassigned, blocked dependency                  |
| /notifications | Explain changes that require attention         | Open related record        | Read/unread, delivery failure                            |
| /profile       | Manage personal settings and security          | Save changes               | Validation and re-authentication                         |

## 8.2 Internal Advertified surfaces

| **Route**                     | **Page purpose**                                          | **Primary action**     |
|-------------------------------|-----------------------------------------------------------|------------------------|
| /opportunities                | Opportunity pipeline                                      | Create opportunity     |
| /opportunities/:id            | Evidence, interpretation, status and next decision        | Review evidence        |
| /opportunities/:id/evidence   | Source facts, conflicts and provenance                    | Approve evidence       |
| /opportunities/:id/strategy   | Strategy, objections and readiness                        | Approve strategy       |
| /briefs                       | Brief pipeline and lifecycle                              | Start brief            |
| /briefs/:id                   | Current approved brief and workflow status                | Continue workflow      |
| /briefs/:id/review            | Review unknowns, assumptions and changes                  | Approve version        |
| /briefs/:id/versions          | Immutable version history and comparison                  | Open version           |
| /audiences/:id                | Audience definitions and evidence                         | Approve audiences      |
| /media-mix/:id                | Channel roles, allocations and rationale                  | Approve media mix      |
| /inventory/intelligence       | Search, benchmark and compare verified supply             | Search inventory       |
| /inventory/items/:id          | Inventory truth, evidence, rates, assets and versions     | Edit / review item     |
| /inventory/imports            | Import runs and quality status                            | Upload inventory       |
| /inventory/imports/:id/review | Human review of extraction exceptions                     | Publish approved facts |
| /plans/:id/inventory          | Map/grid selection with eligibility and rejection reasons | Confirm shortlist      |
| /plans/:id/supply             | Availability, rates, supplier responses and forecast      | Resolve supply         |
| /plans/:id/review             | Plan, assumptions, critic objections and totals           | Approve media plan     |
| /proposals                    | Proposal pipeline and deadlines                           | Open proposal          |
| /proposals/:id                | Three-tier comparison and commercial detail               | Approve proposal       |
| /proposals/:id/document       | Branded document preview and versions                     | Generate final PDF     |
| /campaigns                    | Booked and active work                                    | Open campaign          |
| /campaigns/:id                | Bookings, creative, delivery and measurement              | Open next task         |
| /suppliers                    | Supplier health, coverage and quality                     | Open supplier          |
| /admin/agents                 | Dispatch health, failures, cost and configuration         | Resolve exception      |
| /admin/audit                  | Business and agent audit trail                            | Inspect event          |
| /admin/commercial             | Markups, management fees, VAT and approval policy         | Save policy            |
| /admin/access                 | Users, roles and tenant assignments                       | Invite user            |
| /admin/integrations           | Provider, email, maps and partner connections             | Configure integration  |

## 8.3 Agency and advertiser surfaces

| **Route**               | **Page purpose**                                  | **Primary action**       |
|-------------------------|---------------------------------------------------|--------------------------|
| /client/home            | Campaign and approval overview                    | Open priority approval   |
| /client/briefs          | Submit and monitor briefs                         | Start brief              |
| /client/briefs/:id      | Review complete brief in client language          | Approve / request change |
| /client/audiences/:id   | Review who the campaign is for and why            | Approve audiences        |
| /client/media-mix/:id   | Review channel roles and budget                   | Approve media mix        |
| /client/proposals/:id   | Compare Essential, Growth and Premium outcomes    | Select tier              |
| /client/campaigns/:id   | Track bookings, assets, delivery and next actions | Open next action         |
| /client/performance/:id | Understand outcomes, caveats and recommendations  | Download report          |
| /client/team            | Manage workspace members and approvals            | Invite member            |

## 8.4 Supplier and influencer surfaces

| **Route**                    | **Page purpose**                           | **Primary action**  |
|------------------------------|--------------------------------------------|---------------------|
| /supplier/home               | Listings, requests, freshness and earnings | Open urgent request |
| /supplier/inventory          | Manage own catalogue                       | Add inventory       |
| /supplier/inventory/new      | Create one listing with evidence           | Submit for review   |
| /supplier/imports            | Bulk upload and resolve errors             | Upload file         |
| /supplier/availability       | Maintain current availability              | Update availability |
| /supplier/requests           | Respond to RFQs and booking requests       | Open request        |
| /supplier/quotes/:id         | Confirm rate, terms and validity           | Submit quote        |
| /supplier/bookings           | Track accepted bookings and delivery       | Open booking        |
| /influencer/home             | Requests, deliverables and earnings        | Open request        |
| /influencer/profile          | Profile, audience evidence and rate card   | Update profile      |
| /influencer/requests         | Accept, decline or counter-propose         | Respond             |
| /influencer/deliverables/:id | Submit creative/proof and track approval   | Upload proof        |
| /influencer/earnings         | Track approved and pending payments        | Open payment        |

## 8.5 Screen-level rules

| **Rule**       | **Required behaviour**                                                                                                                  |
|----------------|-----------------------------------------------------------------------------------------------------------------------------------------|
| Language       | Lead with business meaning. Do not expose internal phrases such as browser boundaries, orchestration state or schema validation.        |
| Primary action | One dominant action placed after the user has enough information to decide; secondary actions remain visually subordinate.              |
| Evidence       | Show source, freshness and reviewer status where a decision depends on extracted or external information.                               |
| AI             | Explain what was recommended, why, confidence/limitations and what the person must decide. Never present AI text as unquestioned truth. |
| Status         | Use stable business states with owner and next action; avoid ambiguous labels such as processing without progress or recovery.          |
| States         | Every page defines loading, empty, validation, partial-service, permission, stale-data and recoverable-failure behaviour.               |
| Accessibility  | Keyboard navigation, clear focus, semantic headings, labels, contrast and non-colour status cues are release requirements.              |

# 9. Production architecture

<img src="media/image3.png" style="width:6.65in;height:3.74063in" alt="Layered Advertified production architecture with Commercial API as the only write boundary" />

*Figure 3. The Commercial API is the only canonical write boundary; agents and tools operate through authorised contracts.*

| **Boundary**   | **Responsibility**                                        | **Production requirement**                                                           |
|----------------|-----------------------------------------------------------|--------------------------------------------------------------------------------------|
| Web            | Authenticated React/Vite application                      | Role routes, API contracts, validation, notification service, accessible states      |
| Commercial API | Canonical domain and command boundary                     | RBAC, versions, lifecycle, idempotency, audit, typed money/VAT, tenant scope         |
| Agent runtime  | Specialist dispatch and tool policy                       | Health, retries, model/provider policy, cost, correlation, no direct database access |
| Data           | PostgreSQL/PostGIS + pgvector + object storage            | Commercial state, geography, retrieval indexes and immutable source files            |
| Workers        | Ingestion, crawling, rendering, notifications             | Durable queues, replay-safe jobs, dead-letter handling and human recovery            |
| Integrations   | Email, maps, suppliers, CRM, measurement, payment/finance | Adapter contracts, secrets, rate limits, provenance and fallback                     |

## 9.1 Command, event and recovery contract

- Every command includes tenant_id, actor_id, resource_version, idempotency_key and correlation_id.

- Domain events describe committed facts; agent completion does not equal business approval.

- Workers persist checkpoints before external calls and can resume without duplicating supplier messages, bookings, documents or paid inference.

- UI polling or subscriptions read durable workflow state; they do not infer completion from transient runtime health.

- Provider failures expose a safe retry or human fallback while preserving the last approved artefact.

# 10. Inventory and evidence pipeline

Inventory intelligence is a core product capability, not a late marketplace add-on. The pipeline must work for unseen supplier documents without manual code changes and must preserve enough evidence for a human to confirm every material fact.

| **Stage** | **Controls**                                                                            | **Durable output**              |
|-----------|-----------------------------------------------------------------------------------------|---------------------------------|
| Acquire   | Upload/API/email intake, immutable file hash, supplier and tenant context               | Source file and import run      |
| Route     | Classify channel and document structure; choose parsing strategy                        | Routing decision and confidence |
| Parse     | Text, coordinate-aware tables, merged cells, images, logos and page regions             | Raw extracted evidence          |
| Normalize | Supplier-agnostic schemas for products, rates, dates, contacts, terms and assets        | Candidate facts with lineage    |
| Validate  | Typed money/VAT, dates, duplicates, geometry, required fields and cross-row consistency | Errors, warnings and confidence |
| Review    | Prioritised human tasks for material exceptions; bulk accept only with evidence         | Reviewer decisions              |
| Publish   | Versioned inventory, rate cards, availability and evidence                              | Searchable catalogue            |
| Monitor   | Freshness, supplier changes, expiry, conflicts and extraction quality                   | Tasks and quality metrics       |

## 10.1 Channel coverage

The canonical inventory model supports OOH/DOOH, radio, TV, print, digital/social, influencer, experiential, podcasts, retail, transit, airport, mall, email and mobile. Channel-specific extensions hold the facts that do not generalise; the base commercial fields remain comparable.

## 10.2 Large-catalogue and evidence requirements

- Server-side pagination, filtering and grouping must remain usable beyond 10,000 products.

- Inventory groups by owner, channel, station/publication/network and geography while retaining item-level truth.

- Logos and OOH imagery are extracted, reviewed, versioned and displayed on detail pages—not manually patched into the UI.

- Rates, availability, valid-from/to, supplier contacts and terms remain evidence-linked and freshness-scored.

- Benchmarking separates verified facts, calculated comparables and AI interpretation so users can challenge each layer.

## 10.3 OOH comparative intelligence

OOH/DOOH product detail and Inventory Intelligence must answer a commercial question that raw inventory search cannot: **how does this placement fare against genuinely comparable supply in the same market?** The comparison is a deterministic decision-support capability, not an AI-generated score.

- Gate 6 captures and publishes benchmark-ready inventory truth. Comparative calculations start only from published product/rate versions and belong to Gate 7 planning / Inventory Intelligence; they must not expand the bounded Gate 6 ingestion implementation.
- The target placement and every member of its peer cohort retain exact product version, rate version, evidence, freshness and distance/proximity basis so a benchmark used in a shortlist, plan or proposal is reproducible later.
- OOH proximity uses PostGIS spatial data when verified coordinates exist. Default cohort expansion is configurable and progressively widens from a close radius to wider radius, locality and municipality only until enough compatible peers exist. The UI always states the actual comparison area used.
- A product is comparable only when channel, digital/static state, compatible format/structure class, rate basis, currency/VAT treatment and effective period can be compared without misleading normalization. Dimensions/display area, illumination, digital loop/share, road/route context and measurement basis further constrain or qualify the cohort where present.
- Rate normalization is deterministic and labelled. Never compare raw weekly, four-week, monthly, per-play, per-loop or package prices as if they were the same buying unit. Production, installation and other one-off costs remain separate unless both sides use the same evidenced basis.
- Verified traffic, reach, impressions, footfall or audience measurements may produce efficiency metrics such as cost per thousand only when source, period, unit and methodology are compatible. Missing or incompatible measurements produce `not comparable`; they are never inferred.
- Required benchmark facts include peer count, median, quartiles, min/max, target percentile, percentage above/below median, rate freshness distribution, actual distance/range and the exact filters/normalizations applied. Small or weak cohorts are labelled low-confidence or insufficient rather than padded with incompatible inventory.
- A simple human-facing position such as `strong value`, `market-aligned` or `above market` may be derived from governed thresholds, but the underlying facts must remain visible. Do not make an opaque composite score the only explanation.
- AI may summarise the deterministic facts in plain commercial language after calculation. It cannot select hidden peers, alter calculated values, invent measurement data or convert weak evidence into a confident claim.
- Product detail must show a `Market comparison` section and a `View comparable sites` action. The expanded experience shows the target and peers on a map/list, the cohort criteria, included peer facts, exclusions/reasons, freshness and source confidence.

# 11. Security, POPIA and non-functional requirements

| **Area**            | **Requirement**                                                                                    | **Initial gate**                                               |
|---------------------|----------------------------------------------------------------------------------------------------|----------------------------------------------------------------|
| Tenant isolation    | API and database-enforced resource scope; negative authorisation tests are launch-blocking         | No cross-tenant read/write in automated tests                  |
| POPIA and privacy   | Purpose limitation, minimal collection, retention controls, data-subject handling and legal review | Approved privacy assessment before production                  |
| Secrets             | Managed secrets, rotation, no credentials in browser storage, logs or agent prompts                | Automated secret scanning and rotation procedure               |
| Audit               | Immutable business events, evidence decisions, approvals, provider/model and cost                  | 100% of consequential actions auditable                        |
| Performance         | Paginated queries, indexes, bounded payloads and background workers                                | Proposed: p95 standard authenticated API reads under 2 seconds |
| Reliability         | Durable jobs, idempotency, retries, dead-letter recovery and degraded-mode UI                      | No duplicate external action under replay tests                |
| Backup and recovery | Encrypted backups and tested restore procedure                                                     | RPO/RTO approved and exercised before launch                   |
| Observability       | Structured logs, traces, metrics and alerts across web, API, runtime and workers                   | Correlation from user action to business outcome               |
| Accessibility       | WCAG 2.2 AA target for authenticated workflows                                                     | Automated and manual keyboard/screen-reader checks             |
| AI cost             | Per-run budget, provider policy, cache/reuse and final incremental cost ledger                     | No unapproved paid call; cost visible per proposal             |
| Testing             | Lean new-suite gates: unit, contract, integration, migration and focused Playwright journeys       | New release gate green; legacy disposition recorded separately |

# 12. Delivery roadmap

The roadmap follows commercial value and production risk. It does not defer briefs, inventory or opportunity intelligence behind an identity programme. Timing is assigned only after repository-backed sizing and dependency review.

| **Phase**                  | **Scope**                                                                                                  | **Exit criterion**                                                                         | **Horizon**         |
|----------------------------|------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|---------------------|
| A. Clean foundation        | Commercial API aggregates, RBAC, immutable versions, audit, deterministic test provider, dispatch health   | Opportunity and Brief lifecycle works with no paid model dependency                        | Current             |
| B. Inventory truth         | Repeatable ingestion, review, assets, large catalogue, benchmark-ready OOH/DOOH facts and detail pages      | Unseen supplier file reaches publishable inventory through review                          | Current / near-term |
| C. Opportunity to proposal | Evidence, interpretation, Strategy, Critic, Brief, Media Mix, eligibility, comparative inventory intelligence, supply/forecast, three-tier proposal and PDF | One real opportunity completes end to end with approvals, evidence reuse and recorded cost | Near-term           |
| D. Supplier marketplace    | Self-service listings, availability, RFQs, quotes, booking and commercial settings                         | A supplier manages listings and completes a buyer request without internal data re-entry   | Next                |
| E. Campaign delivery       | Creative workflow, proof, delivery, performance and client reporting                                       | Booked campaign closes the loop to verified outcomes                                       | Next                |
| F. Advanced intelligence   | Licensed audience data, predictive models, activation integrations and controlled optimisation             | Capability is evidence-defensible and client-authorised                                    | Future              |

**Release principle:** Each phase must ship a usable vertical journey with human approvals, observability, recovery and production-safe data boundaries. A feature is not complete because an agent produced text.

# 13. Success metrics

| **Category** | **Metric**                                                 | **Definition**                           | **Owner**             | **Cadence**          |
|--------------|------------------------------------------------------------|------------------------------------------|-----------------------|----------------------|
| Workflow     | Brief-to-approved-proposal turnaround                      | Median and p90 by workflow type          | Product / Operations  | Weekly               |
| Quality      | Human acceptance and material edit rate by agent           | Accepted, edited, rejected, evidence gap | Agent owner           | Weekly               |
| Evidence     | Material claims with approved lineage                      | % of proposal/plan claims                | Governance            | Release + monthly    |
| Inventory    | Extraction precision/recall and unresolved review rate     | By supplier, channel and document type   | Inventory Ops         | Per import + monthly |
| Freshness    | Rates and availability within policy window                | % of active catalogue                    | Supplier Ops          | Daily                |
| Recovery     | Successful resume without duplicate action or paid call    | % of failed runs                         | Engineering           | Weekly               |
| Cost         | Incremental AI cost per opportunity, plan and proposal     | Model/provider and workflow stage        | Engineering / Finance | Per run + monthly    |
| Commercial   | Proposal-to-selection, booking conversion and gross margin | By tier, channel and client segment      | Commercial            | Monthly              |
| Supplier     | RFQ response time and self-service maintenance             | Median response; % supplier-maintained   | Supplier Ops          | Weekly               |
| Reliability  | API/job success, latency and incident recovery             | SLO and error budget                     | Engineering           | Continuous           |

*Targets remain proposed until baselines are measured. The owner must approve each definition, data source, threshold and reporting window before it becomes a release KPI.*

# 14. Risks and mitigations

| **Risk**                                             | **Mitigation**                                                                                           |
|------------------------------------------------------|----------------------------------------------------------------------------------------------------------|
| Unsupported AI claims reach clients                  | Evidence-linked schemas, critic gate, human approval and claim-level source visibility                   |
| Brief intent is silently changed                     | Immutable BriefVersions, explicit unknowns/assumptions and comparison before approval                    |
| Stale rate or availability enters a proposal         | Freshness policy, supplier confirmation and plan invalidation after material change                      |
| Paid calls repeat after failures                     | Checkpointed workflows, idempotency, artefact reuse and cost policy                                      |
| Agent/runtime health blocks business work            | Durable state outside runtime, visible recovery, deterministic fallback and manual continuation          |
| Extraction works only for known files                | Supplier-agnostic schemas, document classification, coordinate-aware evidence and unseen-file evaluation |
| Tenant data leaks                                    | API/database isolation, least privilege, negative tests and launch-blocking security review              |
| Users cannot understand or challenge recommendations | Plain-language rationale, evidence, confidence, rejection reasons and human override                     |
| Roadmap expands into Omni-scale scope                | Non-goals, vertical release gates and commercial evidence before advanced identity/activation            |
| Supplier self-reporting degrades trust               | Evidence requirements, verification tiers, quality scores, audits and dispute workflow                   |
| Model/provider changes regress quality               | Versioned prompts/schemas, evaluation corpus, deterministic contracts and controlled rollout             |
| UI repeats legacy complexity                         | New screen blueprint, one primary action, role testing and no internal wording                           |

# 15. Locked build decisions and source notes

| **Decision**              | **Locked default**                                                                                                                                                                                                                     | **Accountable owner**      |
|---------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------|
| Agent roster              | Implement only the eleven specialists named in Section 6. Record each as absent, scaffolded, implemented or verified after repository inspection. A new agent requires an ADR and owner approval.                                      | Engineering                |
| First production vertical | Build the full-campaign opportunity-to-proposal journey first. Rapid OOH is the second vertical and reuses the same evidence, Brief, plan, approval and proposal primitives.                                                           | Product + Commercial       |
| Proposal options          | A full campaign presents up to three genuinely different client routes, not superficial price copies. Platform packages Launch, Boost, Scale and Dominance remain separate master data and must not be confused with proposal options. | Commercial                 |
| Inventory launch          | All channels share one canonical inventory model. Complete deep supplier self-service and benchmark coverage for OOH and Radio first; other channels remain ingestible and reviewable.                                                 | Commercial + Inventory Ops |
| Supplier verification     | Self-reported data is labelled. Prices, availability, ownership and audience claims require dated evidence; high-value or disputed claims require independent or Advertified review.                                                   | Inventory Ops + Legal      |
| Crawling and POPIA        | Crawl only permitted public or user-supplied sources, identify the crawler, respect exclusions and rate limits, retain source evidence under policy, and block production until Legal approves the register.                           | Legal / Privacy            |
| Payments                  | The canonical lifecycle records payment intent, referral/credit route and booking terms. Providers are adapters; Advertified does not hold client funds unless a separately approved regulated model is implemented.                   | Finance + Commercial       |
| Release SLOs              | Production target: 99.5% monthly availability; p95 standard API reads under 2 seconds; RPO 15 minutes; RTO 4 hours; named incident owner and tested restore before launch.                                                             | Engineering                |

## 15.1 Source notes

| **Source**                    | **Used for**                                                                                                                         | **Location**                           | **Status**                  |
|-------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------|-----------------------------|
| Omnicom Omni                  | Public description of end-to-end workflow, agentic AI, identity backbone, interoperability, production and predictive intelligence   | https://www.omc.com/omni/              | Accessed 28 Aug 2026        |
| Advertified product direction | Clean redevelopment directives, canonical Brief requirements, inventory workflow, agent run behaviour and authenticated UX decisions | Internal working context               | Current through 28 Aug 2026 |
| Implementation status         | Repository tests, migrations, routes, runtime health and provider configuration                                                      | Must be verified from the clean branch | At each sprint/release gate |

