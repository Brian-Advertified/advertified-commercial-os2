# ADVERTIFIED — CANONICAL BUSINESS, PRODUCT, GOVERNANCE & PRODUCTION SPECIFICATION

**Status:** Single normative source of truth  
**Owner:** Advertified  
**Canonical file:** `ADVERTIFIED.md`  
**Effective:** 2 September 2026  

> **Authority rule:** This file is the single normative source for what Advertified is, the business/product it provides, commercial rules, governance, workflows, agentic-AI behaviour, UX direction, architecture boundaries and production acceptance. `AGENTS.md` is the only companion Markdown file and governs contributor/AI working behaviour only; it cannot redefine Advertified product truth.

---

# 0. How This Specification Is Governed

## 0.1 Source-of-truth order

When sources conflict, use this order:

1. The repository owner's latest explicit instruction.
2. This `ADVERTIFIED.md` specification.
3. An explicit owner-approved change recorded into this canonical specification/change history.
4. Executable contracts, migrations and tests as evidence of implementation status — never as authority to silently change product intent.
5. Legacy Advertified applications/prototypes and Git history as read-only historical evidence.

`AGENTS.md` governs how contributors work in the repository, not what the Advertified business/product is.

A missing fact remains unknown. A failed check remains failed. Existing code does not become correct merely because it exists.

## 0.2 Normative rules versus non-normative hypotheses

Advertified separates production truth from business validation:

- **[Principle]** — normative. A trust, commercial or product commitment. Fixed unless the owner deliberately changes it.
- **[Policy]** — normative. Governed, versioned and owned; expected to evolve as evidence, law, commercial conditions or scale change.
- **[Hypothesis]** — **non-normative**. An unproven business or product claim to be tested in real use. A hypothesis is not an implementation requirement, does not define system correctness, and does not by itself block or permit production release.

**Advertified must remain correct, secure and commercially truthful even if every current hypothesis proves false.**

A hypothesis never becomes a Principle or Policy merely because time passes, because it has been repeated often, or because someone edits its bracket tag. Promotion requires the resolution process in Section 32 and retained supporting evidence.

## 0.3 Documentation rule

This document is intentionally a single file even though it is long. The repository's source-code line limits do not require this specification to be split. The purpose of this file is to eliminate competing product truths.

`ADVERTIFIED.md` and `AGENTS.md` are the only Markdown documents intentionally retained in the repository. Machine-readable evidence, manifests, test artefacts, code, configuration and Git history may exist elsewhere, but none of them may silently redefine this specification.

---

# 1. What Advertified Is [Principle]

Advertified is an **evidence-governed commercial operating system for advertising**.

It converts fragmented advertiser demand and fragmented media supply into structured, explainable and executable commercial decisions. It preserves competing commercial states instead of flattening them, uses specialised Agentic AI for interpretation and preparation, escalates material uncertainty through governed rules rather than model confidence, and records the provenance, decisions, approvals, transactions and outcomes that accumulate into commercial intelligence over time.

## 1.1 One-line definition

> **Advertified is an evidence-governed commercial operating system for advertising. It starts with OOH/DOOH as its proving wedge, turns fragmented demand and supply into executable campaigns, retains commercial history rather than flattening it, uses specialised AI agents to remove manual work, and keeps commercial judgement, provenance and incentives visible rather than hiding them behind automation.**

## 1.2 Product promise

Advertified's job is to turn a business opportunity or supplied campaign requirement into an evidence-backed strategy, audience definition, media plan, commercial proposal and executable campaign using real, traceable supply while keeping commercial truth, money, approvals and external consequences under governed control.

## 1.3 Positioning

Advertified is the **commercial intelligence and transaction layer connecting advertiser demand with fragmented media supply** — particularly where buying remains manual, relationship-driven, spreadsheet-heavy, email-heavy or poorly digitised.

Advertified is not:

- a general advertising chatbot;
- a lowest-price marketplace;
- a replacement for DSPs, ad servers, Google, Meta or other mature execution platforms;
- an AI system that presents inference as fact;
- an opaque broker that hides commercial margin;
- a supplier ranking engine where payment can buy suitability;
- a separate OOH product and separate full-campaign product;
- a system in which AI owns canonical commercial state.

## 1.4 Whose side Advertified is on

Advertified is not exclusively on the buyer's side or the supplier's side.

> **Advertified is on the side of the transaction being accurate, executable and commercially transparent.**

The buyer should receive suitable media and defensible pricing. The supplier should retain commercial flexibility and receive qualified demand. Advertified should be paid for making the workflow and transaction more valuable, not because one participant knows less than the other.

## 1.5 The market problem Advertified solves [Principle]

Advertising buying is fragmented across agencies, brands, media owners, sales houses, creators, spreadsheets, email threads, WhatsApp, rate cards, PDFs, presentations, supplier portals and platform-specific buying systems. The fragmentation is especially severe in OOH/DOOH, radio, print, experiential, retail media, local media and other supply that is still sold through people and documents rather than clean APIs.

The recurring problems are:

- a client brief arrives incomplete or ambiguous;
- audience, geography and business context have to be reconstructed manually;
- planners search many disconnected suppliers and rate cards;
- inventory naming and specifications are inconsistent;
- prices are quoted on different bases and change over time;
- availability is often not known until a supplier is contacted;
- negotiated prices, prior quotes and commercial history disappear into inboxes and spreadsheets;
- proposals are assembled manually from multiple systems;
- approvals, bookings, creative, proof and measurement are disconnected from the original brief;
- the reasoning behind a media recommendation is difficult to audit later;
- suppliers repeatedly answer poorly shaped enquiries and maintain duplicate catalogues for different buyers;
- smaller advertisers may not have internal media-planning expertise at all.

Advertified exists to turn this fragmented commercial process into one governed, evidence-backed operating flow.

## 1.6 What Advertified does end to end [Principle]

Advertified supports the full commercial advertising journey, not only planning and not only media search.

```text
Business problem / opportunity / supplied brief
→ preserve source evidence
→ understand the business and requirement
→ research legitimate missing context
→ identify knowns, claims, assumptions, conflicts and unknowns
→ create the canonical Brief
→ segmentation, targeting and positioning (STP)
→ determine channel roles and budget allocation
→ discover and evaluate real media inventory
→ compare prices, commercial conditions and evidence
→ confirm supply where required
→ build an executable media plan
→ create client proposal options and branded documents
→ human approval, self-approval, independent approval or bounded policy authorisation
→ client decision
→ funding / payment / purchase-order readiness
→ supplier RFQs, quotes and bookings
→ creative requirements, concepts and production readiness
→ campaign launch and delivery
→ proof of performance
→ measurement and reporting
→ preserve learning and commercial history for the next decision
```

Every major artifact remains connected to the evidence and exact version that produced it.

## 1.7 The four primary ways work enters Advertified [Principle]

Advertified is not dependent on one intake method.

### 1.7.1 Supplied campaign brief

An advertiser, agency or Advertified planner pastes, types, uploads or forwards the real campaign requirement. Advertified interprets it, researches where legitimate, exposes material uncertainty and creates the canonical Brief before planning.

### 1.7.2 OOH/DOOH-only brief

The same campaign lifecycle is used, but the immutable campaign mode restricts planning and supply to OOH/DOOH. It is not a separate Rapid OOH product or parallel planning engine.

### 1.7.3 Inbound OOH straight-through proposal request

A configured OOH mailbox can receive a complete enquiry and process the same Brief → STP → OOH planning → supply → proposal → PDF flow automatically where bounded policy and deterministic readiness permit it. Unclear or unsafe cases stop for review rather than inventing missing facts.

### 1.7.4 Evidence-led commercial opportunity

Advertified may start without a client-supplied brief where a genuine business opportunity has been identified. Approved public/client evidence is interpreted into an opportunity and strategy, then into a reviewed draft Brief. The Opportunity path never bypasses the Brief stage.

## 1.8 Who uses Advertified and what each participant gets [Principle]

### Brands and advertisers

Advertisers use Advertified to turn a business or marketing problem into an understandable campaign without needing to manually navigate every supplier and media format.

They can:

- supply a brief or business problem;
- review what Advertified understood;
- see evidence, assumptions and unknowns;
- review audiences and strategy;
- understand why each media channel has a role;
- compare proposal options and trade-offs;
- approve or request changes;
- use approved funding/payment routes;
- track campaign readiness and delivery;
- see proof and measurement linked back to the original objective.

### Agencies

Agencies use Advertified as a planning, commercial-intelligence and execution workbench across advertiser accounts.

They can:

- manage multiple advertiser workspaces;
- ingest existing supplier relationships and rate cards;
- use evidence-backed research and specialist AI assistance;
- prepare STP, media mixes and plans;
- compare inventory and commercial benchmarks;
- produce client proposals and branded documents;
- manage approvals, supplier coordination and bookings;
- maintain institutional pricing and campaign history;
- track proof, performance and reporting.

Advertified augments agency expertise; it does not assume the agency needs to surrender its supplier relationships or commercial judgement.

### Media owners and suppliers

Media owners use Advertified to turn their fragmented inventory information into structured, searchable commercial supply and to receive better-shaped demand.

They can:

- onboard their organisation;
- upload rate cards, site lists, media kits and availability files;
- maintain inventory, rates, images, specifications and terms;
- respond to RFQs and booking requests;
- update availability and quote-specific commercial terms;
- participate in bookings and delivery workflows;
- provide proof where required;
- retain control over their own commercial information and flexibility.

### Creators, influencers and specialist partners

Creators and representatives can maintain relevant profiles/rates, receive suitable requests, understand campaign role and deliverables, manage rights/requirements and provide delivery evidence.

### Advertified internal teams

Advertified planners, inventory operations and platform administrators use the system to manage exceptions, evidence, planning, supplier quality, commercial settings, agent operations, audit and production governance.

## 1.9 The product modules in business terms [Principle]

Advertified is one connected product with the following major capabilities.

### Opportunity Intelligence

Find and develop credible advertising/growth opportunities from approved evidence before a formal brief exists.

### Brief Intelligence

Ingest the original brief, extract explicit statements, research legitimate gaps, distinguish evidence from assumption and produce a complete versioned campaign intent.

### Research, Strategy and STP

Understand the business, market context, buying occasions, audiences, segmentation, targeting, positioning, message direction, objectives and constraints without presenting unsupported inference as fact.

### Media Planning

Decide which channels deserve a role, allocate budget, set independent running periods, explain channel contribution and allow authorised human edits.

### Inventory Intelligence

Search, filter, map, benchmark and evaluate real media supply across the governed inventory catalogue, retaining why items qualified or were rejected.

### Inventory Ingestion and Commercial Normalisation

Convert messy supplier PDFs, spreadsheets, emails, images, rate cards and feeds into structured, evidence-backed products, rates, availability, assets, terms and commercial history while preserving the original source.

### Supplier Marketplace and Supply Coordination

Allow media owners to maintain inventory and respond to demand; allow planners to request quotes, confirm availability and coordinate supply without treating recommendations as confirmed bookings.

### Commercial Pricing Intelligence

Retain list, quoted, negotiated and booked commercial states; compare compatible supply; expose freshness, terms and benchmark context; never flatten all pricing into one unsupported number.

### Proposal and Client Decision

Create materially different executable proposal options where useful, generate branded client documents, preserve exact totals and evidence, and record the client's selected option/decision.

### Funding and Commercial Readiness

Support purchase-order, invoice, payment and approved finance/referral routes without letting payment-provider state redefine canonical financial truth.

### Booking and Campaign Delivery

Turn selected commercial lines into governed supplier commitments, track creative requirements, readiness, campaign state and delivery milestones.

### Creative Intelligence

Use campaign/product context and supplied brand/catalogue material to generate concepts and format adaptations for human review. Creative AI assists; it does not publish, claim clearance or change booked media requirements.

### Proof and Measurement

Collect delivery evidence, performance facts and measurement inputs, then interpret outcomes within the actual limitations of the measurement design.

### Tasks, Approvals and Exceptions

Surface only real decisions and exceptions requiring people, with self-approval and independent approval correctly distinguished and bounded automation used where policy already authorises the action class.

### Commercial Administration

Manage roles, tenant access, fee policy, VAT/commercial settings, integrations, provider policy, materiality/freshness rules, audit and agent operations.

## 1.10 How Agentic AI fits into the actual product [Principle]

Agentic AI is not a separate chatbot bolted onto Advertified. It is embedded across the commercial journey through specialist roles that operate on the same canonical evidence and business state.

The agents help Advertified:

- interpret businesses and briefs;
- research and synthesise context;
- develop strategy and STP;
- reason about channel roles;
- interpret inventory purpose and suitability;
- prepare media plans;
- challenge unsupported assumptions through a critic/readiness function;
- explain proposals in client language;
- develop creative concepts;
- interpret measurement evidence.

They use approved tools for inventory search, geography, benchmarking, retrieval and deterministic calculations. They do not own prices, bookings, budgets, permissions, approvals or canonical records.

The user experience should therefore feel like an expert commercial team working inside one system, not like a person repeatedly prompting eleven separate bots.

## 1.11 The media network and inventory layer [Principle]

Advertified builds a connected media supply layer across OOH/DOOH, radio, television, print, digital/social, influencer, experiential, podcast/audio, retail, transit, airport, mall, email/mobile and future governed channels.

The system must support both:

- **Advertified-connected supply** — suppliers who actively maintain or respond through the platform; and
- **organisation-owned supply knowledge** — an agency or advertiser's own supplier relationships, rate cards and commercial history.

This means Advertified creates value before an open marketplace reaches full liquidity.

The inventory layer stores much more than a sellable product name. It connects product identity, location, technical specification, rates, availability, evidence, terms, supplier responses, negotiation history, bookings, proof and measured outcomes.

## 1.12 Current campaign investment bands [Policy]

Advertified currently has configurable campaign investment bands in master data. They are planning/commercial entry bands, not substitutes for actual campaign strategy and not proposal-option names.

Current ZAR baseline:

- **Launch:** R10,000 to below R100,000
- **Boost:** R100,000 to below R500,000
- **Scale:** R500,000 to below R1,000,000
- **Dominance:** R1,000,000 and above, with no fixed upper limit

These bands may help frame service level, campaign ambition or public packaging, but the actual Brief, media plan and proposal must remain evidence- and objective-driven.

A campaign may also present up to three materially different proposal routes where useful. Those proposal routes are not the same thing as the Launch/Boost/Scale/Dominance investment bands.

## 1.13 Funding and payment proposition [Policy]

Advertified can support campaigns that are paid directly as well as approved funding/facilitation routes such as **Advertise Now, Pay Later**.

The platform's role is to coordinate the commercial workflow and record the authoritative state. Specific finance/payment providers are adapters and may change.

Advertified must never imply that funding is approved, money is received or a supplier is paid merely because an AI, user interface or external provider request says so; verified reconciliation controls canonical financial state.

## 1.14 What makes Advertified different [Principle]

Advertified is not differentiated because an LLM can produce a media-plan paragraph.

Its intended differentiation is the combination of:

- one canonical commercial lifecycle from requirement to learning;
- specialist Agentic AI embedded inside that lifecycle;
- evidence and provenance at material decision level;
- structured inventory reconstructed from messy real-world supply;
- cross-channel planning around the campaign job rather than one media owner's ecosystem;
- commercial-state history instead of one overwritten price;
- explainable inventory selection and rejection;
- supplier/agency/advertiser workflows in one governed system;
- human authority over consequential actions;
- self-approval without artificial bureaucracy, while preserving audit;
- straight-through automation where policy and evidence truly make it safe;
- campaign delivery, proof and measurement connected back to the original Brief.

## 1.15 What a complete Advertified campaign means [Principle]

A campaign is not considered complete because an AI generated a strategy, because a PDF exists, or because a booking record was created.

A complete commercial journey preserves the lineage from the original requirement through the decisions and evidence that led to the final outcome, including where applicable:

- source brief/opportunity;
- approved Brief version;
- STP and planning rationale;
- inventory considered, selected and rejected;
- current commercial evidence;
- proposal and client decision;
- approval basis;
- funding/payment/PO state;
- supplier commitments;
- creative readiness;
- delivery proof;
- performance/measurement evidence;
- final learning.

---

# 2. Product Strategy and Wedge

## 2.1 OOH/DOOH-first execution [Policy]

Advertified is multi-channel by design, but **OOH/DOOH is the initial commercial wedge** because it exposes nearly every hard problem the platform is intended to solve:

- fragmented suppliers;
- PDFs, Excel files and emailed rate cards;
- geographic reasoning;
- routes and POIs;
- inconsistent product naming;
- opaque and negotiable pricing;
- availability confirmation;
- supplier negotiation;
- production and installation costs;
- specifications and imagery;
- proof of performance;
- many independent inventory owners.

The canonical OOH journey is:

```text
messy OOH requirement
→ structured Brief
→ STP
→ geography/routes/POIs
→ OOH/DOOH media mix
→ eligible inventory
→ commercial comparison
→ supplier confirmation
→ media plan
→ proposal
→ approval/policy authorisation
→ booking
→ delivery
→ proof
→ measurement
```

OOH/DOOH is the proving ground, **not the permanent limit of Advertified**.

## 2.2 Multi-channel architecture [Principle]

The canonical inventory and planning architecture supports:

- OOH / DOOH;
- Radio;
- Television;
- Print;
- Digital / Social;
- Influencer;
- Experiential;
- Podcasts / Audio;
- Retail media;
- Transit;
- Airports;
- Malls;
- Email;
- Mobile;
- future approved channels through governed extensions.

Channel capabilities do not need equal maturity on day one.

## 2.3 Digital platforms are a separate maturity track [Policy]

Google, Meta and similar platforms present a different integration problem from OOH.

OOH's main difficulty is fragmented, unstructured, human-mediated commercial supply.

Digital platforms' main difficulty is:

- provider APIs;
- authentication;
- campaign objects;
- changing schemas;
- reporting;
- attribution;
- provider-specific execution rules.

Advertified may initially include digital platforms in strategy, budget allocation, planned activity, imported performance and cross-channel measurement while relying on their existing execution infrastructure. Direct execution integrations may be added later where they create sufficient value.

## 2.4 Marketplace sequencing [Policy]

Advertified must create value before it depends on two-sided marketplace liquidity:

```text
Commercial OS first
→ connected supply network second
→ marketplace liquidity third
```

A single agency must be able to ingest its own supplier relationships and gain planning/proposal value without waiting for an open marketplace.

A single media owner must be able to structure inventory, receive enquiries and manage bookings without waiting for mass buyer adoption.

---

# 3. Users and Organisations

Advertified supports role-scoped organisations and workspaces.

## 3.1 Role families [Policy]

- **Advertified Platform Admin** — tenant/platform administration, privileged commercial controls, integrations, governance and audit.
- **Advertified Planner / Agent** — opportunities, briefs, strategy, planning, proposals and client collaboration within assigned scope.
- **Inventory Operations** — imports, extraction review, supplier quality, rates, availability and evidence.
- **Agency Admin** — agency users, advertiser workspaces and commercial oversight.
- **Agency Campaign User** — briefs, collaboration, plans and proposals for assigned advertisers.
- **Advertiser Admin** — own organisation, users, briefs and campaigns.
- **Advertiser Approver** — assigned commercial decisions where the organisation uses separated approval.
- **Supplier User** — own supplier catalogue, rates, availability, RFQs, bookings and assigned requests. This is the only active supplier-user role; it has no supplier administration hierarchy or unrelated tenant-management authority.
- **Influencer / Representative** — own or represented profiles, rate cards, requests and deliverables.
- **Service identities** — least-privilege runtime/worker identities; never interactive commercial approvers.

## 3.2 Tenant isolation [Principle]

Tenant isolation is enforced by the Commercial API and data layer, not by hiding navigation items.

Every protected read, mutation, tool call and job must be independently authorised against:

- authenticated identity;
- tenant/workspace;
- active membership;
- role/permission;
- assignment where required;
- resource state/version.

The browser and AI runtime are never trusted to supply authoritative tenant or role claims.

---

# 4. Campaign Entry Paths and Brief Intake

## 4.1 Two legitimate entry paths [Principle]

Advertified has two primary commercial entry paths:

### A. Supplied requirement / Brief

A user pastes, types, uploads or forwards the actual requirement.

### B. Evidence-led Opportunity

Advertified or a user identifies a potential commercial opportunity from approved evidence and develops it into strategy and a draft Brief.

An Opportunity never bypasses the Brief stage.

## 4.2 No mandatory client registration before a Brief [Principle]

A user must be able to start from the brief itself. Advertified must not force the user to complete administrative client-registration CRUD before supplying the requirement.

Client/organisation identity may be resolved, proposed or created during intake. Material ambiguity in client identity must be confirmed before consequential external action.

## 4.3 Briefs are not treated as "understood" [Principle]

A client's statement is not automatically a fact.

The intake pipeline is:

```text
Brief arrives
→ preserve immutable source
→ extract explicit statements
→ identify supplied evidence
→ research what may legitimately be researched
→ classify verified information / claims / inference / assumptions / unknowns
→ determine campaign mode when clear
→ escalate only materially consequential uncertainty
→ create canonical BriefVersion
```

Ambiguity is assumed to be normal, not exceptional.

## 4.4 Campaign-mode selection [Principle]

Advertified supports exactly one canonical campaign lifecycle with an immutable campaign mode:

- `OOH_ONLY`
- `FULL_CAMPAIGN`

The mode is resolved from evidence before planning. AI may interpret unclear language, but direct supplied media instructions are not AI opinions and must not be represented as confidence-based recommendations.

- A supplied media requirement restricted to OOH and/or DOOH is direct Brief evidence for `OOH_ONLY`.
- A supplied media requirement that includes any required non-OOH channel is direct Brief evidence for `FULL_CAMPAIGN`.
- Explicit full, integrated, multichannel or omnichannel wording establishes `FULL_CAMPAIGN`.
- Non-exclusive wording such as “preferred”, “consider”, “including”, “such as” or “open to” does not by itself prove an OOH-only restriction; Advertified must interpret the complete Brief and supporting research.
- Where the Brief does not specify media, Advertified may research and propose the appropriate mode from the business problem, audience, geography, timing and available media. If the evidence remains materially ambiguous, the user confirms the mode.
- A dedicated inbound OOH mailbox fixes the mode to `OOH_ONLY` for accepted requests.

For a direct supplied-media decision, the review UI shows the campaign type, exact source evidence and that no mode choice is required. It must not present a confidence percentage as the basis for the decision. Inferred decisions retain their evidence and rationale and expose clarification only when material ambiguity remains.

The mode is immutable once the CampaignBrief is established.

**An OOH-only campaign must never be expanded into a full campaign.** If non-OOH scope is subsequently required, start a new CampaignBrief from the original/current requirement and rebuild the planning lineage. Do not silently reuse the OOH-only plan as though it were a full-campaign plan.

## 4.5 CampaignBrief is a working aggregate [Principle]

A CampaignBrief is not five-field CRUD.

It contains immutable BriefVersions covering:

- tenant / client identity;
- source opportunity/tender/request;
- business problem;
- objective and desired outcome;
- audiences and hypotheses;
- geography, routes and POIs where relevant;
- languages and demographic/life-stage evidence where lawful and relevant;
- timing and flighting;
- typed money, currency and VAT status;
- fees/commission treatment where applicable;
- constraints;
- measurement and attribution limitations;
- knowns;
- claims;
- unknowns;
- assumptions;
- conflicts;
- evidence lineage;
- campaign mode;
- governance and approval records.

An approved BriefVersion is never silently edited. A material change creates a new version.

---

# 5. Evidence Model

## 5.1 Evidence is typed, not reduced to one confidence score [Principle]

Advertified separates three different concepts that must never be collapsed into one status.

### Evidence basis — how the information originated

- Client supplied
- Supplier supplied
- External research
- Historical transaction
- Derived
- AI inference

### Verification state — how trustworthy/current it is

- Verified
- Unverified
- Conflicting
- Stale
- Unknown

### Required action — what happens next

- None
- Review
- Confirm with client
- Confirm with supplier
- Human decision required

A fact may therefore be, for example:

- `Supplier supplied + Verified + None`
- `AI inference + Unverified + Review`
- `Client supplied + Conflicting + Human decision required`

## 5.2 Required provenance [Principle]

Every material commercial fact must make the following reconstructable where applicable:

- original source;
- document/API/email/provider;
- source locator (page/sheet/cell/region/URL/reference);
- captured/received date;
- who or what supplied it;
- transformation or derivation;
- verification decision;
- reviewer/actor;
- effective/freshness dates;
- exact version used downstream.

## 5.3 Confidence is diagnostic, not truth [Principle]

Model/extraction confidence may exist as diagnostic metadata for evaluation or review prioritisation.

It must never substitute for:

- evidence basis;
- verification;
- freshness;
- materiality policy;
- human/commercial authority.

Advertified's operating rule is **evidence, not confidence**.

## 5.4 Explainability at individual recommendation level [Principle]

Advertified must be able to answer questions such as:

> Why was this inventory recommended?

The answer must point to real reasoning and evidence such as geography match, route/POI fit, format suitability, rate source/date, availability status, benchmark basis, campaign objective and material assumptions.

The same applies to audience and strategy claims.

---

# 6. Materiality and Consequence Governance

## 6.1 Materiality is governed policy, not AI intuition [Principle]

The AI may propose that something appears material, but fixed materiality rules are versioned policy owned by Advertified.

Each rule records:

```text
rule
→ scope
→ reason
→ required action
→ effective date
→ owner
→ version
```

## 6.2 Always-material starting classes [Policy]

Unless a later governed policy explicitly narrows them, the following are materially consequential:

- campaign budget;
- currency;
- VAT treatment;
- client identity where it affects contracting or delivery;
- material timing/dates;
- geography where it changes buying;
- contractual terms;
- supplier price where it affects client price or commitment;
- inventory identity;
- availability;
- campaign commitment;
- funding/payment state;
- legal/regulatory restrictions;
- external recipient;
- supplier commitment;
- booking;
- invoice;
- publication or creative release.

## 6.3 Example materiality behaviours [Policy]

| Situation | Default treatment |
|---|---|
| Campaign budget changes | Explicit authorised human decision |
| VAT status changes | Explicit authorised human decision |
| Supplier price changes | Preserve old/new values and evidence; re-evaluate affected commercial output |
| Geography is inferred and changes buying | Confirm before consequential use |
| Audience hypothesis is added | May proceed as labelled hypothesis where policy allows |
| Typographical correction with no semantic impact | May resolve automatically |

## 6.4 Governance scales by exception aggregation [Policy]

At low volume, materiality policy can be owned by a named senior product/commercial owner with specialist advice where required.

At higher volume it evolves into a formal governance function with:

- change SLAs;
- versioned policies;
- impact analysis;
- delegated subject-matter ownership;
- review metrics.

Normal campaigns flow under approved rules. Governance should not become a manual approval department for every campaign.

---

# 7. Human Corrections Are Evidence, Not Automatic Truth

## 7.1 Correction provenance [Principle]

A correction must retain:

```text
original value
→ changed value
→ actor
→ reason
→ supporting evidence
→ timestamp
→ subsequent reversals if any
```

## 7.2 Correction classes [Policy]

- Evidence-supported correction
- Authoritative-user correction
- Unverified manual override
- Later-reversed correction

A human change is not automatically ground truth merely because a person made it.

## 7.3 Audit what was not escalated [Principle]

Advertified must measure downstream corrections even when the system originally allowed a fact to pass without review.

This makes it possible to answer:

- Which fields are most often corrected?
- Which source types create the most material errors?
- Which agent/model combinations cause the most material corrections?
- Which suppliers' documents most often conflict with later confirmed quotations?
- Which materiality rules need revision?

Audit data may propose policy change. It must not automatically rewrite governance rules.

---

# 8. Agentic AI Operating Model

## 8.1 Core separation rule [Principle]

Use AI only where interpretation, synthesis, judgement or creative reasoning is genuinely useful.

Use deterministic services for:

- authorisation;
- validation;
- calculations;
- money/VAT;
- lifecycle transitions;
- eligibility hard constraints;
- conflict checks;
- benchmark statistics;
- versioning;
- idempotency;
- document assembly from approved facts;
- notification mechanics;
- durable job control.

## 8.2 Commercial API is canonical truth [Principle]

The Commercial API owns:

- canonical business state;
- tenant enforcement;
- permissions;
- lifecycle transitions;
- approvals;
- money and commercial calculations;
- idempotency;
- audit;
- business commands.

AI agents do not connect directly to the commercial database and do not silently mutate canonical state.

## 8.3 AI outputs are proposals [Principle]

AI output is untrusted proposal data until validated against:

- typed schema;
- exact input versions;
- evidence requirements;
- materiality policy;
- authorised tools;
- tenant scope;
- budgets/cost policy;
- lifecycle rules.

AI must not invent:

- inventory;
- rates;
- availability;
- audience facts;
- supplier responses;
- client decisions;
- approvals;
- legal conclusions;
- delivery proof;
- performance evidence;
- completion evidence.

Use `unknown`, `unverified`, `conflicting`, `stale` or a review state instead.

## 8.4 Closed specialist roster [Policy]

Advertified uses specialist agents rather than one general chatbot.

The initial production roster is:

1. **Opportunity Intelligence Agent** — identifies credible evidence-backed commercial opportunity angles.
2. **Business Interpretation Agent** — interprets what the business sells, to whom, buying occasions, geography and unknowns.
3. **Strategy Agent** — develops evidence-backed growth/communications strategy.
4. **Brief Drafting Agent** — turns approved evidence/strategy into a complete draft BriefVersion without inventing unknowns.
5. **Audience Agent** — produces segmentation, targeting and positioning reasoning and AudienceDefinitions.
6. **Inventory Intelligence Agent** — interprets product purpose and recommends eligible verified supply using deterministic tools.
7. **Media Planning Agent** — develops channel roles, allocations, flighting and MediaPlan drafts.
8. **Critic & Readiness Agent** — identifies unsupported, contradictory, weak or unsafe claims and readiness gaps.
9. **Proposal Narrative Agent** — explains approved commercial structures in client-ready language without changing facts/totals.
10. **Creative Agent** — develops concepts/adaptations and format ideas from approved campaign context; cannot publish or claim clearance.
11. **Measurement Agent** — interprets verified delivery/performance evidence without overstating causality.

Do not create a new agent merely to perform deterministic orchestration, calculation, extraction, rendering, state transition or notification work.

## 8.5 Agent contract [Principle]

Each run must bind:

- tenant;
- actor/service identity;
- agent code;
- exact input versions;
- approved evidence IDs;
- policy version;
- allowed tools;
- maximum tool calls/steps;
- timeout;
- provider/model policy;
- cost cap;
- correlation ID;
- checkpoint/reuse information.

Each output must provide, as applicable:

- typed artifact;
- evidence bindings;
- explicit unknowns;
- explicit assumptions;
- objections;
- concise rationale;
- safe next action;
- provider/model usage and cost metadata.

Private chain-of-thought is not canonical business evidence and must not be stored or exposed as audit rationale.

## 8.6 Model/provider policy [Policy]

Advertified is provider-neutral at the product level.

Model selection must optimise for **the lowest cost that meets the required quality and safety for that agent/task**. More expensive models are used only where evaluation proves they materially improve the required outcome.

Provider/model configuration is governed and versioned, not hard-coded as product truth.

Development and certification should default to deterministic/fake providers unless an explicitly authorised evaluation requires a live provider. Production live-provider use must be:

- allow-listed;
- cost-capped;
- usage-recorded;
- recoverable;
- protected against duplicate paid calls;
- evaluated before rollout.

## 8.7 Durable AI work [Principle]

Long-running agent workflows must survive worker/runtime restarts.

Approved artifacts and checkpoints are durable outside model/provider memory.

On resume:

- reuse validated work;
- reuse approved evidence;
- do not repeat a paid provider call merely because a worker restarted;
- never infer business completion from runtime health alone.

---

# 9. Canonical Commercial Lifecycle

## 9.1 One commercial spine [Principle]

```text
Opportunity or supplied Brief
→ Evidence / interpretation
→ CampaignBrief
→ STP
→ Media Mix
→ Inventory eligibility & intelligence
→ Supply / forecast
→ Media Plan
→ Proposal
→ Client decision
→ Funding / commercial readiness
→ Booking
→ Creative readiness
→ Live delivery
→ Proof
→ Measurement
→ Learning
```

Opportunity discovery may add Strategy before Brief creation. A supplied brief need not be forced through an Opportunity workflow.

## 9.2 STP is mandatory [Principle]

Segmentation, Targeting and Positioning is a canonical planning stage for both `FULL_CAMPAIGN` and `OOH_ONLY`.

OOH STP is not optional. It incorporates:

- geography;
- routes;
- POIs;
- movement context;
- buying occasions;
- audience needs;
- audience exclusions;
- positioning;
- audience promise;
- reasons to believe;
- message direction.

## 9.3 OOH-only versus full campaign [Principle]

There are not two different planning products.

The difference is the allowed channel set:

- `OOH_ONLY` — OOH/DOOH only.
- `FULL_CAMPAIGN` — the governed multi-channel registry.

Everything else uses the same canonical evidence, Brief, STP, planning, proposal, approval, audit and commercial foundations.

## 9.4 User-editable planning [Principle]

AI recommendations are editable within governed rules.

Users must be able to:

- change allocations;
- change selected/rejected inventory;
- change permitted media choices;
- adjust running periods;
- use different running periods by media/channel;
- see why inventory was selected or rejected;
- see assumptions and stale supply;
- understand the consequence of edits before approval.

A change that is materially consequential invalidates/requires re-evaluation of affected downstream artifacts.

---

# 10. Inbound OOH Straight-Through Proposal Automation

## 10.1 Purpose [Policy]

A tenant may configure a dedicated inbound mailbox for OOH proposal requests.

The original email and permitted attachments are preserved as immutable evidence.

The same canonical OOH-only flow is used; no shadow Rapid OOH domain is allowed.

One inbound email may contain more than one explicitly separated campaign Brief. Each campaign intent requires its own CampaignBrief lineage while retaining a reference to the same immutable source email and relevant attachments. Automation must never blend those intents into one Brief; until deterministic splitting and evidence allocation are available, the email remains in `REVIEW_REQUIRED` for authorised separation.

## 10.2 Ready path [Policy]

Where the request is complete and passes the tenant's pre-authorised automation/readiness policy, Advertified may process without per-request user input:

```text
receive email
→ validate mailbox/sender/recipient policy
→ preserve source
→ interpret Brief
→ set OOH_ONLY
→ create STP
→ generate OOH/DOOH mix
→ find eligible inventory
→ reconcile rates/availability
→ create plan
→ create proposal
→ render branded PDF
→ deliver exactly once
→ record audit and AI cost
```

This is not the AI "approving itself". The commercial consequence is permitted by a human-owned, explicit, bounded automation policy that is independently enforced by the platform.

The user-facing Rapid OOH path is this same immutable `OOH_ONLY` lifecycle and never a second
aggregate, namespace or endpoint family. AI resolves `OOH_ONLY` versus `FULL_CAMPAIGN` from the
complete Brief evidence; only genuinely material ambiguity requires clarification. Client
registration is not a prerequisite to intake. `OOH_ONLY` can never convert into
`FULL_CAMPAIGN`; expanded scope starts a new CampaignBrief.

`OOH_ONLY` may interpret the Brief, apply deterministic eligibility, select inventory, approve
the bounded internal planning/proposal artefacts required by the human-owned automation policy,
render and send when the client-facing total including VAT and configured fees is at most
ZAR 500,000, the final total is within budget, mandatory fields are clear, every selected
inventory record is approved/published, and geography, dates, format, rate, currency, VAT,
validity and source evidence pass. Default planning availability passes unless an overlapping
not-available period, blackout, confirmed booking conflict or inactive record exists.

`FULL_CAMPAIGN` always requires human review before a media plan or proposal is sent. Human
review also remains mandatory for OOH_ONLY above the cap; missing or contradictory price, VAT,
date or geography; unresolved geometry; negotiation/discounting; public-sector tender or
regulated/sensitive campaign; inability to remain within budget; and every binding booking,
purchase-order or contractual commitment. AI has no authority to book, issue a purchase order,
spend, accept supplier terms, grant a discount or change canonical supplier facts.

## 10.3 Review-required path [Principle]

The system must not send where material readiness is missing, including:

- unclear recipient identity;
- unclear/missing budget where required;
- unclear dates;
- unclear geography;
- non-OOH scope;
- unsupported format;
- conflicting/stale rates;
- insufficient inventory;
- unresolved material conflict;
- invalid sender/recipient policy;
- terminal render/delivery failure.

Human involvement is for unclear/material exceptions, not a mandatory extra step on every valid automated request.

## 10.4 Exactly-once behaviour [Principle]

Duplicate provider message IDs or canonical content hashes must not create duplicate briefs, proposals or sends.

A retry resumes from the last safe checkpoint.

---

# 11. Inventory, Evidence and the Commercial Graph

## 11.1 Inventory is commercial truth, not just catalogue rows [Principle]

The commercial graph includes:

```text
supplier
→ media brand/network
→ product
→ format
→ location/geography
→ audience relevance
→ rate
→ rate validity
→ availability
→ quote history
→ negotiated history
→ booking history
→ response behaviour
→ proof of delivery
→ campaign use
→ measured outcomes
→ commercial terms
```

## 11.2 The moat is operational/relational history [Hypothesis]

The schema itself is not a moat.

Potential defensibility comes from the accumulated private commercial history that cannot simply be scraped from public rate cards:

- supplier response patterns;
- negotiated discounts;
- actual bookings;
- availability behaviour;
- proof quality;
- campaign outcomes;
- repeated buyer/supplier commercial history.

This is a hypothesis to be tested, not a guaranteed moat.

## 11.3 Inventory extraction objective [Principle]

Advertified must ingest the messy reality of media supply:

- PDFs and scanned/structured rate cards;
- XLSX workbooks;
- CSV files;
- DOCX/media kits;
- images and embedded assets;
- email bodies and attachments;
- supplier APIs/exports;
- manual entry where necessary.

The objective is **not to extract rows or text**. The objective is to establish:

> **What is being sold, by whom, where it is, what its commercially relevant specifications are, what it costs, on what buying basis, under what conditions it can be bought, how current the information is, and exactly where each material fact came from.**

Canonical stages are:

```text
Acquire source
→ Protect / malware and file controls
→ Classify document and channel
→ Render / preserve layout structure
→ Detect known structure and changes
→ Extract candidate facts and assets
→ Normalize structure without changing commercial meaning
→ Bind material fields to evidence
→ Validate deterministically
→ Reconcile identity and prior commercial history
→ classify differences as resolved / comparable / conflict
→ review material exceptions
→ publish versioned commercial truth
→ monitor freshness and later changes
```

## 11.4 Preserve source and layout before interpretation [Principle]

Every import preserves the original source before extraction. Record, as applicable:

- tenant and supplier context;
- source type;
- original filename/message/API reference;
- file/content hash;
- received/captured date;
- object version;
- document/page/sheet structure;
- import/extraction version.

The source is never discarded merely because extraction succeeded.

Advertified must preserve relationships a human can see in the source, including:

- page regions;
- sheet names;
- table boundaries;
- headings and subheadings;
- merged cells;
- row groups;
- column hierarchy;
- footnotes;
- cell/range coordinates;
- images/logos and their source regions.

Layout is evidence. A price under a heading such as `Johannesburg Digital Large Format` cannot be detached from that heading and treated as an unidentified generic rate.

## 11.5 Classify before extracting commercial truth [Principle]

Advertified identifies what kind of material it has received and what commercial purpose it serves before selecting an extraction strategy.

Document classes may include:

- rate card;
- inventory/site/product list;
- availability update;
- media kit;
- package catalogue;
- technical specification;
- audience/measurement report;
- terms and conditions;
- contact/sales information;
- image catalogue;
- mixed commercial workbook.

Classification may also propose:

- supplier/media owner;
- media brand/network;
- channel;
- expected structural pattern;
- likely fields/assets;
- extraction route.

Classification does not itself verify any commercial claim.

## 11.6 Candidate commercial facts [Principle]

Extraction creates **candidates**, not canonical truth.

Candidate fields may include:

### Supplier and ownership

Supplier, media owner, media brand, sales house, network, representative and contact information.

### Product identity

Supplier product/site code, product name, channel, product type, format, package and description.

### Geography

Country, province/region, municipality, locality/suburb, venue, address, road, route, POI, latitude/longitude and coverage area.

### Technical attributes

Dimensions, structure type, static/digital state, illumination, screen specification, loop duration, slot duration, plays/share, spot length, placement type, creative/material specification and other channel extensions.

### Commercial information

Published/list rate, rate basis, currency, VAT treatment, commission, explicit discounts, minimum order, production cost, installation cost, package conditions, inclusions and exclusions.

### Timing

Rate validity, availability period, booking deadline, production/material deadline, lead time and cancellation windows.

### Audience and measurement

Traffic, footfall, circulation, listenership, reach, impressions or other supplied measurement only together with source, period, methodology and limitations.

Where supplied, an inventory audience profile keeps spoken and understood languages
distinct, retains weighted segment shares, records age/life-stage bands and preserves
LSM/SEM or equivalent segments with the named taxonomy and taxonomy version. The
measurement universe is retained separately from the source, period, methodology and
limitations. Sparse coverage is normal: a missing component remains insufficient evidence
and is never converted into a neutral or average value. Editorial positioning copy remains
a supplier claim or inference and is not stored as measured audience fact.

### Terms

Cancellation, production, installation, creative, payment and other commercial conditions.

### Assets

Supplier/media-brand logos, inventory photographs, diagrams, specifications, rate cards and related supporting files.

No field becomes trusted because an extraction model returned it successfully.

## 11.7 Raw value and normalized value are both retained [Principle]

Advertified keeps what the supplier actually supplied and how Advertified interpreted/normalized it.

Example:

```text
Raw value: "R45 000 pm excl VAT"
Normalized amount: 4500000 minor units
Currency: ZAR
Rate basis: MONTHLY
VAT basis: EXCLUSIVE
Source: supplier-rate-card.pdf / page 7 / table 2 / row 14
Transformation: governed money/rate parser vX
```

Normalization may standardize terminology, data types and mathematically equivalent units.

It must never silently change commercial meaning.

Equivalent supplier labels may map into one governed product/rate taxonomy where the mapping is defensible. Materially different products or commercial structures must remain different merely even if merging them would simplify the catalogue.

## 11.8 Inventory-specific materiality [Policy]

Not every extracted word deserves the same evidence/review cost. Inventory extraction extends the global materiality model with three working classes.

### Tier 1 — commercially consequential

These are always evidence-bound and cannot silently rely on AI inference where used commercially:

- supplier/media-owner identity;
- sellable product identity;
- price/amount;
- currency;
- VAT basis;
- rate/buying basis;
- rate validity;
- availability status/period when represented as current;
- geography/location where it affects buying;
- minimum order/volume conditions;
- production/installation cost where it affects totals;
- material inclusions/exclusions;
- cancellation/payment terms where represented to the buyer;
- booking/material deadlines;
- audience/performance claims used in recommendation or proposal;
- ownership/rights claims where relevant to publication or booking.

### Tier 2 — planning consequential

These require evidence when they affect eligibility, planning, production or comparison, including:

- dimensions;
- static/digital state;
- illumination;
- loop/slot/play-share characteristics;
- placement type;
- route/road/venue context;
- technical/creative specifications;
- material delivery requirements.

### Tier 3 — descriptive/contextual

Examples include supplier marketing copy, descriptive narrative and non-decision-critical labels. Preserve the source, but do not spend the same verification or reviewer effort on every descriptive adjective.

The inventory materiality register is governed Policy and may evolve from correction/audit evidence. AI does not decide which class a field belongs to by itself.

## 11.9 Field-level evidence model [Principle]

Material extracted facts reuse the same Advertified evidence vocabulary as briefs and strategy.

Each material field can carry:

- **Evidence basis** — supplier supplied, external research, historical transaction, derived, AI inference, etc.;
- **Verification state** — verified, unverified, conflicting, stale or unknown;
- **Required action** — none, review, confirm with supplier, human decision, etc.;
- exact source locator;
- captured/effective/freshness date;
- extraction method and transformation/normalization lineage.

Extraction-model confidence may prioritize review internally. It is not evidence and does not turn an unverified commercial field into truth.

## 11.10 Deterministic validation [Principle]

Once candidates exist, deterministic controls validate what does not require model judgement, including:

- required field presence;
- money and currency;
- VAT structure;
- rate/buying units;
- date formats and validity windows;
- coordinates/geographic formats;
- duplicate identifiers;
- impossible or malformed values;
- channel/schema compatibility;
- overlapping/conflicting rate periods;
- required evidence bindings;
- governed product/rate codes.

Safe deterministic normalization may be applied and recorded.

If meaning would have to be guessed, the value remains unresolved rather than being silently repaired.

## 11.11 Identity reconciliation and asymmetric merge safety [Principle]

An extracted row does not automatically create a new product, and a similar row does not automatically update an existing one.

Advertified attempts to establish whether a candidate is:

- the same existing product with unchanged information;
- the same product with a new rate;
- the same product with changed attributes;
- a new availability version;
- a new commercial version;
- a duplicate representation;
- genuinely new inventory.

Identity may use supplier product identifiers, governed identity attributes, geography, format/specification and evidence-backed context. AI may assist with ambiguous matching but cannot merge canonical records directly.

**False-merge risk is asymmetric.** Combining two genuinely different products corrupts price, availability and evidence history and is more damaging than temporarily retaining two separate candidates.

> **When evidence is insufficient to prove two inventory candidates are the same commercial product, Advertified does not merge them. Preserve/separate first; reconcile later.**

## 11.12 Reconcile changes using the three-outcome model [Principle]

New supplier evidence is compared with existing canonical commercial history. Old values are not overwritten out of history.

Every material difference follows the same three-outcome model defined in Section 12:

1. **Resolved automatically** — the records are provably answering the same commercial question and an explicit precedence rule applies.
2. **Comparable** — the records describe different but usefully comparable commercial conditions. Advertified may normalize mathematically valid dimensions while retaining every original term.
3. **Conflict requiring resolution** — the information is materially incompatible or precedence cannot be proven; both remain visible until reconciled or explicitly accepted.

Example: an old four-week list rate and a new twelve-week bundled rate may be mathematically comparable on some dimensions, but the new commercial structure must not be treated as a simple replacement unless the governed precedence/comparability rules prove that interpretation.

## 11.13 Human review is exception-led [Principle]

Human review must not become manual re-entry of every extracted row.

Review is created for material exceptions such as:

- unclear product identity;
- uncertain supplier/ownership identity;
- missing/ambiguous price or rate basis;
- uncertain VAT treatment;
- incompatible or conflicting terms;
- questionable geography;
- unclear validity dates;
- unsupported audience/performance claims;
- possible duplicate/merge ambiguity;
- unidentified material assets;
- extraction ambiguity that changes commercial meaning.

The reviewer sees the candidate beside the exact source evidence.

Corrections retain original value, changed value, actor, reason, supporting evidence and time. A human correction is evidence, not automatic ground truth.

## 11.14 Adaptive fidelity and extraction cost [Principle/Policy]

Inventory extraction is expected to be a high-volume workload. Advertified must not apply maximum-cost AI interpretation uniformly to every file, row or unchanged field.

> **Extraction fidelity and AI cost scale with novelty, ambiguity, materiality and change — not merely document size.**

The pipeline should determine, before expensive interpretation where practical:

```text
source received
→ known supplier/source/document class?
→ structural fingerprint changed?
→ which pages/sheets/regions/rows changed?
→ which materiality tiers are affected?
→ deterministic extraction/mapping sufficient?
→ AI interpretation actually required?
→ human review actually required?
```

Examples:

- A first-time supplier's irregular 70-page rate card may require full rendering, layout reconstruction, richer extraction and stronger review.
- A known supplier's monthly availability workbook with unchanged schema should prefer deterministic mapping, structural validation and targeted review.
- If only a small subset of rows/fields changed, Advertified should avoid re-interpreting thousands of unchanged commercial fields without a quality reason.

Model/provider policy follows Section 8.6: use the lowest-cost method that meets the quality and safety requirement. Escalate to a stronger model/tool only when the cheaper deterministic or lower-cost path cannot establish the required result.

Cost optimization must never silently lower evidence requirements for Tier 1 commercial facts.

## 11.15 Declarative supplier mappings versus forbidden special-casing [Principle]

Advertified may maintain governed declarative mappings that explain how a source expresses the generic commercial schema.

Allowed example:

```text
Supplier/source mapping:
"Site No."       → supplierProductCode
"4 Weekly Rate"  → publishedRate
"Lat"            → latitude
"Long"           → longitude
```

This is acceptable when the mapping:

- only translates source vocabulary/structure into the canonical schema;
- still uses the same validation, evidence, materiality, identity and publication rules;
- is versioned and reviewable;
- does not create hidden supplier-specific commercial behaviour.

Forbidden example:

```text
if supplier == "Supplier A":
    skip_standard_validation()
    use_special_price_logic()
    create_supplier_specific_product_state()
```

The governing test is:

> **Does this mapping only explain the supplier's source structure to the generic pipeline, or does it create different business behaviour for that supplier?**

The former may be governed configuration. The latter requires a genuine generic capability/policy decision and must not be smuggled in as configuration.

## 11.16 Publish commercially usable truth [Principle]

Approved candidates become versioned canonical inventory and commercial history.

Publication may create or update:

```text
Supplier
→ media brand/network
→ inventory product/version
→ geography
→ technical attributes
→ rate versions
→ availability versions
→ assets
→ terms
→ measurement evidence
→ provenance
```

A product is planning-available by default. Absence of a supplier availability response or
recent confirmation is not an unavailable state and does not block matching or proposal
generation. Only an overlapping explicit `UNAVAILABLE`/not-available period, blackout,
confirmed booking conflict or deactivated product makes it unavailable for the requested
dates. Advertified does not create a blocking unknown-availability state. Booking confirmation
remains a distinct consequential process and is never inferred from this planning default.

The following states must remain distinct:

> **extracted ≠ verified ≠ currently available ≠ booked**

A successful extraction does not prove supplier confirmation, and planning-available
inventory is not a booking.

## 11.17 Freshness after extraction [Principle]

Ingestion does not end at publication.

Advertified tracks:

- rate validity and confirmation date;
- availability freshness;
- superseding source documents;
- supplier changes;
- changed product specifications;
- expiring terms;
- later conflicts;
- changed evidence quality.

A previously verified fact may become `STALE` without being historically false.

Planning/proposal/booking workflows use governed freshness policy to determine whether re-confirmation or re-planning is required.

Availability freshness is informative rather than a negative availability inference. A stale
or absent supplier response never becomes an unavailable period. Rate and other commercial
freshness rules remain independently binding.

## 11.18 Assets [Principle]

Where available and rights permit, inventory ingestion should extract and retain:

- supplier logos;
- station/publication/network logos;
- OOH photographs;
- relevant product images;
- specification assets;
- terms documents.

Missing assets remain visibly missing/unverified; placeholders must not be presented as verified content.

Possession or extraction of a rate card, image or logo does not establish usage rights. Rights
may be approved only by an authorised Supplier Admin/Owner or an Advertified Admin with
documented supplier permission. An ordinary uploader or agency may submit evidence but may
not self-approve unless written supplier authority is attached.

Rights are recorded separately for internal planning, named-client proposals, Advertified
marketplace display, and public marketing/social use. The record binds the exact asset,
territory, effective date, expiry or `UNTIL_REVOKED`, attestor and immutable written evidence.
The default territory is South Africa; any other territory requires explicit permission.
Public marketing/social use always requires its own explicit scope.

Expired or revoked rights remove the asset from public listings and new proposal versions and
create a revalidation task. Historical proposal documents and evidence remain immutable.
Missing rights never block inventory selection: newly rendered material falls back to a
neutral text-only representation.

## 11.19 Large catalogue UX [Policy]

Advertified must remain usable beyond 10,000 inventory products using server-side:

- search;
- filters;
- grouping;
- counts;
- stable cursor pagination;
- hierarchy;
- virtualisation where needed.

The browser must not load the full catalogue.

## 11.20 Successful extraction definition [Principle]

An import is commercially successful only when Advertified can answer, for every material published product as applicable:

- What is it?
- Who owns or sells it?
- Where is it?
- What are its commercially relevant specifications?
- What does it cost and on what buying basis?
- What is included/excluded?
- For what period is the information valid?
- What do we know about current availability?
- What supporting assets exist?
- Which evidence supports each material commercial claim?
- What remains unknown, stale, conflicting or in need of confirmation?

If those questions cannot be answered, extraction may be technically complete but the affected inventory is not yet commercially ready.

## 11.21 Extraction release acceptance [Policy]

Evaluation uses an explicitly selected, versioned collection of legitimately supplied documents,
kept outside application source and public distribution. The collection used on 2026-09-03 had 43
documents; that is historical evaluation scope, not a required inventory count or system dependency.
Each evaluation manifest records its own membership, source hashes and provenance. A changed
collection requires a new evaluation version, not changes to application code. The evaluation
reserves an untouched 20% holdout where the sample permits; an insufficient sample cannot establish
generalisation. Independently human-authored versioned gold must bind to that manifest, never be
generated from the extraction being scored. PDFs, spreadsheets, presentations, scans and images are measured
separately; an insufficiently represented format remains human-review-
only rather than blocking the entire OOH_ONLY release.

For review-ready documents, release acceptance requires at least 99% critical-field
precision, 95% critical-field recall, zero unsupported critical fields, no more than 0.5%
overall unsupported fields, at least 98% correct table row/column association, 99.5% exact
numeric/currency/date accuracy, 97% reconstructed table-cell accuracy, 0.90 overall OCR
confidence and 0.95 OCR confidence for critical numeric fields. Critical fields are supplier,
inventory identity, media type, location, format/specification, price, currency, VAT basis,
validity dates and explicit availability exceptions.

Every accepted extracted field has a page, cell, region or other exact source evidence
pointer. A corrupt, password-protected, handwritten, unreadable or structurally ambiguous
document, conflicting price/date, unclear currency/VAT treatment, formula without a usable
calculated value, or result below an applicable threshold routes to human handling. Initial
extraction creates candidates subject to the evidence-based acceptance policy in Section 11.23;
clean candidates may be automatically accepted, while exceptions remain pending. Evaluation metrics
are not per-candidate numeric schema-confidence thresholds. Neither evaluation success nor candidate
acceptance publishes or changes canonical inventory without the existing human publication decision.

## 11.22 Durable external extraction orchestration [Policy]

Every external document-extraction submission is represented by a durable, tenant-scoped attempt
before a provider is contacted. The attempt binds the immutable source version/hash, request and
correlation identifiers, provider/version, stable submission key, provider task identifier,
timestamps, polling checkpoint, attempt number, worker lease, provider outcome, failure
classification, accepted extraction artefact and reconciliation notes. Its governed progression is
`PENDING -> SUBMITTING -> RUNNING -> COMPLETED`; exceptional states are `FAILED_RETRYABLE`,
`FAILED_TERMINAL`, `TIMED_OUT`, `RECONCILIATION_REQUIRED` and `CANCELLED`.

A lost or timed-out submission response is ambiguous and never proves that the provider rejected
the work. `SUBMITTING` work without a durably recoverable provider task identifier moves to
`RECONCILIATION_REQUIRED` and is never blindly resubmitted. Workers reclaim expired leases only,
poll the already-recorded task identifier and resume from the last durable checkpoint. Tasks that
exceed 3,600 seconds stop ordinary polling and remain operator-visible. Retry, cancellation and
reconciliation require an authorised human command with a retained reason; a new attempt remains
bound to the same source version and hash, and an older or duplicate result cannot replace the one
accepted canonical extraction artefact.

The pinned Docling Serve `1.30.0` API exposes task submission, status polling and result retrieval,
but no documented idempotent submission/client-request-key lookup or task-cancellation operation.
Advertified therefore guarantees exactly-once canonical extraction acceptance and downstream
effects. It does not claim exactly-once Docling compute execution when acceptance of a submission
cannot be proven.

## 11.23 Admin-originated Day 0 inventory, supplier claiming and replacement [Policy]

Day 0 production inventory intake is an explicit administrator action, not a continuously running
inventory-discovery process.

### Replaceable source documents, not application dependencies

The owner's 2026-09-05 dataset-independence correction establishes that current supplier inventory
files are external, replaceable inputs. An operator may upload one file, any selected collection or
an entirely different collection without an application code or deployment change. The system must
also operate with no inventory loaded. Supported-format and evidence requirements still apply;
unreadable or unsupported inputs fail explicitly rather than receiving invented content.

Production extraction, interpretation, validation, acceptance, publication and startup must not
require particular filenames, supplier rosters, source hashes, folder paths, document counts,
certification markers or privately retained evaluation artefacts. Filenames may identify sources or
assist supported-format dispatch, but never supply missing commercial facts. Source hashes bind
versions, integrity and valid reuse; they must not select known-document answers or bypass checks.

Use the existing upload pipeline for every source. Do not create an offline parallel transcriber,
row-by-row semantic processor, canonical-product assembler or generated-workbook reimport route to
make current supplier files pass. Original source bytes and raw reader evidence remain authoritative.
No Python tool may repair commercial values, accept candidates or substitute a local certificate for
the C# acceptance and human publication decisions. Governed reference data and authorised supplier
ownership remain distinct from evidence extracted from documents.

Specific supplier examples may exist as isolated test inputs or versioned evaluation evidence, never
as implementation rules. Automated regression checks must not require the owner's private inventory
directory or today's dataset. Useful checks include a single upload, renamed inputs, arbitrary
unfamiliar names and changed input collections. A fixture pass is not proof of physical extraction
correctness or generalisation. Dataset certification reports describe only the exact evaluated
sources and revisions; they are not authoritative application-production readiness reports.

Physical/visual comparison against original files remains required for the separately authorised
current certification exercise before paid Bedrock evaluation. It is not a permanent manual gate
for future uploads and must not make one document depend on unrelated inventory. Historical
evaluation and usage evidence is retained as history, not executable product policy.

```text
authorised administrator selects a source file
→ upload, protect and classify
→ create a durable extraction attempt and wake the processor
→ extract, validate and resolve material exceptions
→ resolve one permanent supplier identity
→ publish and atomically cut over that supplier's inventory release
```

Advertified does not poll folders, mailboxes or supplier systems for new inventory and does not
repeatedly scan or reprocess stored documents while idle. Inventory processing is command/event
triggered and remains dormant when no extraction attempt exists. An already-submitted provider task
may be checked only while that durable attempt is active, and startup/recovery processing may finish
an interrupted attempt. An empty inventory queue must not cause rapid database polling, Docling
work, Bedrock calls or repeated extraction.

An upload by an authorised `platform_admin` or `inventory_ops` user is an authorised inventory
source. Supplier registration, credentials, login or supplier confirmation are not prerequisites
for that inventory to be extracted, approved, published and used in planning. This source authority
does not waive malware protection, extraction correctness, evidence, duplicate, material-field or
publication guards. The administrator remains accountable for resolving material extraction and
supplier-identity ambiguity.

The pipeline extracts supplier identity and available contact evidence from the source. Under the
owner's subsequent generic-ingestion direction, authenticated or explicitly selected supplier
ownership is fixed independently of document content. For an unresolved administrator import,
extraction proposes identity evidence; an explicit administrator resolution/creation command then:

- links the import to the existing permanent Supplier ID when identity is established;
- creates one `UNCLAIMED` administrator-managed Supplier record when no existing supplier exists and
  the identity is sufficiently established;
- keeps an ambiguous possible match as a blocking administrator decision rather than creating a
  duplicate supplier from a similar text name; and
- binds every published product, rate, asset, term and later invitation to the permanent Supplier
  ID, never merely to the displayed supplier name.

An unclaimed supplier may own active published inventory. The administrator may later issue a
single-use, expiring and revocable registration invitation bound to that exact Supplier ID and the
`supplier_user` role. Accepting the invitation creates or links the user's authenticated identity
and supplier membership to the existing Supplier record. The supplier then sees the inventory
already loaded on its behalf, and all later supplier uploads remain attached to the same Supplier
ID. Claiming a supplier does not recreate, transfer or rewrite its inventory or audit history.
Invitation issue, resend, revocation and acceptance are audited.

### Evidence-based inventory acceptance — policy `inventory-acceptance/1.0`

The owner's 2026-09-05 correction supersedes the earlier request for mandatory manual schema
approval of every document. Continue the existing pipeline: upload → Docling/applicable source
readers → source-evidence and extraction-completeness checks → Bedrock interpretation of each
distinct structure → deterministic application to all source records → versioned validation →
automatic acceptance of passing candidates and review of exceptions → separate human publication.
Clean documents and rows require no schema-approval task. Model confidence is diagnostic only.

Every applicable mandatory check must positively pass. Each recorded check is passed, failed,
not evaluated or not applicable; not applicable requires an explicit policy condition and reason.
Missing, skipped or unavailable checks keep acceptance pending. Policy 1.0 checks source identity
and revision, evidence-backed commercial mappings, record boundaries across distinct structures,
application beyond representative rows, accounting for source content/exclusions/extraction gaps,
existing required commercial fields and relationships, values/units/dates/governed classifications,
material ambiguity, and preservation of raw evidence and permitted transformations. Existing
business applicability rules remain authoritative; absent optional information is not a blanket
blocker. Defaults require explicit existing policy and policy-derived provenance.

Readers retain actual content, locations and raw values. Interpretation proposes reusable bindings,
record boundaries and contextual commercial meaning, including headings, notes, footnotes, units
and periods. It may not invent missing extraction content, rewrite every row, assign ownership,
replace inventory, accept candidates or publish. Supplier-, filename-, template- and known-corpus
shortcuts are prohibited. Do not add an overlapping enrichment pass after schema interpretation.

Isolated row problems hold the affected candidates; shared mapping problems hold their dependants;
document-wide or unisolatable problems hold the document. Reuse the existing source-versus-
interpretation review workflow for exceptions, showing mappings, affected candidates, evidence and
specific reasons. Human corrections create traceable interpretation revisions and require fresh
validation; a review decision may not bypass missing or failed acceptance evidence.

The existing reprojection command also supports retained-evidence reevaluation and traceable
mapping corrections at import scope, including imports with no candidate rows or no pending rows.
It requires the current import version and exact retained interpretation revision, an independent
authorised reviewer and a recorded reason. The same review screen exposes retained source structure,
raw locations, field mappings and access-controlled download of the clean protected original.
Reevaluation performs no extraction or paid interpretation. Historical rejections and deletion
tombstones remain effective across mapping revisions, even when a rejected source record was
temporarily omitted. Missing reviewer assignment must not discard extraction artifacts or silently
accept unresolved candidates; evidence remains pending until an eligible reviewer is available.
Publication revalidates server-owned retained artifacts, rejects absent schema evidence or changed
canonical values, and records the fresh evaluation in the existing immutable publication audit.
Editable candidate-extension metadata is not authority for acceptance or publication.

Retain source version/hash, extraction and mapping revisions, model/prompt provenance, policy
version, check results, reasons, outcome and timestamp. Preserve historical decisions. Compatible
unchanged retries may reuse retained artifacts; policy changes require reevaluation, not another
paid interpretation when retained evidence suffices. Outages/interruption remain recoverable and
pending. Source authority, acceptance and human publication remain distinct. This correction does
not alter supplier ownership/onboarding, replacement, expiry or proposal-impact rules.

Current certification still requires physical comparison of the current extractions with their
source files before separately authorised paid Bedrock evaluation, then inspection of actual
requests and responses file by file. Certification is not a permanent manual schema-approval gate.
The correction itself authorises no paid run, corpus re-import, deployment or container change.

### Supplier inventory replacement and atomic cutover

An administrator-originated inventory document is a `FULL_REPLACEMENT` release for that supplier by
default. A partial/delta interpretation may exist only as a separately approved explicit import mode;
it is never inferred from a file.

The previous supplier inventory changes only after the new import has completed extraction,
required review and successful publication. The publication transaction must atomically:

1. make the new supplier inventory release current;
2. mark every previously current product/version from that supplier `EXPIRED` and `SUPERSEDED`,
   unavailable for new planning and linked to the replacing release;
3. mark unresolved or pending earlier imports, candidates and draft/uncommitted listings from that
   supplier `CANCELLED` or `SUPERSEDED` as applicable and soft-delete them from ordinary operational
   views; and
4. retain every original source, extraction artefact, review decision, product/rate version,
   proposal binding, audit event and supersession link permanently.

Nothing in this process is physically deleted. Replacement is scoped by the permanent Supplier ID.
A failed, cancelled, ambiguous or publish-blocked new import does not expire or hide the supplier's
currently usable inventory.

### Effect on proposals and committed history

Every proposal that references superseded supplier inventory and has not crossed the confirmed
booking/committed-supply boundary becomes `INVENTORY_REVIEW_REQUIRED`. This includes draft,
in-review, approved, sent and selected-but-unbooked proposal versions. The proposal remains readable,
but approval, sending, client selection and booking conversion are blocked until the impact is
resolved.

Each affected proposal and line must show:

- that the supplier published replacement inventory;
- the affected old product, rate and availability versions;
- the old and replacement inventory-release references;
- whether an evidence-backed equivalent exists in the new release;
- material rate, specification, geography, availability or term differences; and
- whether the line must be replaced, removed, re-priced or explicitly re-confirmed.

Advertified never silently substitutes a proposal line. It may suggest a source-linked equivalent,
but an authorised human accepts the replacement and creates a new proposal/planning version. A
client viewing an already-sent affected proposal sees a clear inventory-updated warning and cannot
accept the stale option.

Confirmed bookings, legally committed supplier inventory, issued financial records, live/completed
campaigns and historical proposal documents retain the exact commercial snapshot used at the time.
They are not rewritten by a later supplier inventory upload.

---

# 12. Pricing, Commercial States and Benchmarking

## 12.1 Never collapse commercial prices into one value [Principle]

Price lifecycle and comparison intelligence are separate.

### Transaction price lifecycle

```text
Published/List
→ Quoted
→ Negotiated
→ Accepted/Booked
```

### Comparison intelligence

- Benchmark range
- Historical booked prices
- Comparable supplier rates
- Dated historical quotes where relevant

A benchmark is not a stage in the transaction lifecycle.

## 12.2 Differences are intelligence [Principle]

A supplier quoting below list rate may reveal negotiation flexibility. A historical booked rate may reveal achievable commercial precedent.

Do not erase differences merely to produce one neat number.

## 12.3 Conflict resolution — three outcomes [Principle]

Advertified does not silently pick a winning value.

Automatic resolution is allowed only when an explicit rule can prove that records answer the same commercial question, considering as applicable:

- same inventory;
- same supplier;
- same currency;
- same VAT basis;
- same rate/buying unit;
- same/compatible duration;
- same/compatible campaign period;
- compatible production/inclusion basis;
- compatible minimum order/volume conditions;
- compatible commercial terms.

Every potential conflict resolves to one of three states:

1. **Resolved automatically** — proven comparable and explicit precedence rule applies.
2. **Comparable** — Advertified can calculate helpful normalized mathematics but cannot claim the commercial offers are equivalent.
3. **Conflict requiring resolution** — materially incompatible information stays visible until reconciled or explicitly accepted.

## 12.4 Normalize mathematics, not commercial meaning [Principle]

Normalize only dimensions whose mathematical equivalence is defined.

Examples:

- four-week versus twelve-week rates may support weekly display where commercial basis is otherwise comparable;
- ex-VAT versus VAT-inclusive may be normalized where tax treatment is known;
- bundled production/media may only be decomposed if component evidence exists.

Do **not** manufacture equivalence.

One site versus a five-site bundle is not necessarily `bundle price ÷ 5` for commercial decision purposes.

When appropriate, Advertified should explicitly say **Not directly comparable**.

## 12.5 OOH market comparison [Policy]

OOH/DOOH benchmarking is deterministic first.

Comparable cohorts should consider:

- channel and digital/static state;
- format/structure class;
- spatial proximity;
- dimensions/display area where known;
- rate basis;
- currency/VAT;
- effective period;
- loop/share characteristics for DOOH;
- measurement compatibility;
- freshness.

The UI should expose:

- peer count;
- actual geography/radius/fallback used;
- included and excluded peers;
- median/quartiles/min/max where statistically defensible;
- target percentage above/below median;
- freshness;
- cohort limitations;
- deterministic benchmark policy/version.

AI may explain calculated facts. It may not choose hidden peers or alter benchmark arithmetic.

---

# 13. Audience, Strategy and Planning

## 13.1 Evidence-backed audience reasoning [Principle]

Advertified may reason about:

- product and price context;
- buying occasion;
- geography;
- language;
- age/life-stage where supported;
- lawful aggregate demographic evidence;
- LSM/SEM or equivalent market segmentation where supported;
- budget/timing;
- channel contribution;
- movement/location context.

It must distinguish:

- confirmed evidence;
- supplied claims;
- commercially reasoned inference;
- planning assumption;
- unknown.

Sensitive attributes must never be inferred for an individual.

## 13.2 Strategy [Principle]

Strategy must derive from approved evidence and clearly identify:

- business problem;
- commercial opportunity;
- objective;
- target audiences;
- proposition/positioning;
- channel implications;
- assumptions;
- risks/objections;
- measurement limitations.

The Critic/Readiness function challenges unsupported certainty and material contradictions.

## 13.3 Media mix [Principle]

Media Mix explains:

- why each channel exists in the plan;
- channel role;
- budget allocation;
- running period;
- interaction with other media;
- major assumptions.

No channel is included merely because it is familiar or because a prior meeting happened at a particular media brand/event.

## 13.4 Inventory matching for planning [Principle/Policy]

The approved AudienceDefinition set and approved MediaMix are exact inputs to inventory
matching. Hard eligibility is deterministic and precedes suitability interpretation. It
includes permitted channel/product type, required geography or verified spatial coverage,
campaign dates and availability, rate validity/currency/budget, and any required delivery
characteristics that are actually represented in canonical data.

Suitability remains visible by component. Language, life-stage and LSM/SEM fit are not
collapsed into an unexplained average. A component has one of three truthful outcomes:
evidence-backed fit, evidence-backed non-fit, or insufficient/incompatible evidence. An
audience component is scored only when the inventory profile retains measurement source,
period and methodology. LSM/SEM comparison additionally requires the target and inventory
to carry the same normalized taxonomy name and exact taxonomy version; otherwise the
component remains unscored with an explicit evidence gap.

Initial production uses exact LSM/SEM taxonomy key and version matching only. Original source
labels and versions are retained; the same label under a different version is not an exact
match. AI/semantic similarity may not translate taxonomies. Missing or unmatched taxonomy is
`UNKNOWN`, not non-fit, and blocks selection only when the Brief explicitly marks that audience
requirement mandatory. No version mappings are active. A later mapping requires an
effective-dated, human-approved table with evidence and audit history.

Missing audience evidence does not silently make a hard-eligible product ineligible for a
human-reviewed shortlist, but it blocks straight-through selection that depends on that
component. Every shortlist binds the exact audience set, ProductVersion, RateVersion,
AvailabilityVersion, marketplace snapshot where applicable, evidence metadata and matching
input hash. A Proposal inherits the approved plan's exact inventory and never searches or
re-resolves inventory itself.

The governed `INVENTORY_SUITABILITY_OOH_V1` policy weights the visible normalized components:
geographic/route/POI fit 30%, audience/context fit 25%, objective/format fit 15%, budget
efficiency 15%, evidence quality/freshness 10%, and portfolio coverage/diversity contribution
5%. Availability is binary and is not scored. Sponsored placement never changes suitability.
Tie-breaking is deterministic: more complete critical evidence, fresher valid rate, better
total target coverage, lower client-facing cost for materially equivalent suitability, then
stable inventory ID.

Every Brief spatial requirement is classified `REQUIRED`, `PREFERRED` or `EXCLUDED` and uses
one of four canonical forms: point plus explicit radius; versioned authoritative administrative
boundary; supplied catchment polygon; or route plus buffer. A route without a supplied buffer
retains a visible/editable inferred 500-metre default. `EXCLUDED` overrides all other spatial
classifications. Point distance uses `ST_DWithin` in metres, administrative and catchment
coverage uses `ST_Covers`, and route distance uses `ST_DWithin`. Polygon intersection is only
candidate discovery; required polygon eligibility needs at least 50% of the target area covered
unless the Brief records another threshold. Geometry is stored in EPSG:4326 and distance/area
calculation uses metre-safe PostGIS geography operations.

Invalid, ambiguous or unverified required geometry creates a human clarification task and may
not fall back silently to text matching. A selected shortlist must collectively cover every
required target. When that is impossible the governed result is `DO_NOT_BUY` with insufficient
suitable inventory, never relaxed eligibility.

PostGIS owns authoritative spatial intersection, containment, distance and route/catchment
operations when the required geometry is verified. pgvector may improve semantic recall for
descriptive brief-to-inventory retrieval and possible-duplicate discovery. It never decides
geography, identity merge, eligibility, price, availability or booking; a semantic duplicate
is a review candidate, never an automatic merge.

Inventory embeddings use Amazon Bedrock `amazon.titan-embed-text-v2:0`, 1,024 dimensions,
normalization enabled, on-demand invocation in `eu-west-1`. Canonical PostgreSQL, documents,
metadata and vectors remain in `af-south-1`. Only canonical normalized non-personal searchable
inventory text may be embedded; source documents, contacts, email addresses, confidential
Briefs, contracts, images and financial identifiers are excluded. Stored lineage includes
content hash, provider/model/version, dimensions, generated time and job. Similarity queries
never mix versions. Regeneration occurs only when searchable content/model/dimensions change or
an administrator explicitly requests a backfill.

Embedding spend is hard-capped at USD 3/month in staging and USD 10/month initially in
production, alerts at 80%, stops background work at 100%, and falls back to deterministic and
lexical search. Tests use a deterministic zero-cost provider. One staging smoke test/backfill
up to USD 3 is authorised after credentials are configured; production provider calls are not.

## 13.5 Media plan visual direction [Policy]

Planning screens should be simple to use but technically rich enough to support real planning.

Where useful they should include:

- editable allocation bars/charts;
- distinct visual identity per media/channel;
- channel/media logos where appropriate;
- independent running periods;
- maps where geography materially assists the decision;
- selected vs rejected inventory clearly distinguished;
- rejection reasons;
- totals and commercial reconciliation;
- responsive business-safe explanations.

---

# 14. Proposals and Client Decisions

## 14.1 Proposal truth [Principle]

A proposal is generated only from exact structured commercial inputs and evidence.

AI narrative may improve explanation but may not change:

- inventory;
- prices;
- fees;
- VAT;
- dates;
- quantities;
- terms;
- availability state;
- calculated totals.

## 14.2 Proposal options [Policy]

Advertified may present up to three genuinely different client routes where commercially useful.

Options must be materially different in outcome, scope, channel/placement mix, timing or budget — not superficial copies with different labels.

Platform package names such as Launch, Boost, Scale and Dominance are separate configurable commercial master data and must not be confused with proposal-option identities.

## 14.3 Branded documents [Principle]

Final proposal documents must be assembled from approved/current structured facts and retain:

- exact input version references;
- generation time;
- document version/hash;
- pricing basis;
- material assumptions/availability labels;
- approval/authorisation basis.

Generated client documents must be visually inspected as part of release/UAT evidence for relevant workflows.

---

# 15. Approval Model

## 15.1 Self-approval is allowed but audited [Principle]

An authorised human may directly approve their own artefact where their permission/policy allows it.

Self-approval is not inherently a vulnerability.

The system must record and display it as **Self-approval** (`SELF` or governed equivalent).

## 15.2 Independent approval [Principle]

Where a different authorised human approves, the system records and displays **Independent approval** (`INDEPENDENT` or governed equivalent) and identifies the approver.

## 15.3 Optional approval assignment for multi-user organisations [Principle]

A multi-user organisation may optionally assign an approval to a different eligible user.

Only when this assignment is deliberately selected does a pending approval state need to wait for another person.

## 15.4 No mandatory submit → wait → approve cycle for self-approval [Principle]

Advertified must not force self-approval into an artificial multi-step process solely to mimic separation of duties.

For an authorised self-approver, approval may be a single consequential action from the approvable state.

## 15.5 Approval audit data [Principle]

Every approval decision must make reconstructable:

- tenant/organisation;
- resource type;
- resource ID;
- exact version;
- creator/responsible user used for classification;
- approving human;
- approval mode (`SELF` / `INDEPENDENT`);
- outcome;
- timestamp;
- reason/notes where relevant;
- correlation ID;
- optional assignment record.

A materially changed version must not silently inherit an approval from an older version.

## 15.6 AI/service identities cannot approve their own output [Principle]

The self-approval policy applies to authorised human users.

An AI agent, agent runtime, worker or service identity cannot use human self-approval rules to approve its own generated commercial output.

## 15.7 Bounded automation [Policy]

Straight-through automation may perform an external action only where an explicit human-owned policy pre-authorises the exact class of action and deterministic readiness guards pass.

The audit must distinguish this policy-based authorisation from a human `SELF` or `INDEPENDENT` approval.

---

# 16. Supplier Marketplace and Supplier Value

## 16.1 Supplier participation [Hypothesis/Policy]

At launch, supplier participation should be free or near-frictionless because supply density and freshness are more valuable than early supplier SaaS revenue.

## 16.2 Core supplier foundation [Policy]

The intended permanent participation foundation includes:

- basic onboarding;
- inventory listing;
- inventory updates;
- rates and availability maintenance;
- receiving relevant enquiries;
- responding with rates/availability;
- participating in governed bookings.

This boundary should be made clear so suppliers are not baited into dependency on something marketed as permanently free and then unexpectedly paywalled.

## 16.3 Potential premium supplier capabilities [Hypothesis]

Future premium capabilities may include:

- advanced analytics;
- automated ingestion/integrations;
- team workflows;
- CRM capabilities;
- forecasting;
- enhanced distribution;
- advanced benchmarking intelligence;
- API access;
- premium operational automation.

Whether suppliers will pay for these is unproven.

## 16.4 Supplier north-star hypothesis [Hypothesis]

Potential supplier value should be measured primarily by **incremental booked revenue attributable to Advertified demand**, supported by:

- incremental enquiries;
- new buyers introduced;
- enquiry-to-quote conversion;
- quote-to-booking conversion;
- booked revenue;
- unsold inventory monetised;
- repeat bookings;
- response time;
- administrative interactions per booking.

## 16.5 Attribution classes [Policy/Hypothesis]

- New buyer via Advertified — strong incremental signal.
- Previously inactive buyer reactivated — probable incremental signal.
- Existing supplier/buyer relationship routed through Advertified — operational value, not automatically incremental revenue.

Attribution must not be overstated.

## 16.6 Attribution disputes [Principle]

Suppliers may dispute attribution, submit evidence and have the operational classification amended without deleting/re-writing the original transaction history.

At low volume, a named governance owner may decide ambiguous cases.

If attribution later materially affects fees, supplier status or financial incentives, final adjudication should be organisationally separated from teams directly rewarded by the classification outcome.

---

# 17. Monetization and Incentive Integrity

## 17.1 Revenue must not corrupt recommendation truth [Principle]

Advertified rejects monetization structures that create hidden recommendation conflicts.

### Not acceptable as default product behaviour

- Hidden media margin where supplier cost is obscured and the difference silently retained.
- Pure percentage-of-savings pricing that rewards inflated benchmarks.
- Pay-for-rank in suitability scoring.

## 17.2 Sequenced revenue model [Hypothesis/Policy]

Do not launch four separate monetization businesses simultaneously.

Current sequence to validate:

1. **First:** transparent advertiser/agency platform or campaign-management fee.
2. **Then:** explicit transaction/service fees where Advertified performs genuine transactional work.
3. **Later:** premium supplier operational tools once genuine supplier value is proven.
4. **Later still:** finance/funding referral/facilitation revenue, separated from media recommendation logic.

These are hypotheses until market willingness-to-pay is proven.

## 17.3 Buyer-side conflict guardrail [Principle]

Advertified must not manufacture complexity, savings claims or dependency merely to justify its own fee.

If Advertified claims it saved time, improved pricing, accelerated proposals or improved conversion, the claim should be supported by observable evidence and an explicit comparison basis.

Product design must not deliberately create unnecessary workflow friction to make the platform appear indispensable.

## 17.4 Sponsored visibility boundary [Principle]

> **Money can buy visibility. Money cannot buy an Advertified suitability score.**

Sponsored opportunities, if ever offered, must be visually and structurally separate from evidence/suitability-ranked recommendations.

If sponsored inventory independently qualifies for the normal recommendation set, it earns that position under the same rules as non-sponsored inventory.

Whether suppliers will pay meaningful amounts for separated sponsored visibility is an unvalidated hypothesis and not a core launch dependency.

---

# 18. Disintermediation and Retention

## 18.1 Direct relationships are expected [Principle]

Advertified assumes buyers and suppliers will exchange contact details and may communicate directly.

The durable strategy is not to trap either party contractually.

## 18.2 Retention anchor [Hypothesis]

Advertified aims to remain useful after the introduction through the continuing commercial workflow and institutional memory:

```text
Brief & approvals
→ multi-supplier planning
→ versioned prices
→ negotiated history
→ bookings
→ campaign documents
→ creative
→ delivery milestones
→ proof
→ invoices/funding records
→ measurement
→ client reporting
→ institutional commercial history
```

> **Advertified's long-term value is not owning the introduction between buyer and supplier; it is owning the trusted commercial workflow and institutional memory around the campaigns they execute together.**

This is a critical hypothesis, not a proven fact.

The first pilots must measure whether platform usage remains high after buyer and supplier already know each other.

---

# 19. Campaign Delivery, Proof and Measurement

## 19.1 Booking truth [Principle]

A recommendation is not a booking.

Provisional availability is not confirmed supply.

Only authorised, selected, current commercial lines may progress into booking.

A material substitution, price change, date change or inventory change creates a new version/decision and cannot be silently applied.

## 19.2 Creative [Principle]

Creative concepts may be generated by AI, but production/publication requires governed rights, format and approval controls.

The Creative Agent cannot:

- claim legal/brand clearance;
- publish autonomously;
- change booked format;
- invent approved brand assets.

## 19.3 Proof [Principle]

Delivery proof is evidence and must retain:

- campaign/booking link;
- source/object;
- capture time;
- location where relevant;
- reviewer status;
- limitations.

## 19.4 Measurement [Principle]

Advertified distinguishes delivery/performance facts from causal interpretation.

The Measurement Agent may interpret evidence, identify patterns and recommend learning, but cannot claim causality beyond the measurement design.

---

# 20. Funding, Payments and Commercial Finance

## 20.1 Provider adapters [Policy]

Payment/funding providers are adapters, not domain truth.

Advertified may support configured routes such as:

- once-off payment provider;
- manual EFT reconciliation;
- Advertise Now Pay Later / B2B funding referral or facilitation;
- future approved providers.

Specific vendors may change without redefining the lifecycle.

## 20.2 Financial truth [Principle]

Money is represented as ISO currency plus integer minor units.

Keep separately identifiable where relevant:

- supplier cost;
- discounts;
- commission;
- management/platform fees;
- production/installation;
- VAT;
- client total.

Provider status never silently becomes canonical paid/confirmed state without verified reconciliation.

---

# 21. Human-Facing Experience

## 21.1 Experience principle [Principle]

Users start with the commercial outcome and the next decision.

They provide a brief, not implementation parameters.

Every page should make clear:

- what Advertified understands;
- what is supported by evidence;
- what remains uncertain;
- current business status;
- responsible owner;
- one dominant next action;
- safe recovery when something fails.

## 21.2 Never expose internal engineering language [Principle]

Ordinary users should not see terms such as:

- canonical aggregate;
- browser boundary;
- schema validation;
- migration;
- dispatch;
- provider payload;
- runtime internals;
- orchestration implementation details.

Use commercial, human-understandable language.

## 21.3 Visual system [Policy]

The owner-approved authenticated screens are one Advertified design system, not independent page concepts.

The authenticated product uses one shared visual language:

- primary Advertified violet `#6038F5`;
- deep navigation navy `#071631`;
- white cards/surfaces;
- neutral application canvas `#F8F9FC`;
- primary text `#101828`, body `#344054`, muted `#667085`;
- success green only for semantic positive/automation state;
- consistent warning/danger/chart colours;
- one typography scale: 22 px page title, 16 px section title, 14 px card title, 13 px navigation/default UI, 12 px body/control, 11 px helper/meta;
- one spacing rhythm: 4 / 8 / 12 / 16 / 20 / 24 px;
- 38 px minimum standard controls;
- shared card border, radius and restrained shadow treatment.

**Dark green is not part of the Advertified interface theme.** Rapid OOH may use green as a semantic workflow accent, but it does not become a separate green-themed product.

A screen is not complete merely because its data is correct. If it visibly feels like a different application, that is a UX defect.

### 21.3.1 Navigation hierarchy [Principle]

Advertified separates three navigation concepts:

1. **Global product navigation** — one persistent sidebar and one global top bar shared by every authenticated screen. Their width, colours, typography, controls and behaviour do not change by module. Inventory, Rapid OOH, campaign work, finance and administration must never switch into a different application shell.
2. **Campaign/inventory process progress** — the horizontal Full Campaign, Rapid OOH and Inventory Intelligence rails inside the shared shell. These show process stage and are not duplicate global menus.
3. **Page-local navigation** — tabs/section rails that navigate inside the current record only.

A campaign process rail derives its campaign-mode presentation from the immutable mode on the current CampaignBrief lineage. A route name is not campaign-mode evidence, and an unresolved or unavailable mode must never default visually to `FULL_CAMPAIGN`. Downstream proposal, funding, booking, delivery, proof and measurement records retain that same mode context through their canonical Brief lineage.

The authenticated global sidebar is limited to:

- Home;
- Opportunities;
- Briefs;
- Inventory;
- Marketplace;
- OOH Inbox for authorised roles;
- Bookings;
- Campaigns;
- Tasks;
- Finance;
- Settings for authorised roles.

Campaign stages such as Strategy & STP, Planning, Proposal, Approval, Delivery, Measurement and Reporting are reached through the relevant campaign/work record and process rail. They are not duplicated as unrelated global menu destinations.

A visible global menu item must open its own real product area. It must never point to an unrelated screen as a placeholder. A process step that has no valid destination remains non-navigational rather than secretly routing elsewhere.

Use readable typography, charts/graphs where useful, consistent icons, subtle orientation/feedback animation, maps when geography matters, and clear spacing/hierarchy.

### 21.3.2 Agency command-centre experience [Principle]

Advertified must feel like an intelligent campaign command centre rather than an administrative form system.

The product should visibly demonstrate that work has been understood, analysed and advanced. Where truthful backend evidence exists, surfaces should expose useful moments of intelligence such as:

- likely audience groups and buying contexts;
- inferred campaign mode and why it was selected;
- geography, route, catchment and POI implications;
- recommended channel roles and media-mix reasoning;
- verified inventory counts and shortlist quality;
- budget balance, over-concentration and allocation trade-offs;
- commercial/rate benchmarks and materially better-value alternatives;
- measurement gaps, evidence limitations and unresolved decisions;
- supplier responses, availability changes and proposal consequences;
- creative territories, concepts or examples where the creative workflow supports them.

Intelligence should not be hidden behind backend execution. When the system has reached a useful conclusion, the user should be able to see the conclusion, its confidence/evidence basis and the next decision it enables.

The desired agency feeling is: **"I gave Advertified a commercial problem and, within minutes, I understand the audience, market, media options, risks and what I should do next."**

### 21.3.3 Visual momentum and delight [Policy]

Professional consistency is the baseline, not the complete experience. Advertified should create restrained moments of visual reward as users move work forward.

Use, where the underlying data supports them:

- responsive campaign summaries and decision cards;
- animated but non-distracting stage/progress feedback;
- maps that react to geography, route, POI and selected-site changes;
- visual media-allocation bars with stable per-channel colours and channel icons/logos;
- editable per-channel running-period/timeline bars;
- immediate budget-total and allocation feedback when users make planning changes;
- benchmark, reach, frequency, performance and comparison charts when the data is defensible;
- shortlist scoring and clear selected/rejected states;
- supplier/media-owner identity and approved logos where rights permit;
- creative previews and concept examples when generated or supplied assets exist;
- concise "what changed" feedback after AI or human actions;
- meaningful empty states that explain how to create value rather than showing dead space.

Motion must communicate state, causality, progress or orientation. Decorative animation that slows work, causes layout instability or misrepresents progress is prohibited.

### 21.3.4 Explainability as an interaction [Principle]

Important recommendations should support a concise **Why this?** interaction where practical.

Users should be able to understand why Advertified recommended or rejected an audience, geography, channel, media product, supplier, rate, allocation or measurement approach. Explanations must use retained evidence and governed reasoning; they must not fabricate certainty after the fact.

### 21.3.5 Dashboard as live work command centre [Policy]

The authenticated home/dashboard should answer **"What needs my attention now?"**, not merely display aggregate counts.

Where persisted truth exists it should surface actionable changes such as:

- approvals or corrections requiring attention;
- supplier responses or availability changes;
- proposals ready for review or client decision;
- campaign, proof, booking or measurement deadlines;
- meaningful campaign performance or delivery exceptions;
- work that became blocked or unblocked;
- material changes since the user's last interaction.

Counts may support this view, but counts alone are not the dashboard experience.

### 21.3.6 Map standard [Policy]

PostGIS remains the canonical geographic truth and eligibility engine. Mapbox is the standard authenticated frontend map-rendering and interaction layer unless this specification is explicitly changed.

All authenticated map surfaces must reuse the shared Advertified map component rather than introducing page-specific mapping stacks. The common map capability should support, as applicable:

- points and inventory/site markers;
- radius/catchment overlays;
- routes and governed route buffers;
- polygons and administrative/catchment boundaries;
- POIs;
- clustered dense result sets;
- selected/rejected/highlighted inventory states;
- fit-to-data behaviour and accessible map fallbacks.

Maps are decision surfaces, not decorative backgrounds. They must render canonical or explicitly draft geography and clearly distinguish unverified/draft geometry from verified commercial geography.

Avoid clutter, repeated banners, decorative stock-image dependency, internal technical copy, fake metrics/progress, per-screen themes, per-screen typography scales, arbitrary component sizing, or actions shown before the user has enough information to decide.

## 21.4 Validation and notifications [Policy]

Frontend forms and external data boundaries must use robust typed validation. Zod is the current browser validation standard where applicable.

User notifications should be centralised through the approved notification service/toast pattern rather than inconsistent one-off components.

Messages must be human-safe and explain the affected action and recovery.

## 21.5 Truthful progress [Principle]

Steppers, progress, metrics and activity feeds represent persisted backend truth only.

Never fabricate:

- completion percentages;
- provider calls;
- discovered inventory;
- supplier responses;
- publication;
- booking;
- measurement.

---

# 22. Core Product Surfaces [Policy]

The exact route structure may evolve, but these working surfaces must exist in the product architecture where applicable:

## 22.0 Public market-entry surfaces

Advertified's public experience explains the product and allows the market to enter the correct governed journey. It may include:

- public home / product explanation;
- How Advertified Works;
- media/channel solutions;
- published Media Network / media-owner discovery;
- campaign investment bands/packages;
- Advertise Now, Pay Later explanation;
- Start a Campaign / brief entry;
- agency registration;
- advertiser registration/onboarding;
- media-owner/supplier registration;
- creator/specialist registration;
- media-partner information;
- FAQ/contact/legal/privacy surfaces.

Public pages may describe available published inventory and participants, but they must not expose private rates, tenant data, unsupported live metrics or internal operational state.

## 22.1 Global

- Sign-in / identity
- Workspace selection
- Role-specific Home
- Human Tasks / Approvals / Exceptions
- Notifications
- Profile/security

## 22.2 Advertified/internal

- Opportunities
- Evidence/research
- Strategy
- Briefs and version history
- STP/Audiences
- Media Mix
- Inventory Intelligence
- Inventory product details
- Inventory imports/review
- Shortlist/inventory selection
- Supply/availability
- Media Plan review
- Proposals
- Proposal document preview/render
- Campaigns
- Suppliers
- Agent operations
- Audit
- Commercial policy/settings
- Access
- Integrations

## 22.3 Agency/Advertiser

- Home/priority approvals
- Briefs
- Audience/STP review
- Media Mix review
- Proposals/client decision
- Campaign tracking
- Performance/reporting
- Team/access
- Agent operations, current per-run AI cost caps and tenant-attributable AI usage/cost for Agency Admin only

## 22.4 Supplier

- Supplier home
- Inventory catalogue
- Bulk imports
- Availability
- Requests/RFQs
- Quotes
- Bookings
- Delivery/proof obligations where applicable

## 22.5 Creator / Influencer / Specialist

- Profile and represented identity
- Audience/platform evidence where applicable
- Rate card / deliverable pricing
- Assignment requests
- Deliverables, usage rights and exclusivity terms
- Creative/proof submission
- Earnings/payment status where within the approved commercial model

---

# 23. Technical Architecture

## 23.1 Locked logical boundaries [Principle]

### Web

Current stack:

- React 19.2
- TypeScript
- Vite

Web owns authenticated UI and client-side interaction. It does not own canonical business state, database access or model-provider credentials.

### Commercial API

Current stack:

- C# / .NET 10
- ASP.NET Core
- relational persistence/migrations

The Commercial API is the only canonical commercial write boundary.

### Agent runtime

Current stack:

- Python 3.12-compatible
- FastAPI

The runtime owns specialist agent execution and provider/tool orchestration, not canonical business records.

### Data

Current baseline:

- PostgreSQL 16
- PostGIS
- pgvector where evidence-backed semantic retrieval provides value

### Object storage

S3-compatible storage for:

- original files;
- evidence snapshots;
- extracted assets;
- generated documents.

### Cache/queues

Redis or other approved infrastructure may be used where measured need exists, but it must not become a second source of commercial truth.

## 23.2 Production infrastructure topology [Policy]

Advertified is currently AWS-oriented, including `af-south-1` where appropriate.

The precise production topology (for example EC2 vs managed container compute, database hosting, load balancing and supporting services) is an **infrastructure policy decision**, not a product principle.

Choose the simplest production-grade topology that meets:

- security;
- availability;
- backup/recovery;
- observability;
- performance;
- cost requirements.

Do not introduce expensive infrastructure merely because it is architecturally fashionable.

## 23.3 Modular-monolith default [Principle]

The Commercial API remains a modular monolith unless measured scaling or isolation needs justify a service split.

Do not create microservices, message buses or duplicate orchestration systems without evidence and an approved architecture decision.

---

# 24. Canonical Data Conventions

## 24.1 Data conventions [Principle]

- UUID identifiers owned by the canonical service.
- UTC timestamps in storage/API; user display is timezone-aware.
- Money as currency + integer minor units.
- Immutable artefact versions for consequential planning/commercial artifacts.
- Optimistic concurrency for mutable aggregates.
- Tenant scope on all protected records.
- Append-only audit events for consequential actions.
- Original evidence/source files retained under policy.
- Business deletion follows retention/privacy policy; no casual hard delete.

## 24.2 Core canonical records [Policy]

Advertified's canonical domain includes, at minimum:

### Identity/commercial

- Tenant
- User
- Membership
- ClientAccount / resolved client identity
- Contact
- Opportunity

### Evidence/planning

- EvidenceSource
- EvidenceItem
- EvidenceConflict
- StrategyVersion
- CampaignBrief
- BriefVersion
- STPVersion
- AudienceDefinition
- MediaMixVersion
- InventoryShortlistVersion
- MediaPlanVersion
- MediaPlanLine
- BenchmarkSnapshot

### Supply

- Supplier
- InventoryProduct
- InventoryRate
- InventoryAvailability
- InventoryAsset
- InventoryImport
- InventoryCandidate
- InventoryReviewDecision
- SpatialLocation

### Commercial transaction

- ProposalVersion
- ProposalOption/Tier
- RFQ
- SupplierResponse
- PurchaseOrder
- PaymentIntent
- Invoice
- Booking
- Campaign
- CreativeAsset
- DeliveryProof
- PerformanceMetric

### Governance/automation

- Approval
- ApprovalAssignment
- HumanTask
- MaterialityPolicyVersion
- AgentRun
- ToolInvocation
- AIUsageLedger
- AuditEvent
- OutboxMessage
- IdempotencyRecord
- InboundCampaignEmail
- EmailProposalAutomationRun

## 24.3 Master data [Principle]

Governed vocabularies belong in versioned master/reference data, not scattered magic strings.

Examples:

- channels;
- product types;
- rate types;
- lifecycle states;
- roles/permissions;
- rejection reasons;
- proposal/package labels;
- currencies;
- integration types;
- document classes;
- asset types;
- measurement units;
- materiality rules/freshness policies where configurable.

Stable codes must never be silently repurposed.

---

# 25. Commands, Events and Durable Work

## 25.1 Command rules [Principle]

A consequential command should bind:

- tenant;
- actor;
- resource/version;
- idempotency key;
- correlation ID;
- permission;
- materiality/approval state where required.

Accepted state changes write canonical state and audit atomically where possible.

## 25.2 Idempotency [Principle]

Duplicate requests/callbacks must not duplicate:

- proposals;
- sends;
- bookings;
- payments;
- supplier requests;
- imports;
- paid AI calls.

## 25.3 Events [Principle]

Domain events describe committed business facts. An agent completing a run does not itself mean a business artefact is approved.

## 25.4 Human tasks [Principle]

A HumanTask represents a real decision or exception.

It is not merely a navigation link.

Each task should explain:

- why it matters;
- affected resource/version;
- relevant evidence/conflict;
- one primary action;
- allowed alternatives;
- assignee;
- due/recovery state where relevant.

---

# 26. External Integrations

## 26.1 Adapter principle [Principle]

External providers implement Advertified-owned interfaces.

Provider SDK objects and provider-specific status names must not leak into core commercial state.

Relevant integration classes include:

- OIDC identity;
- AWS Bedrock or other approved AI provider;
- Docling/document extraction;
- S3-compatible object storage;
- transactional email (currently Resend direction);
- maps/geocoding/routes/POIs;
- payment/funding;
- supplier systems;
- measurement platforms;
- future licensed audience data.

## 26.2 Provider resilience [Principle]

Every provider adapter must define:

- typed request/response;
- authentication boundary;
- timeout;
- retry classification;
- idempotency/reconciliation strategy;
- health signal;
- deterministic test double;
- business-safe failure/recovery.

Provider unavailability must preserve canonical business state.

---

# 27. Security, Privacy and Trust

## 27.1 Security principles

- Deny by default.
- Tenant isolation at API/data boundaries.
- Least privilege.
- No secrets in browser bundles, prompts, logs or source control.
- Parameterised queries and typed validation.
- Secure session/OIDC.
- CSRF protection where cookie auth is used.
- CORS allow-list.
- File type/size/malware controls.
- Rate limits and bounded heavy operations.
- Dependency/image scanning.
- Append-only audit for consequential actions.
- Provider/model usage and cost audit.

## 27.2 POPIA/privacy [Policy]

Advertified must apply:

- purpose limitation;
- minimal data collection;
- lawful basis/authority tracking where applicable;
- retention policy;
- access/correction/deletion or restriction processes where legally applicable;
- provider/cross-border review where required;
- no consumer identity graph as a prerequisite for the core product.

Exact retention periods are governed policy and require legal/privacy sign-off before production use.

## 27.3 Anti-prompt-injection rule [Principle]

Websites, documents, emails and tool results are untrusted data.

Embedded text attempting to alter system instructions, permissions, tools, destinations or commercial authority is never treated as an instruction to the system.

---

# 28. Engineering Guardrails

These rules exist to keep the implementation maintainable and production-grade.

## 28.1 Code structure [Policy]

- Apply SOLID.
- One canonical owner for each business rule.
- No god classes/services/hooks.
- No parallel domain models.
- Prefer cohesive modules over arbitrary numbered fragments.
- Source files should remain at or below the repository's governed line limits.
- Functions should remain small and understandable.
- Avoid excessive cyclomatic complexity.
- No dead commented-out implementation.
- No TODO without owner/issue where the repo policy requires one.
- Line limits are maintainability guardrails, not permission to split one cohesive business rule into artificial numbered fragments.
- If a genuinely cohesive rule or named algorithm cannot be made clearer within the normal file/function limit, an exception requires explicit owner approval, a written rationale adjacent to the implementation, and evidence that the exception is narrower and more maintainable than an artificial split.
- An exception applies only to the named implementation; it does not weaken the general line-limit policy or become precedent automatically.

## 28.2 Duplication [Principle]

Fix reusable causes, not only the current client/example.

Do not introduce:

- duplicate DTOs where generated contracts exist;
- duplicate approval systems;
- separate Rapid OOH aggregates;
- duplicated business calculations;
- client-specific production branches;
- supplier-name conditionals in generic extraction logic.

## 28.3 N+1, scale and caching [Policy]

- Avoid N+1 query patterns.
- Paginate large collections.
- Batch repeated reads where appropriate.
- Cache only data that can safely be cached and always preserve authoritative source/version semantics.
- Measure heavy work rather than guessing performance.

## 28.4 Tests [Policy]

Tests should prove:

- domain invariants;
- lifecycle transitions;
- authorization/tenant boundaries;
- contracts;
- migration safety;
- real regressions;
- critical user journeys;
- agent/evidence safety;
- inventory unseen-file behaviour.

Avoid unnecessary duplicated tests, testing framework behaviour or inflating test counts for appearance.

Use parameterised cases where they represent the same rule.

---

# 29. Reliability, Operations and Production

## 29.1 Reliability [Principle]

- Durable long-running work.
- Retry only safe transient failures.
- No blind retry after ambiguous paid/external acceptance.
- Dead-letter/review path for poison work.
- Safe resume from persisted checkpoint.
- No duplicate external action under replay.
- Degraded-mode UI rather than fabricated success.

## 29.2 Observability [Principle]

Correlate a user action to the eventual business outcome across:

- web;
- API;
- workers;
- agent runtime;
- providers;
- audit.

Track structured logs, metrics and traces without exposing secrets, unnecessary PII or private reasoning.

## 29.3 Backups and recovery [Policy]

Production requires:

- encrypted backups;
- point-in-time recovery appropriate to the chosen database topology;
- tested restore;
- defined RPO/RTO;
- rollback/compensating procedure;
- incident ownership.

Exact targets are release policy and must be validated against the deployed infrastructure/cost model.

## 29.4 Cost discipline [Principle]

Build and operate as a production system without unnecessary infrastructure or unnecessary paid AI calls.

Choose the simplest architecture that safely meets the requirement.

AI usage must be visible by workflow/provider/model and attributable to the resulting commercial run.

---

# 30. Success Metrics and Validation

## 30.1 Operational metrics [Policy/Hypothesis]

Measure, with definitions approved before they become KPIs:

### Workflow

- Brief-to-approved-proposal turnaround
- Proposal turnaround by workflow type
- Human review time

### Agent/evidence quality

- Material correction rate by field/agent/source
- Unsupported claim rate
- Evidence completeness
- Review-required rate
- Later-reversed correction rate

### Inventory

- Extraction precision/recall
- Sellable-item recall
- Unresolved review rate
- Rate freshness
- Availability freshness
- Supplier-maintained percentage

### Commercial

- Proposal selection/conversion
- Booking conversion
- Client fee revenue
- Transaction value
- Repeat campaign use

### Supplier

- Enquiry-to-response time
- Quote-to-booking conversion
- Supplier-maintained inventory
- Incremental/new-buyer signals
- Repeat supplier participation

### Reliability/cost

- API/job reliability
- queue age
- recovery without duplicate effects
- incremental AI cost per workflow

## 30.2 Do not claim unsupported benefits [Principle]

Advertified must apply its own evidence discipline to its marketing and internal claims.

Do not state percentage time savings, incremental revenue, conversion uplift or pricing improvement until a defensible measurement supports the claim.

---

# 31. Immediate Pilot Validation Priorities [Hypothesis]

The next useful step is not additional conceptual design. It is falsifying the highest-risk business assumptions cheaply.

## Priority 1 — Retention after introduction

**Question:** Once buyer and supplier know each other, does the buyer continue using Advertified?

Measure:

- repeat campaign usage;
- percentage of repeat supplier relationships still processed/recorded in Advertified;
- which stages are reused;
- where usage drops out;
- whether previous quotes/approvals/proof/history are retrieved later.

If usage cliffs after introduction, the anti-disintermediation thesis must be reconsidered.

## Priority 2 — Supplier participation

**Question:** Will suppliers maintain usable inventory and respond to qualified Advertified enquiries when core participation is free?

Measure:

- onboarding completion;
- rate/availability freshness;
- response rate;
- response speed;
- quality of replies;
- quote-to-booking conversion;
- repeat participation.

## Priority 3 — Buyer willingness to pay and structured-workflow value

**Question:** Do agencies/advertisers value the structured workflow and institutional commercial history enough to pay for it instead of reverting to email/spreadsheets/WhatsApp?

Measure:

- willingness to pay;
- repeat usage;
- retrieval/reuse of previous commercial records;
- proposal/approval workflow adoption;
- time/interaction reduction only where it can be measured credibly.

## Deferred validation

These may be tested later but are not launch-critical:

- paid separated sponsored visibility;
- premium supplier SaaS willingness to pay;
- finance-referral economics;
- long-term defensibility of the commercial graph as a moat.

---

# 32. Non-Normative Business Validation & Hypothesis Resolution Register

Nothing in this section defines system correctness, architecture, security, production readiness or release permission. These are business/product questions to be tested using real evidence. Advertified must remain correct, secure and commercially truthful whether any hypothesis is confirmed, falsified or remains inconclusive.

## 32.1 Hypothesis lifecycle [Principle]

A hypothesis may not silently become load-bearing product logic. Every material hypothesis has a durable resolution record:

```text
Hypothesis ID
→ original claim
→ horizon
→ named owner
→ test method
→ pre-declared decision criteria
→ evidence collected
→ evidence outcome
→ disposition
→ decision owner
→ decision date
→ ADVERTIFIED.md version changed, if any
```

The evidence outcome has exactly three states:

1. **CONFIRMED** — the defined test produced sufficient evidence supporting the claim.
2. **FALSIFIED** — the defined test produced sufficient evidence against the claim.
3. **INCONCLUSIVE** — the evidence cannot support either conclusion. The record must state why, such as insufficient sample, weak isolation, poor data quality or insufficient observation period, and what the next valid test would require.

An evidence outcome does **not** edit this specification automatically. After the evidence outcome, the named owner must record one explicit disposition:

- **PROMOTE_TO_PRINCIPLE** — only when the owner deliberately makes the validated claim a durable normative commitment; confirmation alone is not enough.
- **PROMOTE_TO_POLICY** — when evidence justifies a governed operational/commercial rule that may evolve.
- **VALIDATED_NON_NORMATIVE** — retain the finding as useful business evidence without turning it into a production invariant.
- **REVISE** — replace the claim with a narrower or materially different hypothesis and issue a new/revised hypothesis record.
- **REJECT** — remove the claim from active product/revenue assumptions while preserving the resolution record.
- **RETEST** — keep it unresolved because the result was inconclusive, with the next test explicitly defined.

No agent, developer or document editor may promote a hypothesis merely by changing `[Hypothesis]` to `[Principle]` or `[Policy]`. The retained resolution record is the justification for any such change.

## 32.2 Evidence quality [Principle]

Hypothesis evidence is governed with the same discipline Advertified applies to commercial evidence:

- retain the original test definition before inspecting the result where practical;
- preserve source data, observation period, cohort definition and exclusions;
- distinguish observed behaviour from interviews, stated preference and willingness-to-pay;
- do not treat a survey answer as equivalent to an actual paid transaction;
- record material confounders and missing data;
- do not selectively redefine success after seeing the outcome;
- preserve contradictory evidence;
- record who interpreted the result and any financial incentive they have in the outcome.

## 32.3 Current active hypotheses

| ID | Hypothesis | Horizon | Cheapest credible first test | Resolution signal |
|---|---|---|---|---|
| **HYP-001** | Buyers will remain active after direct supplier relationships form. | Pilot | Instrument introduced buyer/supplier pairs and observe whether proposals, approvals, proof, reporting and later campaigns continue through Advertified after the first booking. | Sustained meaningful workflow use over 2–3 campaign cycles supports the claim. Sharp post-introduction drop-off falsifies the proposed retention mechanism and requires redesign. |
| **HYP-002** | Free core supplier participation will create sufficient inventory density and freshness. | Pilot | Onboard a real supplier cohort at no core listing/enquiry fee; measure onboarding completion, inventory/rate freshness, response rate and repeat participation. | Sustained maintenance and response at a defined usable level supports the model; stale inventory or weak participation despite qualified demand argues against it. |
| **HYP-003** | A buyer-side platform/management fee is the best first monetization model. | Pilot | Quote a real transparent platform/management fee to the first agency/advertiser cohort rather than only asking willingness-to-pay questions. | Actual paid acceptance supports the sequence. Repeated refusal with continued product demand indicates the revenue sequence should change rather than implying the product itself failed. |
| **HYP-004** | Agencies value institutional commercial memory enough to displace meaningful spreadsheet/email/WhatsApp behaviour. | Pilot | Observe whether users voluntarily return to prior quotes, approvals, supplier responses, proof and campaign history instead of reverting to ad hoc records. | Repeated retrieval/reuse and reduced parallel tracking supports the claim; persistent off-platform duplication weakens or falsifies it. |
| **HYP-005** | Suppliers will pay for specific premium operational capabilities. | Pilot | Use interviews to narrow candidate capabilities, then test one concrete premium capability with a real or commitment-backed price before broad build-out. | Paid adoption of a specific capability supports a Policy for that capability; generic stated interest does not validate the entire premium-tool list. |
| **HYP-006** | Separated sponsored visibility has meaningful willingness-to-pay without influencing suitability rank. | Later | Offer clearly separated sponsored visibility to a small supplier cohort at a real, even nominal, price. | Real paid conversions justify packaging/pricing Policy. Zero meaningful uptake means it should remain non-core or be dropped; suitability ranking remains unaffected either way. |
| **HYP-007** | The accumulated commercial graph becomes a durable operational/relational moat. | Long horizon | Track whether proprietary supplier-response history, negotiated-rate patterns, repeat bookings, proof and measured outcomes compound into data competitors cannot recreate from public sources. | This resolves by multi-period trend and defensibility evidence, not one pilot. It remains long-horizon until replication difficulty and compounding value are demonstrated. |
| **HYP-008** | Advertified can attribute incremental supplier revenue with enough reliability to use the classification commercially. | Pilot/quarterly | Run attribution classifications on real transactions, record evidence and supplier disputes, and independently review ambiguous cases. | Low material dispute/error rates and evidence-backed agreement support a governed attribution Policy; frequent ambiguity means classification must remain analytical only. |
| **HYP-009** | The OOH/DOOH proving wedge generalises economically to other fragmented channels. | Multi-phase | After OOH proves the core workflow, introduce one adjacent fragmented channel using the same canonical model and measure reuse versus channel-specific complexity. | High reuse with acceptable economics supports expansion; substantial bespoke workflow/integration burden means channel architecture or sequencing must be revised. |
| **HYP-010** | A buyer-side fee model can operate without creating pressure to overstate Advertified's indispensability. | Ongoing | Audit buyer-facing savings/value claims, workflow friction and retention metrics against actual evidence and compare product decisions with fee incentives. | Evidence that value claims remain accurate and no artificial dependency is introduced supports the model. Repeated incentive-driven distortion requires fee/product redesign. |

## 32.4 Resolution records

Resolution records are append-only business-governance evidence. A resolved hypothesis is not deleted from history. At minimum retain:

| Field | Required content |
|---|---|
| Hypothesis ID | Stable identifier, never repurposed |
| Original claim | Exact claim at time of test |
| Hypothesis version | Version tested |
| Horizon | Pilot / quarterly / ongoing / long-horizon |
| Owner | Named accountable owner |
| Test method | What was actually tested |
| Decision criteria | Criteria defined before resolution where practical |
| Cohort / period | Who/what and when |
| Evidence references | Durable links/identifiers to collected evidence |
| Limitations/confounders | What the test could not establish |
| Evidence outcome | CONFIRMED / FALSIFIED / INCONCLUSIVE |
| Disposition | PROMOTE_TO_PRINCIPLE / PROMOTE_TO_POLICY / VALIDATED_NON_NORMATIVE / REVISE / REJECT / RETEST |
| Decision rationale | Concise evidence-based explanation |
| Decision owner | Person accountable for disposition |
| Decision date | UTC date/time |
| Specification effect | Exact `ADVERTIFIED.md` section/version changed, or `NONE` |

## 32.5 Production boundary

A hypothesis may justify instrumentation, experiments or optional feature flags, but production safety cannot depend on its truth.

Examples:

- Advertified preserves commercial history as a product capability; whether that history materially improves retention remains HYP-001/HYP-004 until demonstrated.
- Advertified records enquiry, quote, booking and revenue facts; whether those facts prove incremental supplier revenue remains HYP-008 until demonstrated.
- Advertified may expose a separated sponsored-visibility experiment; money still cannot buy suitability rank even if HYP-006 is later confirmed.
- Advertified can launch with transparent buyer fees where approved; HYP-003 determines whether that commercial model works, not whether the software is production-correct.

---

# 33. Explicit Product Principles Register

The principles are grouped by weight so trust/commercial commitments are not visually flattened with workflow or brand constraints.

## 33.1 Trust, commercial and governance principles

1. Advertified is a governed commercial operating system, not a chatbot.
2. The Commercial API is canonical commercial truth.
3. AI proposes; it does not silently become commercial truth.
4. A supplied Brief is preserved as source evidence.
5. Client-supplied statements are not automatically verified facts.
6. Evidence basis, verification state and required action are separate concepts.
7. Material uncertainty is governed by consequence/policy, not a global AI confidence score.
8. Commercial history is preserved rather than overwritten.
9. Price lifecycle and comparison intelligence are separate.
10. Conflicts are resolved only when comparability and precedence are provable.
11. Normalize mathematics where valid; never invent commercial equivalence.
12. Human corrections are evidence, not automatic truth.
13. Self-approval is allowed for authorised humans and must be audited as such.
14. Independent approval must be visibly distinguished from self-approval.
15. Multi-user organisations may optionally assign approval to another eligible user.
16. Self-approval must not be forced through a mandatory submit/wait/approve sequence.
17. AI/service identities cannot approve their own output.
18. Money cannot buy an Advertified suitability score.
19. Advertified must not manufacture complexity or savings claims to justify its own fee.
20. Buyer/supplier direct relationships are expected; retention must come from continuing workflow value.
21. Recommendation logic remains explainable at the individual decision level.
22. Inventory/rate/availability evidence and freshness remain visible.
23. A recommendation is not a booking; provisional supply is not confirmed supply.
24. No invented supplier/client responses or completion evidence.
25. Security/tenant boundaries cannot be weakened to make a workflow pass.
26. The product applies evidence discipline to its own claims and success metrics.

## 33.2 Product and workflow principles

1. OOH_ONLY and FULL_CAMPAIGN use one canonical lifecycle.
2. OOH_ONLY cannot expand into FULL_CAMPAIGN; changed scope starts a new Brief.
3. STP applies to OOH as well as full campaigns.
4. Users may change AI planning recommendations within governed rules.
5. Bounded straight-through automation uses pre-authorised policy, deterministic readiness and full audit.
6. One business rule has one canonical implementation.

## 33.3 Experience and brand constraints

1. User-facing language is commercial and human, not implementation jargon.
2. Progress and metrics must reflect persisted truth.
3. Dark green is not part of the Advertified UI theme.

---

# 34. Explicit Policy Register

The following are deliberately governed and may evolve through versioned decisions:

- materiality rules;
- approval permissions/delegation;
- bounded automation limits;
- evidence freshness windows;
- price precedence rules;
- normalization rules;
- benchmark cohort rules and minimum sample classifications;
- channel rollout sequence;
- commercial fees/pricing;
- supplier free/premium packaging;
- provider/model selection;
- retention periods;
- infrastructure topology;
- SLO/RPO/RTO targets;
- external providers;
- rate limits;
- proposal/package labels;
- supported document classes;
- inventory master data;
- payment/funding routes.

Policy changes must be attributable to a named owner/version and must not silently rewrite historical decisions.

---

# 35. Anti-Patterns and Forbidden Shortcuts

Do not:

- create a separate Rapid OOH domain/workflow;
- silently convert OOH-only to full campaign;
- force client setup before brief intake;
- reduce Brief to simplistic CRUD;
- allow AI to directly mutate canonical database state;
- let prompts define commercial permissions/materiality;
- use model confidence as a substitute for evidence;
- collapse list/quote/negotiated/booked prices;
- silently pick between conflicting evidence;
- infer missing material prices, budget, availability or dates;
- present stale rates as current;
- treat human overrides as automatic truth;
- rank sponsored inventory higher because it is sponsored;
- hide supplier cost/margin decisions from privileged governance where disclosure is required;
- fabricate supplier responses;
- fabricate client approvals;
- fabricate live/provider activity;
- use hidden database edits as workflow completion;
- hard-code client/supplier examples into generic product rules;
- create unnecessary infrastructure or paid AI spend;
- expose internal engineering wording on human-facing screens;
- use dark green as an Advertified theme colour;
- create duplicate sources of commercial truth;
- declare production readiness based on code volume, screenshots or AI confidence.

---

# 36. Production Readiness and Definition of Done

## 36.1 Capability status language [Principle]

A capability may be described as:

- **Absent**
- **Scaffolded**
- **Implemented**
- **Tested**
- **Verified**
- **Blocked**

Do not collapse these into vague percentage claims without a defined denominator.

## 36.2 Production-ready standard [Policy/Principle]

Advertified is production-ready only when the applicable release scope has evidence for:

- canonical business flow;
- role/tenant isolation;
- production identity/authentication;
- migrations/data constraints;
- security testing;
- rate/cost abuse controls;
- agent contract/evidence safety;
- real inventory ingestion;
- real planning/proposal journey;
- approval/self-approval audit behaviour;
- exactly-once external actions;
- delivery/proof where in release scope;
- observability;
- backup/restore;
- failure/recovery;
- business-safe UX;
- accessibility appropriate to release;
- deployment/rollback;
- named human production sign-off.

No AI agent can approve its own correctness or production readiness.

## 36.3 Real-case certification [Policy]

Before broad production launch, certify against genuine owner/UAT cases rather than generated demos, including:

- OOH-only supplied/inbound requirements;
- full multi-channel briefs;
- evidence-led/unbriefed opportunities;
- unseen inventory files;
- supplier operations;
- stale/conflicting commercial inputs;
- restart/recovery;
- cross-tenant negatives.

Exact certification sample sizes may be revised by owner policy, but the evidence must be real, retained and reproducible.

---

# 37. Named Behavioural Examples — Fixtures, Not Product Branches

Named real examples may be retained as acceptance fixtures to prove generic reasoning, including campaigns/opportunities such as:

- Rayetsa Furniture;
- Takealot Black Friday OOH;
- Jameson Select;
- Department of Health vaccination awareness;
- Indlu Properties;
- multilingual church campaign;
- local OOH benchmark examples.

These are test/acceptance evidence only.

They must never create client-specific production branches, hard-coded answers or permanent rules that apply to unrelated customers.

---

# 38. Executable Implementation Contract [Principle]

This section turns the product/business rules above into a build contract. A competent implementation team must be able to derive the application without inventing core Advertified behaviour. Internal class names and code composition may differ, but the observable business behaviour, invariants, permissions, state transitions, evidence rules and external consequences defined here are binding unless the owner changes them.

## 38.1 Canonical command rule

Every consequence-bearing mutation must execute through the Commercial API and bind:

- `tenantId`;
- authenticated `actorId` or least-privilege service identity;
- required permission;
- target resource and exact resource/version where applicable;
- `Idempotency-Key` for replay-sensitive POST commands;
- optimistic concurrency version / ETag where mutable state is involved;
- correlation ID;
- materiality / approval / automation-policy state where applicable.

The accepted command writes canonical state, audit and outbox/business event atomically where practical. An agent output or worker completion never substitutes for an accepted Commercial API command.

## 38.2 Versioned-artifact rule

Consequential planning/commercial artefacts are immutable after approval. Material change creates a new version. Downstream artefacts remain bound to the exact approved input version and are marked stale or require explicit revalidation when a material upstream input changes.

---

# 39. Canonical State Machines and Guards

Only the transitions below, or deliberately added governed extensions, are valid. UI state must be derived from canonical state; the browser may not invent lifecycle progression.

## 39.1 Opportunity lifecycle

| From | Command | Required guards | To | Event |
|---|---|---|---|---|
| `CREATED` | `StartQualification` | authorised owner/actor; at least one permitted source or source plan | `QUALIFYING` | `OpportunityQualificationStarted` |
| `QUALIFYING` | `SubmitEvidenceForReview` | capture complete enough for review; failures retained | `EVIDENCE_REVIEW` | `OpportunityEvidenceSubmitted` |
| `EVIDENCE_REVIEW` | `ApproveEvidenceSet` | material evidence reviewed; unresolved gaps explicitly recorded | `STRATEGY_READY` | `OpportunityEvidenceApproved` |
| `STRATEGY_READY` | `ApproveStrategy` | exact StrategyVersion; no unresolved critical critic objection | `BRIEF_READY` | `StrategyApproved` |
| `BRIEF_READY` | `ApproveBriefVersion` | complete approvable BriefVersion; material conflicts resolved/accepted | `PLANNING` | `BriefApproved` |
| `PLANNING` | `ApproveProposal` | proposal references current approved plan and totals reconcile | `PROPOSAL_READY` | `ProposalApproved` |
| `PROPOSAL_READY` | `MarkWon` | selected proposal option/client decision exists | `WON` | `OpportunityWon` |
| active pre-WON state | `MarkLost` | reason recorded | `LOST` | `OpportunityLost` |
| `WON`/`LOST` | `Archive` | no unresolved consequential task | `ARCHIVED` | `OpportunityArchived` |

An Opportunity path may generate strategy before Brief creation. A supplied Brief does not need an Opportunity.

## 39.2 BriefVersion lifecycle

| From | Command | Required guards | To | Notes |
|---|---|---|---|---|
| — | `CreateBriefVersion` | source retained; required structure or explicit unknowns | `DRAFT` | version number increments; prior versions retained |
| `DRAFT` | `MarkBriefVersionReady` | deterministic validation passes; material unknowns explicitly represented | `DRAFT` with readiness recorded | readiness is not approval |
| `DRAFT` | `SubmitBriefVersion` | valid reviewable version | `IN_REVIEW` | used when independent review is deliberately requested |
| `DRAFT` or `IN_REVIEW` | `ApproveBriefVersion` | actor has `brief_approve`; exact version; material blockers resolved; account approval policy permits actor | `APPROVED` | authorised self-approval may be one action; no artificial wait step |
| `IN_REVIEW` | `RejectBriefVersion` | authorised reviewer; reason required | `REJECTED` | |
| `APPROVED`/`REJECTED` | `CreateBriefRevision` | authorised edit; base version identified | new `DRAFT` | copies by value and preserves evidence lineage |

`OOH_ONLY` / `FULL_CAMPAIGN` mode is immutable for the CampaignBrief once locked. Changing from OOH-only to multi-channel always creates a new CampaignBrief/planning lineage.

## 39.3 Strategy / STP / Media Mix / Media Plan lifecycle

| Artefact | Create state | Approval command | Approval guard | Approved effect |
|---|---|---|---|---|
| StrategyVersion | `DRAFT` | `ApproveStrategy` | evidence bindings exist; no unresolved critical objection | exact version becomes approved strategy |
| STPVersion / AudienceDefinitionSet | generated draft/current version | `ApproveStp` or bounded approved planning command | segmentation, targeting, positioning, exclusions and evidence are valid | enables media mix |
| MediaMixVersion | `DRAFT` | `ApproveMediaMix` | allocations reconcile to planning budget; channel set permitted by campaign mode | enables shortlist/supply work |
| InventoryShortlistVersion | generated | `SelectShortlist` | all selected items pass hard eligibility; rejected items keep reason | freezes selected/rejected candidate decision for plan |
| MediaPlanVersion | `DRAFT` | `ApproveMediaPlan` | line totals reconcile; rates/freshness/supply states valid; no unresolved critical objection | enables proposal generation |

A materially changed brief, rate, availability, mix, shortlist or plan input invalidates or marks affected downstream draft artefacts stale. Approved historical versions remain immutable.

## 39.4 Proposal lifecycle

| From | Command | Guard | To |
|---|---|---|---|
| — | `GenerateProposal` | approved/current plan(s); account commercial policy | `DRAFT` |
| `DRAFT` | `UpdateProposal` | no change to canonical money/inventory through narrative-only edits | `DRAFT` new version or updated draft version per implementation |
| `DRAFT`/`IN_REVIEW` | `ApproveProposal` | exact proposal version; totals/terms/evidence valid; actor permitted by approval policy | `APPROVED` |
| `IN_REVIEW` | `RejectProposal` | reason required | `REJECTED` |
| `APPROVED` | `RenderProposal` | approved exact structured version | `APPROVED` + immutable document version |
| `APPROVED` | `ShareProposal` | valid recipient/delivery route; required approval/policy basis; idempotency | `SENT` |
| `SENT` | `SelectProposalOption` | option exists, current, not expired and has no unresolved supplier-inventory impact | `SELECTED` |
| `SENT` | `DeclineProposal` | authorised client actor | `DECLINED` |
| any state before confirmed booking/committed supply | `RegisterInventorySupersessionImpact` | one or more referenced supplier product/rate/availability versions were replaced | lifecycle state retained + `INVENTORY_REVIEW_REQUIRED`; approval/send/select/booking actions blocked |
| `APPROVED`/`SENT` | `ExpireProposal` | expiry reached or authorised expiry | `EXPIRED` |

Inventory supersession is an orthogonal stale-input condition, not permission to rewrite a proposal.
The exact old line remains visible, the impact is recorded against the affected version and any
replacement creates a new planning/proposal version after authorised review. A sent stale proposal
remains readable to its recipient but cannot be newly accepted. Confirmed bookings and historical
commercial documents remain bound to their original snapshot.

Client-facing proposal options may be 1–3 genuinely different routes. Package names such as Launch/Boost/Scale/Dominance are commercial bands, not automatically proposal-option names.

## 39.5 Inventory import lifecycle

| From | Command | Guard | To |
|---|---|---|---|
| — | `CreateInventoryImport` | authorised source; size/type/hash and explicit replacement mode captured | `UPLOADED` |
| `UPLOADED` | `ProtectAndClassify` | malware/type checks | `CLASSIFYING` |
| `CLASSIFYING` | `Extract` | explicit upload/execute command; document class/parser strategy selected | `EXTRACTING` |
| `EXTRACTING` | `ValidateCandidates` | source locators/candidates and supplier-identity evidence produced | `VALIDATING` |
| `VALIDATING` | `OpenReview` | validation outcomes persisted; existing Supplier ID linked, one unclaimed Supplier created, or ambiguous supplier match recorded as blocker | `REVIEW_REQUIRED` |
| `REVIEW_REQUIRED` | `ResolveSupplierIdentity` | authorised administrator selects/merges the permanent Supplier ID where automatic resolution was ambiguous | remains review until supplier and candidate blockers are resolved |
| `REVIEW_REQUIRED` | `ReviewCandidate` | reviewer decision with provenance | remains review until all blockers resolved |
| `REVIEW_REQUIRED` | `PublishApprovedInventory` | no unresolved publish blocker; permanent Supplier ID resolved; administrator or supplier publication authority | `PUBLISHING` |
| `PUBLISHING` | `CompleteImportAndCutover` | new release committed; previous supplier release superseded; pending prior work soft-deleted; proposal impacts persisted atomically and idempotently | `COMPLETED` |
| any active stage | `FailImport` | classified failure and checkpoint recorded; current supplier release remains unchanged | `FAILED` |
| `FAILED` | `ResumeImport` | retryable/corrected input; safe checkpoint | previous safe stage |

Creating an import does not start an always-running inventory scan. The upload/execute command wakes
the durable processor for that import. Only an active external extraction task may have bounded
status checks. When no attempt is active, inventory extraction is dormant. Supplier claiming is
independent of publication: an administrator-managed `UNCLAIMED` Supplier may have current usable
inventory, and accepting its later invitation attaches the user to the same Supplier ID without
changing inventory history.

## 39.6 Inbound OOH automation lifecycle

| From | Command | Guard | To |
|---|---|---|---|
| — | `ReceiveInboundEmail` | configured mailbox, verified callback/signature, unique provider message ID/content hash | `RECEIVED` |
| `RECEIVED` | `StartAutomatedProposal` | tenant opt-in; valid sender/reply address; source retained | `PROCESSING` |
| `PROCESSING` | `CompleteAutomatedProposal` | OOH_ONLY; complete brief; STP/mix/supply/plan/proposal/document current; readiness policy passes | `SENT` |
| `RECEIVED`/`PROCESSING` | `RequireAutomationReview` | any missing/conflicting material fact, non-OOH scope, stale/insufficient supply or policy failure | `REVIEW_REQUIRED` |
| `PROCESSING` | `FailAutomation` | terminal classified provider/implementation failure | `FAILED` |
| any | `DetectDuplicateInboundEmail` | duplicate provider ID/content hash | `DUPLICATE` |

Retry resumes from the last safe checkpoint and may not duplicate a proposal, document, message or paid AI attempt.

## 39.7 Marketplace / RFQ lifecycle

| Aggregate | From | Command | To | Guard |
|---|---|---|---|---|
| Listing | `DRAFT` | `PublishListing` | `PUBLISHED` | supplier owns listing; required publish fields/evidence valid |
| Listing | `PUBLISHED` | `ArchiveListing` | `ARCHIVED` | authorised supplier/admin |
| RFQ | `DRAFT` | `SendRFQ` | `SENT` | verified supplier target; authorised external action; idempotency |
| RFQ | `SENT` | `SubmitSupplierResponse` | `RESPONDED` | supplier owns response; rate/availability evidence retained |
| RFQ | `RESPONDED` | `AcceptSupplierResponse` | `ACCEPTED` | authorised buyer/planner; affected plan lineage updated |
| RFQ | open | expiry job/command | `EXPIRED` | due/validity reached |

Supplier responses never silently overwrite older rates/availability. They create new dated commercial evidence/versions.

## 39.8 Booking / funding / campaign lifecycle

| Aggregate | From | Command | Guard | To |
|---|---|---|---|---|
| Booking | — | `CreateBooking` | selected immutable proposal option/line | `DRAFT` |
| Booking | `DRAFT` | `RequestSupplierConfirmation` | supplier/terms resolved | `PENDING_SUPPLIER` |
| Booking | `PENDING_SUPPLIER` | `ConfirmBooking` | supplier-authorised confirmation | `CONFIRMED` |
| PurchaseOrder | —/`DRAFT` | `SubmitPurchaseOrder` | exact selected option, signed PO evidence, amount/currency | `SUBMITTED` |
| PurchaseOrder | `SUBMITTED` | `ApprovePurchaseOrder` | finance permission; reconciliation valid | `APPROVED` |
| Invoice | `DRAFT` | `IssueInvoice` | accepted proposal + approved PO; totals reconcile | `ISSUED` |
| PaymentIntent | —/`DRAFT` | `StartPayment` | allowed payment route and invoice | `PENDING` |
| PaymentIntent | `PENDING` | `ConfirmPayment` | verified provider receipt or authorised reconciliation | `CONFIRMED` |
| Campaign | `PLANNED` | `ConfirmBookings` | required bookings confirmed | `BOOKED` |
| Campaign | `BOOKED` | `RequestCreative` | booked-format requirements known | `CREATIVE_PENDING` |
| Campaign | `CREATIVE_PENDING` | `ApproveCreativeReadiness` | required creative approvals/technical readiness complete | `READY` |
| Campaign | `READY` | `StartCampaign` | start condition/time and delivery dependencies satisfied | `LIVE` |
| Campaign | `LIVE` | `CompleteCampaign` | delivery window closed; proof workflow opened | `COMPLETED` |

## 39.9 Agent run lifecycle

| From | Command | Guard | To |
|---|---|---|---|
| — | `QueueAgentRun` | exact input versions + policy frozen | `QUEUED` |
| `QUEUED` | `StartRun` | provider/tool/cost policy valid | `RUNNING` |
| `RUNNING` | `RequireHumanReview` | tool/agent/readiness identifies review requirement | `WAITING_FOR_HUMAN` |
| `RUNNING` | `CompleteRun` | typed output validates and is persisted as proposal artefact | `COMPLETED` |
| `RUNNING` | `FailRun` | classified error and durable checkpoint | `FAILED` |
| `FAILED`/`WAITING_FOR_HUMAN` | `ResumeRun` | safe checkpoint; unchanged inputs or explicit rerun policy | `RUNNING` |
| active | `CancelRun` | safe cancellation | `CANCELLED` |

---

# 40. Canonical Data Model and Invariants

All protected business records are tenant-safe. UUIDs are stable identifiers. UTC is stored; display uses workspace/user timezone. Money uses ISO currency + integer minor units. Large binaries live in object storage with immutable hash/version references.

## 40.1 Identity and organisations

| Entity | Minimum required fields | Invariants |
|---|---|---|
| Tenant | id, type, legalName, tradingName, status, timezone, currency, VAT profile, settings, timestamps | active membership required for tenant access; stable tenant identity |
| User | id, email, displayName, status, auth linkage, timestamps | case-insensitive email uniqueness within identity policy; no provider password in domain tables |
| Membership | id, tenantId, userId, roleCode, status, assignment metadata | role from governed registry; unique active tenant/user membership as policy defines |
| ClientAccount | id, owning tenant, legal/trading name, external reference?, billing profile, status | tenant scoped; not required before initial brief intake |
| Contact | id, tenantId, clientId?, purpose, name, email/phone where supplied, status | purpose limited; privacy/consent basis where applicable |
| Supplier | id, tenantId, legal/trading name, claimStatus, verification level, contacts, status, commercial terms, createdFromImportId? | permanent identity survives claiming and inventory replacement; an administrator-managed unclaimed supplier may own active inventory; supplier users can mutate only their own claimed supplier resources |
| SupplierClaimInvitation | id, tenantId, supplierId, invitedEmail, roleCode, status, expiresAtUtc, tokenHash, createdBy/At, revokedBy/At?, acceptedUserId/At? | single-use, expiring, revocable and auditable; acceptance attaches the user to the existing Supplier ID and never creates a second supplier or exposes raw invitation credentials |

## 40.2 Evidence and opportunity

| Entity | Minimum fields | Invariants |
|---|---|---|
| EvidenceSource | id, tenantId, sourceType, locator/objectKey, title, contentHash, capturedAt, policyBasis, status | original source immutable; duplicate handling by tenant/type/hash |
| EvidenceItem | id, sourceId, locator, claimType, raw/structured value, evidence basis, verification state, required action, reviewedBy/At | material claims retain source locator and review lineage |
| EvidenceConflict | id, affected fact/resource, competing evidence IDs, comparison state, resolution, resolver | no silent overwrite of unresolved material contradiction |
| Opportunity | id, tenantId, clientId?, title, sourceType/ref, owner, stage, expected value?, deadline?, problem/objective summaries, version | stage only changes by named commands |
| OpportunityAngle | id, opportunityId, version, title, rationale, evidence IDs, status | rejected angles retained; selection auditable |
| StrategyVersion | id, opportunityId, versionNo, diagnosis, growth thesis, objectives, audiences, positioning/proposition, channel implications, risks, evidence IDs, status | immutable after approval; critic objections bound to version |

## 40.3 Campaign brief and planning

| Entity | Minimum fields | Invariants |
|---|---|---|
| CampaignBrief | id, tenantId, clientId?, opportunityId?, sourceType/ref, title, owner, lifecycleStatus, currentDraftVersionId?, approvedVersionId? | one canonical brief aggregate per campaign intent |
| BriefVersion | id, briefId, versionNo, campaignMode, businessProblem, objective, audiences, geography, timing, budget/currency, VAT, fees, constraints, measurement, claims, unknowns, assumptions, conflicts, evidence IDs, status, creator | immutable after approval; campaign mode cannot change within brief |
| STPVersion | id, briefVersionId, versionNo, segmentation, targets, rationale, exclusions, positioning, audience promise, reasons-to-believe, message pillars, evidence IDs, status | required for OOH_ONLY and FULL_CAMPAIGN |
| AudienceDefinition | id, stpVersionId, name, need state/buying context, geography/context, lawful demographic evidence?, exclusions, evidence IDs, status | individual sensitive attributes never inferred |
| MediaMixVersion | id, briefVersionId, stpVersionId, versionNo, totalBudget, allocations, channel roles, flighting, assumptions, evidence IDs, status | allocation total equals governed planning budget; OOH_ONLY permits only OOH/DOOH |
| InventoryShortlistVersion | id, briefVersionId, versionNo, candidate scores/facts, selected IDs, rejection reasons, evidence/retrieval version IDs, assumptions, status | only hard-eligible inventory can be selected |
| MediaPlanVersion | id, briefVersionId, mixVersionId, shortlistVersionId, versionNo, totals, forecast, assumptions, supply state, critic objections, status | references exact approved inputs |
| MediaPlanLine | id, planVersionId, productVersionId, rateVersionId, availabilityVersionId?, dates, quantity, supplierCost, clientPrice components, VAT/fees, forecast | line money reconciles; provisional supply labelled |

## 40.4 Inventory and commercial supply

| Entity | Minimum fields | Invariants |
|---|---|---|
| InventoryProduct | id, supplierId, channelCode, productTypeCode, supplierProductCode?, name, description, geography, governed attributes, verification state, lifecycle state, currentReleaseId? | product identity stable; material change creates version/history; expired/superseded products remain historical but are excluded from new planning |
| InventoryProductVersion | productId, versionNo, inventoryReleaseId, canonical attributes, evidence bindings, effective dates, supersededByVersionId? | recommendations bind exact version; a later release never rewrites a referenced historical version |
| InventoryRate | id, productId/version, rateType, amountMinor, currency, VAT basis/status, commission/discount metadata where explicit, inclusions/exclusions, validFrom/To, evidenceId, status | no silent raw-unit comparison; history retained |
| InventoryAvailabilityException | id, productId/version, start/end, type (`NOT_AVAILABLE`, `BLACKOUT`, confirmed booking conflict), source/evidence, recordedAt | absence means planning-available; only an overlapping exception or confirmed booking conflict blocks the requested dates; booking remains separately confirmed |
| InventoryAsset | id, productId/version, type, objectKey, mime, hash, dimensions?, sourceLocator, rights/review status | source/rights retained |
| InventorySpatialLocation | productVersionId, point/geometry, coordinate source, verification, resolved geography version | OOH/DOOH spatial truth uses PostGIS when verified |
| InventoryImport | id, tenantId, supplierId?, sourceObjectKey, hash, class, replacementMode, pipeline status, schema/parser version, counts, failure summary | same hash idempotent unless explicit reprocess; admin Day 0 default is `FULL_REPLACEMENT`; no cutover before successful publication |
| SupplierInventoryRelease | id, tenantId, supplierId, importId, versionNo, replacementMode, status, effectiveAtUtc, supersedesReleaseId?, expiredAtUtc?, createdBy | at most one current release per supplier; successful cutover is atomic; historical releases and links are immutable; failed imports cannot change the current release |
| InventoryCandidate | id, importId, source locator, raw fields, normalized proposed fields, evidence bindings, validation issues, review status, supersededAtUtc?, softDeletedAtUtc? | never public before publish; pending records from an older supplier release leave operational views but remain auditable |
| InventoryReviewDecision | id, candidateId, decision, field changes, reason, reviewer, timestamp | append-only review history |
| InventoryBenchmarkSnapshot | id, target product/rate version, policy version, comparison basis/geography, peer product/rate IDs, statistics, confidence reasons, createdAt | immutable when used by shortlist/plan/proposal; reproducible |

## 40.5 Proposal, marketplace, finance and delivery

| Entity | Minimum fields | Invariants |
|---|---|---|
| ProposalVersion | id, briefVersionId, planVersionIds, versionNo, title, executive summary, terms, expiry, status, inventoryReviewState, documentId? | cannot remain actionable after referenced material input changes; old version remains immutable/readable while supersession impact blocks consequences |
| ProposalOption | id, proposalVersionId, label, budget, outcomes, included plan version, display order | 1–3; distinct in meaningful scope/outcome where multiple |
| ProposalDocument | id, proposalVersionId, format, objectKey, hash, generatedAt, template/version | exact source proposal reference retained |
| ProposalInventoryImpact | id, tenantId, proposalVersionId, proposalLineId, supplierId, oldReleaseId, replacementReleaseId, old product/rate/availability version refs, impactType, detectedAtUtc, resolutionStatus, resolvedBy/At?, replacement refs? | append-only impact history; unresolved impact blocks approve/share/select/book; resolution creates new planning/proposal lineage and never mutates the old line |
| RFQ | id, tenantId, supplierId, brief/plan line refs, requested items, dueAt, status | external send idempotent and authorised |
| SupplierResponse | id, rfqId, terms, rate versions, availability versions, evidence IDs, receivedAt, review state | material changes invalidate affected planning assumptions |
| Approval | id, tenantId, resourceType/id, versionId, decision, creator/responsible actor, approver, mode SELF/INDEPENDENT/policy class, reason?, decidedAt | exact-version append-only decision |
| ApprovalAssignment | id, tenantId, resource/version, assigner, assignee, status, assignedAt/completedAt | optional; independent approval only when deliberately assigned |
| PurchaseOrder | id, tenantId, proposalVersion/option, PO number, objectKey, amount/currency, status, approvedBy/At | exact selected option and signed evidence required |
| Invoice | id, tenantId, proposal/option, PO, invoice number, commercial component totals, VAT, total, status | totals deterministic and reconciled |
| PaymentIntent | id, tenantId, invoice/proposal, methodCode, amount/currency, externalRef?, status, evidence | provider state reconciled before canonical confirmation |
| Booking | id, tenantId, selected proposal/plan line, supplierId, terms, amount, status, confirmedAt?, cancellation | only selected immutable line may be booked |
| Campaign | id, tenantId, briefId, proposal/option, status, start/end, owner, measurement plan | state machine in §39.8 |
| CreativeAsset | id, campaignId, booking/format requirements, objectKey, version, rights, brand review, supplier technical review | publication/delivery only approved version |
| DeliveryProof | id, campaignId, bookingId, type, objectKey/ref, capturedAt, location?, review state | evidence retained and attributable |
| PerformanceEvidence | id, campaignId, metric type, value/unit/period, source/evidence, quality | source/method limitations visible |
| MeasurementReport | id, campaignId, version, input evidence IDs, interpretation, limitations, status | no unsupported causality |

## 40.6 Runtime/audit records

| Entity | Minimum fields | Invariants |
|---|---|---|
| AgentRun | id, tenantId, workflow/agent code, resource ref, status, exact input version, provider policy, correlation, checkpoints, timestamps | durable outside provider process |
| ToolInvocation | id, runId, stepId, toolName/schemaVersion, input hash, attempt/status, result ref | authorised, auditable, replay-aware |
| AIUsageLedger | id, run/step, provider/model, input/output units, currency, incremental cost, cache/reuse state | one record per provider attempt |
| HumanTask | id, tenantId, type, resource/version, assignee, priority, due?, status, action schema | real decision/exception; one clear action |
| AuditEvent | id, tenantId, actor, action, resource ref, correlation, timestamp, outcome, metadata | append-only; no private chain-of-thought |
| OutboxMessage | id, event type, aggregate ref, payload version/data, occurredAt, publishedAt?, attempts | written with committed business state |
| IdempotencyRecord | tenantId, key, command, request hash, response ref, expiry | same key + different request = conflict |

---

# 41. Permissions and Authorisation Contract

Permissions are independent of UI visibility. The Commercial API re-authorises every protected action.

## 41.1 Canonical role codes

`platform_admin`, `internal_planner`, `inventory_ops`, `agency_admin`, `agency_campaign_user`, `advertiser_admin`, `advertiser_approver`, `supplier_user`, `influencer_rep`, `agent_runtime_service`, `worker_service`. The retained `supplier_admin` code is inactive historical reference data and must not be assigned.

## 41.2 Capability matrix

| Capability | Platform/Admin & internal | Agency | Advertiser | Supplier | Service identities |
|---|---|---|---|---|---|
| Tenant/user administration | platform_admin; delegated tenant admins | agency_admin | advertiser_admin | no | none interactively |
| Opportunity create/edit | platform_admin/internal_planner | agency_admin/campaign_user assigned | view assigned | no | runtime reads approved inputs only |
| Evidence register | platform_admin/internal_planner | agency users assigned | supplied through allowed flows | own supplier evidence where workflow permits | workers capture only via allowed tools |
| Evidence review | platform_admin/inventory_ops or governed reviewer | no unless explicitly permissioned | no unless explicit task | own confirmations not equivalent to independent review | no approval |
| Brief create/edit/submit | platform_admin/internal_planner | agency_admin/campaign_user | advertiser flows may submit own supplied brief where enabled | no | worker only bounded automation |
| Brief approve | authorised human with `brief_approve` | permitted agency users where policy grants | advertiser approver/admin if configured | no | never human approval |
| Planning generate/edit | platform_admin/internal_planner | assigned agency users | review/comment/decision surfaces | supply response only | worker may execute bounded deterministic/automation commands |
| Planning approve | authorised human per tenant policy | permitted agency users | advertiser approver where client approval configured | no | bounded automation policy only; never agent self-approval |
| Proposal generate/edit/approve/share | authorised assigned roles | agency assigned roles | view/select/decline; approve where tenant process grants | no | worker may execute pre-authorised inbound OOH send |
| Inventory import/review/publish | platform_admin/inventory_ops may upload, resolve/create unclaimed supplier identity, review, publish, supersede and issue supplier-claim invitations | view/select | view allowed products | invited supplier may claim the existing Supplier ID, then import/manage own inventory; publication review per policy | worker may extract and persist impact proposals but has no independent approval, supplier-claim or publication authority |
| RFQ create/send/review | internal/agency assigned | yes assigned | view where allowed | respond own | worker delivery mechanics only |
| Booking create/request | internal/agency assigned | yes assigned | view/selected-option decision | supplier confirms own | worker mechanics only |
| Funding/PO/invoice/payment | finance-permissioned platform/agency actions as defined | submit/start allowed where permissioned | client evidence/payment interaction where enabled | own payment terms only | adapters reconcile via restricted service identity |
| Creative | internal/agency upload; advertiser brand/legal approval; inventory_ops technical support | assigned | brand/legal approval | supplier technical review own booking | worker processing only |
| Delivery proof | internal review | assigned view/review | view | supplier submit own booking | ingest worker only |
| Measurement | internal/agency generate; authorised review | assigned | view/review where configured | own proof facts only | measurement agent proposes interpretation only |
| Agent operations and AI cost oversight | platform_admin | agency_admin | no | no | runtime/worker write governed usage only; no interactive access |

## 41.3 Self-approval and independent approval

Edit permission and approval permission are separate concepts, but an authorised human may hold both. If the person approving is the same responsible creator/owner, record `SELF`. If a different authorised person approves, record `INDEPENDENT`. Optional assignment creates an independent-review task. No compulsory assignment exists solely to manufacture separation of duties.

## 41.4 Service identity prohibition

`agent_runtime_service` and `worker_service` never receive human approval authority. A worker may execute an external consequence only under an explicit, versioned human-owned bounded automation policy whose readiness guards are enforced independently of agent output.

---

# 42. Commercial API Contract

## 42.1 API-wide rules

- Base path: `/api/v1`.
- JSON UTF-8; OpenAPI generated from implementation and checked against this contract.
- Auth: secure OIDC/session or bearer/service identity adapter.
- Tenant-scoped routes use `/tenants/{tenantId}` and API-resolved membership.
- `X-Correlation-ID` accepted/created and returned.
- `Idempotency-Key` required on consequence-bearing commands.
- ETag/expected version required for mutable/version transitions where concurrency matters.
- Cookie-authenticated unsafe browser methods require CSRF protection.
- Money: integer minor units + ISO currency + explicit VAT/fee components.
- Time: UTC ISO 8601.
- Lists use stable server-side paging/filtering; large inventory never returned as an unbounded payload.
- Errors use `application/problem+json` with stable business-safe code, title/detail, correlation ID and field errors; never expose stack traces/prompts/secrets.
- Long work returns/persists a run resource rather than keeping a browser request open indefinitely.

## 42.2 Required endpoint families

The exact method names may use framework conventions, but the observable resource/command model must cover the following canonical operations.

### Identity/workspaces

- `GET /api/v1/me`
- `GET /api/v1/workspaces`
- `GET /api/v1/tenants/{tenantId}/home`
- tenant/user/membership invite/manage operations under permissioned tenant routes;
- issue, resend and revoke a supplier-claim invitation bound to one permanent Supplier ID;
- accept a supplier-claim invitation through authenticated identity and create/link the permitted supplier membership without recreating the Supplier.

### Opportunity/evidence

- create/list/get/update Opportunity;
- register evidence sources and review evidence items;
- start/inspect interpretation, opportunity-angle, strategy/critic and brief-drafting runs;
- select opportunity angle;
- resolve critic objections;
- submit/approve/reject strategy.

### Brief

- `POST /api/v1/tenants/{tenantId}/briefs:understand`
- `POST /api/v1/tenants/{tenantId}/briefs`
- `POST /api/v1/tenants/{tenantId}/briefs/{briefId}/versions`
- `GET /api/v1/tenants/{tenantId}/briefs/{briefId}`
- `POST /api/v1/tenants/{tenantId}/brief-versions/{versionId}:ready`
- `POST /api/v1/tenants/{tenantId}/brief-versions/{versionId}:submit`
- `POST /api/v1/tenants/{tenantId}/brief-versions/{versionId}:approve`
- `POST /api/v1/tenants/{tenantId}/brief-versions/{versionId}:reject`
- campaign-mode lock/select command bound to exact BriefVersion.

### Planning

- generate/get STP/AudienceDefinitions;
- generate/update/approve MediaMixVersion;
- generate/search/select InventoryShortlistVersion;
- get benchmark/comparable inventory;
- resolve supplier/supply state;
- generate/get/approve MediaPlanVersion;
- resolve critic objections and stale-input blockers.

### Inventory

- create upload/import intent with explicit replacement mode and administrator source authority;
- execute/resume the durable import from an explicit command without continuous source or idle-queue polling;
- get import/candidate/extraction state;
- resolve or merge supplier identity, link an existing Supplier ID, or create one administrator-managed unclaimed Supplier;
- review candidate;
- preview the supplier-release replacement impact before publication;
- publish approved candidates and atomically cut over the supplier release, supersede prior current inventory, soft-delete prior pending work and persist proposal impacts;
- list current and historical supplier inventory releases and supersession lineage;
- list/search/filter current inventory products while preserving permissioned historical access;
- get product detail/rate/availability/assets/evidence;
- get deterministic benchmark/comparables;
- list and resolve proposal inventory impacts through a new planning/proposal version;
- supplier-owned listing create/update/publish/archive within the claimed supplier scope.

### Proposal

- generate/get/update proposal;
- approve/reject exact proposal version;
- render immutable PDF/document;
- share/send through authorised route;
- select/decline option;
- read client decision and expiry state.

### Inbound OOH automation

- configure/read tenant mailbox;
- receive signed provider callback / inbound campaign email;
- list/get message/run state;
- start/process/retry from safe checkpoint;
- expose review-required blockers without sending.

### Marketplace / RFQ / booking

- list/search marketplace supply;
- create/send RFQ;
- supplier submit response;
- buyer review/accept response;
- create booking from selected proposal line;
- request supplier confirmation;
- supplier confirm booking;
- list/get bookings by tenant/authorised scope.

### Funding / commercial finance

- submit/approve purchase order;
- issue invoice;
- start payment/funding route;
- reconcile payment provider/manual evidence;
- read funding readiness for selected proposal/campaign.

### Campaign delivery

- get campaign and delivery state;
- confirm bookings;
- request/upload/review creative;
- approve creative readiness;
- start/complete campaign;
- supplier submit delivery proof; internal review;
- submit/review performance evidence;
- generate/review measurement report.

### Runtime/tasks/audit

- get agent run; resume/cancel where safe;
- list/complete HumanTasks;
- read permissioned AuditEvents and AI cost/usage where role permits;
- health/readiness endpoints for API/worker/runtime without leaking secrets.

## 42.3 Command payload floor

| Command | Minimum input |
|---|---|
| CreateBriefVersion | briefId/baseVersion?, businessProblem, objective, audiences[], geography[], timing, typed budget or explicit unknown, currency, VAT status, constraints[], measurement[], claims/unknowns/assumptions/conflicts, evidence IDs |
| ApprovalCommand | resourceType, resourceId, exact versionId, decision, reason?; actor derived server-side |
| GenerateArtefact | exact inputVersionIds[], policy/provider selector, requestedBy, idempotency key |
| CreateInventoryImport | supplierId?, file name/type/size/hash, document hint? |
| ReviewInventoryCandidate | decision APPROVE/REJECT/EDIT, field patch?, reason codes/notes, expected version |
| SendExternal | exact approved resource/document version, resolved recipient, delivery mode/template, authorisation basis, idempotency key |
| CreateBooking | selected proposal option ID + exact selected plan line/product/rate/availability versions |

---

# 43. Agent Contracts

## 43.1 Common invocation envelope

Every agent invocation includes: tenant, actor/service identity, run/step/correlation IDs, agent code, contract version, prompt/config version, exact canonical input version IDs, approved evidence IDs, locale, policy version, allowed tools, maximum tool calls/steps, timeout, provider/model/cost cap, live-provider permission and prior checkpoint/reuse refs.

## 43.2 Common output envelope

Every agent returns a typed schema with: `status` (`COMPLETED`/`REVIEW_REQUIRED`/`FAILED`), typed artifact, evidence bindings for material claims, unknowns, assumptions, objections, concise business rationale, suggested valid next action and usage metadata. Invalid free text is not canonical output.

## 43.3 Per-agent matrix

| Agent | Required inputs | Typed output | Allowed responsibilities/tools | Forbidden |
|---|---|---|---|---|
| Business Interpretation | approved website/file/brief evidence | business model, products, customers, occasions, geography, unknowns/inferences | read evidence; permitted research/capture requests | invent transaction/demographic facts; approve |
| Opportunity Intelligence | approved evidence + interpretation | ranked opportunity angles with evidence/rationale | retrieval/read evidence | select final angle; approve strategy |
| Strategy | approved evidence/interpretation/selected angle or supplied brief context | StrategyVersion draft | evidence read, approved research, deterministic benchmark summaries | alter facts/money; approve |
| Critic & Readiness | candidate artefact + exact inputs + policy | immutable objections/readiness | read-only artefacts/evidence/policy | edit candidate; silently reduce severity; approve |
| Brief Drafting | approved strategy/evidence or supplied brief understanding | complete BriefVersion draft | canonical read + propose brief | omit material unknowns; overwrite source; approve |
| Audience | approved Brief/Strategy/evidence | STP/AudienceDefinition set | approved evidence + licensed audience source if configured | individual sensitive-attribute inference; unsupported demographic fact |
| Inventory Intelligence | approved brief/STP/mix + verified inventory snapshot | eligible/scored candidate set + reasons | inventory search, product interpretation, eligibility, benchmark, geography/route/POI tools | include hard-ineligible supply as eligible; invent rate/availability |
| Media Planning | approved brief/STP/mix/shortlist/supply | MediaMix or MediaPlan draft | deterministic calculations, inventory/benchmark/forecast reads | change Brief; fabricate reach; approve |
| Proposal Narrative | approved plan + proposal policy | client-ready proposal narrative | read approved facts; request render preview | change totals/inventory/terms; send |
| Creative | approved brief/strategy/plan + brand/product assets + format specs | creative concept/adaptation set | approved assets/specs, concept generation | claim clearance; publish; alter booked format |
| Measurement | verified proof/performance + measurement plan | evidence-backed interpretation/report draft | read verified metrics; deterministic comparisons | invent causality; autonomously reallocate spend |

## 43.4 Agent escalation

An agent returns `REVIEW_REQUIRED` rather than guessing when a material required fact is unknown/conflicting/stale, when policy/tool authority is insufficient, when output cannot validate, when evidence cannot support a material claim, or when a consequential next action needs human/policy authority.

---

# 44. Inventory Channel Schemas and Publication Requirements

## 44.1 Common inventory fields

Every product uses the common commercial schema where applicable:

- supplier and media brand/network identity;
- supplier product code;
- channel and product type;
- sellable name/description;
- geography/location/coverage;
- rate type, amount, currency, VAT basis/status;
- rate validity;
- commission/discount only where explicitly evidenced;
- inclusions/exclusions;
- production/installation/one-off costs;
- minimum order/commitment;
- availability window/status/capacity;
- booking lead time and cancellation terms;
- audience/measurement source, period, methodology and limitations;
- assets/specifications;
- evidence basis, verification, freshness and required action.

## 44.2 Channel extensions

| Channel | Required/important extension fields |
|---|---|
| OOH/DOOH | format, dimensions, structure type, static/digital, illumination, coordinates, road/route/traffic direction, POIs, image, production/installation; DOOH adds loop length, slot length, plays/share-of-loop/screen delivery characteristics |
| Radio | station/network, frequency/coverage, package type, programme/daypart, days, spot length, spots, sponsorship/powerspot, audience source |
| Television | channel/network, programme, daypart, days, spot length/quantity, package/sponsorship, audience source, material deadline |
| Print | publication/edition/frequency, section/placement, size/colour, insertion count, circulation source, material deadline |
| Digital | publisher/platform, placement/format, buying unit, targeting basis, estimated impressions where evidenced, CPM/CPC/fixed basis, creative/tracking specs |
| Social | platform, placement/ad product, buying basis, targeting, expected delivery evidence source, creative/tracking specs |
| Influencer | profile/platform/handle, representation, audience snapshot source, deliverable type/quantity, usage rights, exclusivity, production, rate |
| Experiential | venue/asset, capacity/footprint, duration, staffing/equipment/permits, inclusions/exclusions, production, rate |
| Podcast/audio | show/network, episode placement, host-read/produced, duration, downloads/audience source, targeting, production/usage, rate |
| Retail/transit/mall | network/venue/route, unit type, location, footfall/audience source, dimensions, dwell context, digital loop where relevant, installation, rate |
| Email/mobile | publisher/list owner, format, volume/delivery unit, audience/targeting basis, creative specs, privacy/lawful basis, rate |

## 44.3 Inventory extraction materiality classes

### Tier A — always evidence-bound / commercially consequential

Supplier/product identity, sellable product code where supplied, channel/product type, buying location where it determines purchase, rate amount/currency/VAT/rate basis, validity period, availability, minimum order, production/installation cost, inclusions/exclusions, cancellation/payment/booking terms, supplier confirmation and audience/performance claims used in recommendations.

### Tier B — planning consequential

Dimensions, format, static/digital state, illumination, loop/slot/spot length, daypart/programme/placement, route/road/POI/venue context, technical/creative specifications, material deadlines and other fields that determine eligibility or deliverability.

### Tier C — descriptive

Marketing copy, non-decision-critical description and decorative metadata. Source may be retained without expensive field-by-field verification unless later promoted into a recommendation claim.

Materiality policy may version these classes, but AI does not decide them ad hoc.

## 44.4 Publication dispositions

| Condition | Default disposition |
|---|---|
| Supplier/product identity unresolved | block affected product publish |
| Missing amount/currency/rate basis where product is presented as priced | block priced publish; may publish explicitly unpriced only if product policy allows |
| VAT unknown where commercial comparison/client pricing requires it | review/block commercial use |
| OOH/DOOH required coordinates missing/invalid | block geography-dependent publish/planning until resolved, unless explicit non-coordinate product policy exists |
| No availability exception supplied | product is planning-available; absence/stale supplier response does not block matching or proposal; booking remains separately confirmed |
| Overlapping not-available/blackout/confirmed booking conflict | reject inventory for the affected requested dates |
| Missing asset/logo/photo | visible review task; does not automatically block commercial record unless channel/use requires asset |
| Unsupported audience/measurement claim | exclude claim from verified client promise; product may publish if core product fields valid |
| Material terms missing/conflicting | review required; booking/proposal consequence may block |
| Possible duplicate / identity uncertain | **do not merge by default**; retain separate candidate until same-identity evidence is sufficient |

## 44.5 False-merge asymmetry

A false merge can corrupt two products' commercial history and is more damaging than a recoverable duplicate. Therefore, when identity evidence is genuinely insufficient, Advertified prefers separate candidates/records and a review task over automatic merge.

## 44.6 Declarative mapping boundary

Allowed example: a supplier mapping that says `"Site No." -> supplierProductCode`, `"4 Weekly Rate" -> publishedRate`, `"Lat" -> latitude`, `"Long" -> longitude` and then sends values through the same generic normalization, validation, evidence and publication rules.

Not allowed: supplier-name-specific code that changes commercial semantics, bypasses standard validation, invents pricing rules, changes approval requirements or creates a separate product model. Supplier-specific vocabulary/shape configuration is acceptable; supplier-specific business truth is not.

## 44.7 Adaptive extraction cost

Extraction fidelity and AI spend scale with novelty, ambiguity, materiality and change—not simply file size.

- first-time/structurally novel/messy material: full render/layout reconstruction, stronger extraction and review;
- known stable template: validate structural fingerprint, deterministic/declarative mapping where safe;
- revision/change file: identify changed regions/rows and re-process material changes rather than re-running expensive AI over unchanged thousands of rows;
- AI only where semantic interpretation is necessary;
- deterministic normalization/calculation/validation whenever equivalent quality can be achieved without model inference.

---

# 45. Commercial Calculation Contract

Money calculations are deterministic and reproducible. AI may explain them but never performs authoritative arithmetic.

## 45.1 Money representation

All stored monetary values use integer minor units and ISO currency. Never use binary floating-point for authoritative money arithmetic.

## 45.2 Line-level components

For each selected media line, retain separately where applicable:

- supplier/list/quoted/negotiated/basis rate reference;
- quantity/duration/buying unit;
- supplier media cost;
- evidenced supplier discount or negotiated change;
- production cost;
- installation cost;
- other explicit pass-through costs;
- disclosed markup/service/management/platform fee if policy applies;
- agency commission if policy/contract applies;
- taxable/non-taxable classification per component;
- VAT amount;
- client line total.

## 45.3 Calculation order

1. Select the exact evidenced rate state appropriate to the transaction (`Quoted`/`Negotiated`/other governed source). Do not infer a discount from a benchmark.
2. Apply quantity/duration only according to the governed rate basis and exact commercial terms.
3. Apply explicitly evidenced supplier discounts/negotiation terms before any percentage-based commission/fee whose policy basis is defined as net media cost.
4. Add separately evidenced production, installation and other pass-through costs.
5. Apply disclosed markup/management/platform/service fee only to the configured, visible fee basis. The basis and percentage/fixed amount are account-policy data and must be shown to privileged users.
6. Apply agency commission only according to the tenant/client contract policy; do not infer that commission applies.
7. Calculate VAT per taxable component using the governed VAT rate/status effective for the transaction. Current ZAR VAT-registered default is 15% only where the governed policy applies; exemptions/not-applicable components remain zero-rated in the calculation according to their actual legal treatment.
8. Reconcile option subtotal, VAT and total as the sum of line/components. No hidden residual/margin bucket is permitted.

## 45.4 Explicit formulas

For a component with percentage fee `p` stored as a decimal and base `B` minor units:

`feeMinor = RoundAccordingToPolicy(B * p)`.

`taxableSubtotalMinor = Sum(taxable component minor amounts)`.

`vatMinor = RoundAccordingToPolicy(taxableSubtotalMinor * applicableVatRate)` unless tax is calculated per line/component by accounting policy, in which case the system must use that one governed method consistently and retain the rounding policy.

`clientTotalMinor = Sum(all pre-VAT client components) + vatMinor`.

Proposal option total equals the exact sum of included plan lines/components. Invoice total must reconcile to the exact selected proposal option/approved changes; booking cannot silently substitute another price/site/date.

## 45.5 Price states are not overwritten

Published/List, Quoted, Negotiated and Accepted/Booked values remain separate records/states with sources and dates. Benchmark/historical comparable intelligence is never used as a transaction price unless a supplier/human-authorised commercial process establishes a real quote/rate.

---

# 46. Screen and Interaction Contracts

Every screen has one recognisable business outcome, one dominant next action when action is required, complete data/error states, and API-enforced permissions.

## 46.1 Universal screen states

Each relevant screen must implement: loading, empty, normal, validation error, permission/forbidden, partial/stale data, service failure, recoverable failure, and success/next action. Hidden required fields may not block progress without being surfaced to the user.

## 46.2 Public product surfaces

| Surface | Purpose | Primary action |
|---|---|---|
| Home | explain Advertified outcome/value and real media-network proof | See how it works / start relevant journey |
| How it works | explain brief-to-measurement journey | Start campaign |
| Solutions/channel pages | explain channel roles without pretending every channel has equal execution maturity | Start campaign / explore media |
| Media network/partners | show published media network truth without fabricated counts | Explore channel/partner |
| Packages/investment bands | explain configurable investment bands | Start campaign |
| Advertise Now, Pay Later | explain funding/referral route without promising approval | Start/learn funding route |
| Register | advertiser/agency/media owner/creator onboarding | Register appropriate participant type |
| Contact/FAQ/legal | support and lawful public information | contextual action |

## 46.3 Authenticated core surfaces

| Surface | Must show | Primary action |
|---|---|---|
| Home | role-relevant KPIs/tasks backed by canonical data | highest-priority real task |
| New Brief | paste/type/upload source; minimal admin before interpretation | Understand brief |
| Brief | source, structured version, claims/unknowns/conflicts, version changes | approve/self-approve/request change/continue |
| Opportunity | sources, interpretation, angles, strategy/readiness | complete current decision |
| Strategy/STP | evidence-backed audience/positioning and objections | approve/request change |
| Media Mix | channel role, allocations, flighting, editable bars/timeline | approve/update mix |
| Inventory | scalable search/filter/grouping | select/open product |
| Inventory Product | specifications, evidence, rate history, availability, assets, benchmark | select/view comparables/edit if authorised |
| Import Review | source render beside extracted candidate, supplier identity/claim state, material issues, corrections and full-replacement impact preview | resolve supplier/exception, then publish replacement |
| Shortlist | selected and rejected inventory with reasons | confirm/change shortlist |
| Supply | supplier responses, current/stale rates and availability | resolve supply |
| Media Plan | line items, running periods, totals, forecast, evidence, objections | approve/change plan |
| Proposal | distinct options, exact totals/terms, document preview, approval mode and visible supplier-inventory supersession impacts | approve/share/select only when current; resolve affected inventory through a new version |
| OOH Inbox | mailbox status, messages, checkpoints, review blockers, send result | open exception / safe retry |
| Supplier | permanent identity, claim/onboarding state, contacts, inventory releases, current/expired counts, invitations and audit history | issue/revoke invitation, inspect release or manage claimed supplier as authorised |
| Marketplace/Supplier requests | listings/RFQs/responses/booking state | appropriate create/respond/review action |
| Funding | selected option, PO, invoice/payment/funding state | submit/approve/reconcile permitted action |
| Campaign | bookings, creative, delivery/proof/measurement rail | current delivery action |
| Tasks | only actual assigned decisions/exceptions | complete task |
| Agent Operations | persisted run/tool/checkpoint/failure/cost data | safe resume/open exception |
| Commercial Policy | fees/VAT/approval/automation/freshness settings | save new policy version |
| Audit | permissioned immutable business/AI events | inspect/filter only |

## 46.4 Human-facing wording

Never expose implementation terms such as schema, payload, canonical aggregate, worker lease, provider prompt, migration or browser boundary to ordinary users. Explain what happened to the business action, why it matters and what the person can do next. Correlation IDs may appear under technical/support details.

---

# 47. Error, Event and Recovery Contract

## 47.1 Stable error classes

At minimum the platform must distinguish and expose stable machine-readable errors for:

- `TENANT_FORBIDDEN` / permission denied;
- `VERSION_CONFLICT`;
- `EVIDENCE_REQUIRED`;
- `MATERIAL_CONFIRMATION_REQUIRED`;
- `ARTIFACT_STALE`;
- `RATE_STALE` / `RATE_EXPIRED`;
- `AVAILABILITY_UNKNOWN` / supply not confirmed;
- `INVENTORY_NOT_ELIGIBLE` with rejection reason;
- `CAMPAIGN_MODE_CONFLICT`;
- `APPROVAL_REQUIRED` / approval policy failure;
- `IDEMPOTENCY_CONFLICT`;
- `COST_POLICY_BLOCKED`;
- `AGENT_OUTPUT_INVALID`;
- `AGENT_RUNTIME_UNAVAILABLE`;
- `INPUT_VERSION_DRIFT`;
- `RUN_NOT_RESUMABLE`;
- `INVALID_PROVIDER_SIGNATURE`;
- `DELIVERY_AMBIGUOUS`;
- `FILE_UNSAFE` / unsupported file;
- `SUPPLIER_IDENTITY_AMBIGUOUS` / permanent Supplier ID requires administrator resolution;
- `SUPPLIER_INVITATION_INVALID` / expired, revoked, already-used or wrong-supplier claim link;
- `INVENTORY_SUPERSEDED` / the referenced supplier release is no longer current;
- `PROPOSAL_INVENTORY_REVIEW_REQUIRED` / a pending proposal references superseded supplier inventory;
- `VALIDATION_FAILED` with field errors.

Errors never masquerade as successful business output.

## 47.2 Canonical event classes

Committed events include, as applicable:

`EvidenceApproved`, `StrategyApproved`, `BriefApproved`, `CampaignModeSelected`, `MediaMixApproved`, `SupplierCreatedFromInventory`, `SupplierClaimInvitationIssued/Revoked/Accepted`, `InventoryRateChanged`, `AvailabilityChanged`, `InventoryPublished`, `SupplierInventoryReleaseSuperseded`, `ProposalInventoryReviewRequired/Resolved`, `InventoryShortlistSelected`, `MediaPlanApproved`, `ProposalApproved`, `ProposalShared`, `ProposalOptionSelected`, `SupplierResponseReceived`, `BookingConfirmed`, `PurchaseOrderApproved`, `PaymentConfirmed`, `CampaignBookingsConfirmed`, `CreativeApproved`, `CampaignStarted`, `CampaignCompleted`, `DeliveryProofSubmitted/Reviewed`, `PerformanceEvidenceReviewed`, `MeasurementReportReviewed`, `AgentRunReviewRequired`, `AgentRunCompleted`.

Consumers use events to create tasks/notifications or advance eligible work; event delivery does not bypass current-state guards.

## 47.3 Retry rules

- retry automatically only safe, classified transient operations;
- never blindly retry a provider/external action after acceptance is ambiguous;
- reconcile provider request/event IDs before retry;
- persist checkpoint before the next side effect;
- poison/terminal work becomes visible review/dead-letter state, not an infinite loop;
- cancellation prevents future work but never pretends an already completed external effect was undone.

---

# 48. Acceptance Journeys — Build Cannot Be Claimed Complete Without These

These journeys are behavioural contracts, not optional demos.

## E2E-01 Tenant isolation

Create two tenants with similarly shaped resources. Browser/API/background/tool attempts to read/write/enumerate the other tenant must fail without leaking resource existence and must create appropriate security audit evidence.

## E2E-02 Supplied Brief to proposal

Paste/upload a genuine brief → preserve source → understand/classify claims/unknowns → create/approve exact BriefVersion → STP → media mix → inventory/supply → plan → proposal options → approval → branded document. No client CRUD prerequisite and no invented material fact.

## E2E-03 Full multi-channel campaign

Approved full-campaign brief → STP → multi-channel rationale/allocation → eligible supply → plan → proposal → selected option → funding/booking readiness. Each channel has explicit role; user can edit mix/periods; material changes stale downstream artefacts.

## E2E-04 OOH-only interactive

OOH requirement → mode locked `OOH_ONLY` → STP including geography/routes/POIs → only OOH/DOOH mix → verified eligible inventory → benchmark/supply → plan/proposal. Attempt to add radio/TV/etc. is rejected and requires a new CampaignBrief.

## E2E-05 Inbound OOH straight-through

Signed inbound email with complete material fields → immutable source → OOH_ONLY → STP/mix/inventory/supply/plan/proposal/document → exactly-once delivery with no per-message human action where bounded policy passes. Incomplete/non-OOH/conflicting/stale cases send nothing and enter `REVIEW_REQUIRED`.

## E2E-06 Unseen admin inventory file and unclaimed supplier

Authorised administrator uploads a held-out supplier PDF/XLSX/CSV/etc. → protect/classify/render → command-triggered durable extraction with no idle inventory polling → reconstruct layout/tables/assets → extract raw/normalized values and supplier identity with locators → deterministic validation → link an existing Supplier ID or create one administrator-managed `UNCLAIMED` Supplier → exception-led review → publish one current supplier inventory release → searchable/detail/benchmark ready. Supplier credentials are not required for publication, and no supplier-name code is added for the case. The administrator then issues a one-time supplier claim invitation; after authenticated acceptance, the supplier sees the already-published inventory under the same permanent Supplier ID and may upload future inventory within that scope.

## E2E-07 Pricing conflict/comparison

Provide old list rate, newer quote, negotiated historical price and structurally different bundle. System preserves all states and returns one of `resolved automatically`, `comparable`, or `conflict requiring resolution`; no silent overwrite or fake equivalence.

## E2E-08 Supplier marketplace

Supplier manages own listing/rate/availability → buyer creates RFQ → supplier response with evidence → buyer accepts/replans → booking request → supplier confirmation. Cross-supplier/tenant access denied.

## E2E-09 Approval modes

Authorised creator self-approves exact version in one action and audit shows `SELF`. Separate user can optionally be assigned and approval shows `INDEPENDENT`. AI/service cannot use either human mode. Material revision requires new approval.

## E2E-10 Campaign delivery

Selected option → PO/funding/payment readiness as applicable → booking(s) confirmed → format-specific creative uploaded/reviewed → campaign READY/LIVE → supplier proof submitted/reviewed → performance evidence → measurement report. Each state reflects real evidence.

## E2E-11 Failure/resume/idempotency

Interrupt worker/runtime/provider around every consequential checkpoint. Restart/resume produces no duplicate canonical record, email, booking, invoice/payment action or paid model attempt. Ambiguous external acceptance enters reconciliation/review.

## E2E-12 Stale commercial input

Change a selected rate or availability after plan/proposal creation. Affected draft/current downstream artefact becomes stale or blocked; user sees impact and must recalculate/reconfirm before approval/send/booking.

## E2E-13 Large catalogue

10,000+ inventory products remain searchable/filterable/grouped via server-side paging/queries; browser does not load full catalogue; no N+1 degradation across common list/detail flows.

## E2E-14 Role experiences

Each human role sees only authorised navigation/data/actions, with correct home/tasks and deep-link/API protection. User-facing copy is commercial and does not reveal internals.

## E2E-15 Anti-prompt-injection

Malicious text embedded in brief/email/PDF/web evidence attempts to override instructions/send/book/change tool permissions. It is retained only as untrusted content and cannot change system/tool/policy authority.

## E2E-16 Supplier inventory replacement and proposal impact

Publish supplier release A and use one of its exact product/rate versions in draft, sent and selected-but-unbooked proposals → start replacement upload B and prove release A remains current while B is extracting, failed or review-blocked → successfully publish B → atomically make B current, mark all A inventory expired/superseded, cancel/supersede and soft-delete older pending imports/candidates/listings, and retain all historical evidence → mark every uncommitted affected proposal and line `INVENTORY_REVIEW_REQUIRED` with a visible old/replacement comparison → reject stale client acceptance and booking conversion → require authorised replacement/removal/re-pricing through new planning/proposal versions. A confirmed booking and completed historical proposal using release A remain unchanged and readable.

---

# 49. Production Deployment and Release Contract

## 49.1 Required deployment properties

The exact AWS compute/database topology is governed policy, but an implementation is not production-ready unless the selected topology provides:

- TLS/DNS and secure ingress;
- isolated web/API/runtime/worker responsibilities;
- PostgreSQL/PostGIS/pgvector canonical datastore;
- private/restricted database and object-storage access;
- S3-compatible versioned private object storage;
- managed secret injection and per-service least privilege;
- durable queue/outbox/worker processing for long work;
- health/readiness endpoints;
- central structured logs/metrics/traces with correlation IDs;
- backup/PITR appropriate to selected database topology;
- tested restore and rollback/compensating process;
- migration procedure using backward-compatible/expand-contract discipline where needed;
- rate limits, upload limits and bounded AI/tool execution;
- build provenance/SBOM/dependency and secret scans;
- staging or production-like pre-release verification;
- production smoke checks and named incident owner.

## 49.2 Initial deployment decision record

Before first production deploy, the owner must record directly in this section (or a versioned governed setting referenced here) the selected compute, database, ingress, object storage, queue/event, secret, telemetry, backup/RPO/RTO and DNS/TLS topology. This is the one remaining environment-specific choice that cannot be honestly universalised in the product specification. Once selected, it is Policy and implementation must match until deliberately changed.

### 49.2.1 Inventory production-readiness decision and work packet — 2026-09-02

The repository owner approved the availability, controlled automation, spatial matching,
Bedrock embedding, asset-rights, extraction-acceptance and exact LSM/SEM policies recorded in
Sections 10, 11 and 13.4. The authorised local work packet is one coordinated implementation
batch that completes those software controls plus both OOH_ONLY and FULL_CAMPAIGN navigation
and acceptance journeys. Acceptance evidence is the synchronized specification/contracts/
master data, forward-safe disposable-database migrations, affected and full API/agent/web/
architecture checks, and connected journeys where the development environment supports them.

One staging Bedrock embedding smoke test/backfill is authorised up to USD 3 after credentials
are configured. Production infrastructure provisioning, an independent security review,
production provider calls and production deployment remain separate explicit go-live gates and
are not authorised by this record.

### 49.2.2 Agency-admin Agent Operations visibility work packet — 2026-09-03

The repository owner requires Agency Admin users to see agent budgets and costs in the local
product. The authorised local work packet is a read-only Agent Operations surface inside the
governed Settings area. It exposes the closed agent roster, active provider/model policy,
current per-run cost caps, tenant-attributable recorded AI usage/cost and durable run exceptions.
It does not authorise budget edits, live provider use or spend. Acceptance evidence is an
Agency Admin browser journey, tenant/role API authorization checks, deterministic zero-cost
local state, affected builds/tests and architecture checks.

Retained local evidence on 2026-09-03: `docker build --file api/Dockerfile --target
build --tag advertified/agent-operations-build:local .` passed on SDK `10.0.400`;
the pinned-SDK `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj
--filter "FullyQualifiedName~AgentOperationsAcceptanceTests|FullyQualifiedName~OpenApiContractTests"`
run passed 3/3; `npm run type-check`, `npm run lint`, `npm test` and `npm run build`
passed; the affected Playwright Settings and shell run passed 12/12 across desktop and
compact projects; and `python -m pytest tests/architecture -q` passed 42/42. An authenticated
local-stack smoke check returned role `agency_admin`, 11 agents, provider `deterministic`,
live provider disabled and total recorded cost zero.

### 49.2.3 Confidential inventory corpus ingestion and certification work packet — 2026-09-03

The repository owner supplied a read-only local corpus that was independently enumerated before
content processing as 43 files totalling 311,080,670 bytes: 33 PDF, eight PPTX and two XLSX
documents. The authorised local work packet may stream those originals once through the existing
production inventory-ingestion code path using local Docling and deterministic validation, retain
hash-addressed extraction artefacts outside version control and Docker build contexts, and reserve
an untouched deterministic holdout of at least 20%. Expected paid-AI cost is USD 0.00; no live or
paid provider call is authorised. Every extraction stops at human review and must not be published
without the existing approval process. Acceptance evidence is an exact source manifest with
SHA-256 hashes, unchanged-source replay prevention, one retained extraction artefact per processed
source, full-corpus and held-out evaluator results against independently human-authored gold data,
affected tests and architecture checks. Until the gold data exists and the held-out thresholds in
Section 11.21 pass, extraction certification and production readiness remain blocked.

Initial retained local evidence on 2026-09-03: dataset
`inventory-corpus-2026-09-03-53783bb0a017` records 43 unique SHA-256 source hashes,
311,080,670 bytes and a deterministic 34-document training / nine-document holdout split;
cached preparation revalidated file count, size and modification time without rehashing unchanged
sources. Expected and actual paid-AI cost were USD 0.00. The production API accepted ten
training imports into ten single protected objects with no clean-file quarantine duplicates; one
completed local Docling extraction reached `REVIEW_REQUIRED` and has a preserved hash-addressed
observed artefact. Real-corpus execution exposed an unresolved durability defect: pinned local
Docling tasks can exceed one hour, while the API does not yet persist the external task identifier
for recovery after the initiating request ends. Processing stopped with nine imports safely
`UPLOADED`; the nine-document holdout remained untouched. That initial checkpoint did not prove
full-corpus extraction or certification. The authorised durability recovery and final corpus
outcome are recorded in Section 49.2.4; no approval or publication occurred at this checkpoint.

### 49.2.4 Docling extraction durability repair work packet — 2026-09-03

The repository owner classified the interrupted Docling work as a production-blocking durability
defect and authorised one cohesive local repair batch. No ambiguous task may be restarted,
resubmitted or duplicated, and the remaining 43-file corpus remains paused until the migration,
durable task-ID capture, restart/resume, timeout/reconciliation and exactly-once canonical-effect
guards are proven. The batch adds forward-safe attempt persistence, leased worker orchestration,
explicit authorised retry/cancel/reconciliation commands and operator-visible attempt history.
Only focused state-machine and restart/database integration evidence is required in this batch;
full governed-suite and corpus execution remain later system-candidate work. The existing Docling
container, Compose project, volumes and read-only source corpus must be reused, and paid AI spend
remains forbidden.

The owner subsequently authorised terminal reconciliation of the nine pre-durability attempts.
Each original attempt is retained as `CANCELLED / CANCELLED_BY_OPERATOR / OPERATOR_CANCELLED`,
with its source hash, created/submitted timestamps and reconciliation/cancellation audit events,
zero external task identifiers and zero accepted artefacts. The exact retained reason is:
"Pre-durability Docling attempt has no recoverable external task ID. Provider acceptance and
cancellation cannot be established. No artifact is accepted. Any late result is permanently
fenced from canonical state." The ten-minute zero-activity observation is supporting evidence
only and is not represented as proof that an old provider task never existed. All nine affected
imports have a fresh later attempt bound to the same source version/hash and a later accepted
artefact; none is represented as a resumed, successful or provider-cancelled legacy attempt.

Local migrations `202609030042_InventoryExtractionDurability` through
`202609030047_InventoryCandidatePagingIndex` applied in sequence. Database guards and focused
tests reject an old, non-running, non-latest, mismatched or lease-less result before it can create
an artefact, update an import, create/change inventory, consume an accepted-attempt slot or
overwrite a newer attempt. Restart probes retained provider task identifiers and resumed polling;
known provider task loss became explicit terminal `task_not_found` evidence before a fresh linked
attempt was requested. The existing Docling image and project were reused with one worker,
single-item OCR/layout/table batches, bounded API concurrency of one and no embedded image
payloads. The previously OOM-affected JCDecaux source completed on fresh attempt 2 without a
container restart. A real alias-collision result initially rolled back atomically, then completed
on fresh attempt 2 after canonical value/evidence normalization was repaired.

Final corpus evidence for dataset `inventory-corpus-2026-09-03-53783bb0a017` accounts for all 43
unchanged sources and all 43 imports at `REVIEW_REQUIRED`: 43 accepted artefacts, 43 distinct
imports, 43 distinct source hashes, 43 distinct source-version acceptance slots and zero duplicate
accepted slots. Extraction produced 4,902 review candidates and 4,902 pending human-review tasks;
zero products were published. The replay check revalidated count, size, modification time and all
43 retained artefact hashes, then skipped every unchanged successful source. Both full and
nine-document holdout evaluator invocations stopped with the exact prerequisite failure "The
corpus must cover PDF, spreadsheet, presentation, scan and image." Governed scan/image labels,
per-format release modes and independent non-empty gold/observed cell mappings remain unresolved
human inputs, so extraction certification and production readiness remain blocked without invented
scores.

The pinned-SDK durability/Docling filter passed 24/24, alias-collision inventory acceptance passed
2/2, the final full Commercial API suite passed 173/173 on SDK `10.0.400`, corpus-tool tests passed
10/10 and architecture tests passed 42/42. Web type-check, lint, 6/6 unit tests and production build
passed; the connected Agency Admin Agent Operations UAT passed 1/1 with 11 visible agents, zero
recorded cost and paid AI disabled. Both normal and recovery Compose configurations validated, and
the existing `advertified-dev-web-1` remained healthy on port 3017 after its earlier image-only
replacement. No second web container, alternate port, full-stack restart or unchanged-image rebuild
was used. The embedding preflight found two active local products; at two full 8,192-token model
inputs and USD 0.02 per million input tokens, its conservative ceiling was USD 0.00032768. No
embedding job or paid-AI call was enabled, and total actual paid-AI cost remained USD 0.00.

### 49.2.5 Inventory semantic recovery and publication-readiness work packet — 2026-09-03

The repository owner determined that the durable 43-file run is transport-complete but not
commercially extracted: the 4,902 retained rows require a format-aware semantic recovery before
human review or publication. The authorised local work packet preserves every original source,
extraction artefact, candidate, hash and failure observation; adds native XLSX sheet/cell/formula/
format/merged-header handling and native PPTX slide/text/table/image/spatial handling; reconstructs
PDF headings, multi-row tables, footnotes, packages and deliverables from retained Docling output;
and reruns Docling only for an individual source whose retained result is proven insufficient.

Deterministic parsing and validation run before a governed Bedrock inventory-intelligence pass.
Only necessary structured facts and bounded evidence excerpts may leave the canonical boundary.
Bedrock may classify media/offering types, propose source-linked mappings and aliases, interpret
descriptions and commercial terms, identify ambiguity and draft searchable descriptions. It may
not invent a missing supplier, price, availability, geography, date or term; perform authoritative
commercial arithmetic; approve a candidate; or publish inventory. Outputs are immutable proposals,
cached by input/source hash, tenant-scoped, replay-safe, costed and stopped before the shared USD 5
evaluation/certification ceiling. The complete intended paid batch is costed before activation.

Acceptance starts with direct field-level comparison of `DMS Digital Rate Card .xlsx`, `Reveel -
ZA - Publisher Media Kit.pptx`, `SABC May 2026 TV Rates (1).pdf` and `Algoa FM - Algoa Club Package
- Plan A - Generic & Sponsorship -2026.pdf`; these are only the calibration set, not the quality
scope. All 43 source files remain failed semantic extractions until each regenerated file result
passes source-file accounting, field coverage, hierarchy, commercial-term and exact-evidence
checks. The gate requires source-linked suppliers, standalone products, packages, sponsorships/
Powerboosts, deliverables, prices/currency/VAT/buying basis, validity, geography, explicit
availability exceptions, conditions and assets as applicable, with non-product headings and
footnotes retained as evidence but excluded from product counts. Only after the calibration set
passes may unchanged retained artefacts be re-normalised in a bounded batch, followed by a
file-by-file exception report and corpus reconciliation. Human-authored gold and exception-first
authorised approval remain mandatory; no AI or service identity may approve or publish. After
authorised publication, unchanged canonical products are embedded once by content hash and must be
retrievable in both OOH_ONLY and FULL_CAMPAIGN proposal journeys. No production deployment, push,
external communication, supplier commitment or persistent-volume deletion is authorised.

The semantic recovery implementation now includes source-hash-verified retained reprojection,
format-aware Office extraction, bounded multimodal packets, tenant- and budget-scoped semantic run
records, provider `CountTokens` enforcement where supported with a conservative local fallback,
one-attempt ambiguity quarantine,
proposal-only grounded output, review-safe supersession and a permission-scoped no-call preflight
that reports every retained source, packet, image, blocker and conservative cost before activation.
Migrations `048` through `050` define semantic run durability, reprojection lineage and idempotency.
The paid DStv profile, when explicitly authorised and re-enabled, uses
`us.amazon.nova-pro-v1:0`, records configured prices of USD 0.80 per million input tokens and USD
3.20 per million output tokens, caps each request at USD 0.06 and caps transcription and enrichment
together at USD 0.25 under budget scope `inventory-dstv-certification-2026-09-04`. Transcription may
reserve at most half of that shared scope, the provider policy permits one attempt, every packet is
token-counted before inference and enrichment does not resend Office images. A read-only audit on
2026-09-04 found no ledgered runs in the new DStv scope. The preceding
`inventory-corpus-2026-09-03` scope contained 11 runs with USD 0.056574 actual cost and USD 0.171362
committed or reserved under the store's completed-versus-maximum accounting rule. The runtime log
also recorded three direct DStv calls outside that ledger: two HTTP 503 responses and one HTTP 200
response. The retained successful response cost USD 0.011548. The failed responses' usage was
overwritten before audit, so confirmed actual local spend is at least USD 0.068122 and the strict
per-call-capped upper bound is USD 0.188122. The direct-call helper is disabled, the API is stopped,
the old live runtime container and its temporary patch helpers were removed, and the checked-in
corpus override is now deterministic with semantic processing and live calls disabled, zero live
cost caps and no AWS credentials mount. Certification must use the governed API preflight and
reprojection path after an explicit, recorded reactivation.

The latest local verification compiled the Release API with zero warnings and zero errors, passed
the complete agent-runtime suite 49/49, passed the non-infrastructure API suite 142/142, passed the
targeted inventory persistence suite 5/5, passed master-data migration coverage 2/2, passed the
extraction architecture and corpus tooling set 30/30, and passed the web production build, lint and
unit tests 6/6. The original 43 source files and current corpus data were not changed and no Docker
image was built. The verification sequence itself made no Bedrock request; the audited costs above
came from earlier or concurrently initiated certification attempts.

Application of migrations `048`–`050`, replacement of the existing corpus API/runtime, execution of
the no-call cost report and all semantic reprojections remain operationally blocked because the
connected Docker workspace forces Compose project `advertified-dev`, while the durable corpus is in
`advertified-os2-dev`. Running the connector's current `up`, `start` or `restart` path would mutate
the wrong stack. Production certification therefore remains NO-GO until the connector targets the
existing corpus project, the in-place update and preflight succeed, paid execution is separately
authorised, all 43 results receive source-level quality evaluation, and authorised humans review
and publish acceptable candidates.

### 49.2.6 DMS deterministic source certification — 2026-09-04

The DMS calibration workbook is now certified through the restored `advertified-os2-dev` stack.
The final implementation removes Bedrock from source transcription entirely. Hash-bound XLSX/PPTX
embedded images are submitted one at a time to the local Docling synchronous endpoint, preventing a
stale asynchronous queue from leaving a small source image indefinitely pending. The deterministic
projection repairs OCR-specific multi-row headers only when source geometry supports the repair,
including the merged `Platform`/`Ad Unit` heading and the empty Width plus combined Height value
observed in this workbook. It reconstructs the four physical commercial rows, preserves their
source-image and local-OCR coordinates, normalises `16 9` to `16 x 9`, reconstructs `DStv Media
Sales` from the visible rate-card title, and attaches only visibly extracted DStv positioning copy
to DStv rows. Exact source-name mappings derive the governed `DIGITAL` and `DIGITAL_PLACEMENT`
codes under human-review policy. A visible `R` prefix supports ZAR parsing, while `R1,10` remains
raw, has no normalised amount and carries `AMBIGUOUS_TRUNCATED_RATE`. No buying basis or commercial
validity date is inferred.

Retained reprojection attempt 16 completed successfully under provider version
`advertified-embedded-image-docling/1.2.0`. The import returned to `REVIEW_REQUIRED`, its stale
failure code was cleared, the four earlier incorrect unreviewed candidates were superseded, and four
new source-linked candidates were created. Human-authored file-gold evaluation reported 4 expected,
4 observed, zero failures and `PASS`. Nothing was approved or published. The restored agent runtime
reported `deterministic-zero-cost`; live Bedrock execution was false and the active explicit
certification scope retained USD 0 committed cost. No paid inference was used for the successful
local repair. Using the previously reported USD 0.05743146 workbook-evaluation baseline plus the
three bounded certification calls totalling USD 0.033503, known reported cumulative Bedrock spend
for this investigation is approximately USD 0.09093446.

The final focused verification passed the API build with zero warnings and zero errors, the complete
Docling/Office extraction class 22/22, the agent-runtime suite 50/50, and file-gold/corpus/
architecture checks 32/32. Web, API, runtime, Docling, PostgreSQL, MinIO, Redis, ClamAV and Mailhog
were healthy in the single `advertified-os2-dev` project. Temporary `advertified-dev` containers,
networks and volumes were removed; unused build cache fell from 4.161 GB to 2.623 GB and local
volumes from 972.2 MB to 764.5 MB, with zero volume bytes reported reclaimable afterward.

This certifies the DMS XLSX calibration file only. The other calibration files and the remaining
42 corpus documents still require the same physical-file evaluation before corpus-wide extraction
or production publication can be declared ready.

### 49.2.7 Single local Docker workspace work packet — 2026-09-04

The repository owner requires every local Docker workflow to reuse the one existing Compose
project named exactly `advertified-os2-dev`. This is one multi-service application stack, not one
physical service container. Local scripts and Compose overlays must not create an alternate
Advertified project, parallel service container, one-off migration container or separately named
build project. A changed application image may replace only its existing named service container;
unchanged services and persistent volumes remain in place. Repository scripts must not perform a
global Docker image prune because unrelated Docker state is outside Advertified's authority.

The bounded implementation work packet centralises the exact project-name guard, makes local
PowerShell Compose calls pass that name explicitly, resolves service containers through Compose,
uses the retained migrator service instead of `compose run`, and aligns remaining local tool
defaults. Each explicit image build is followed by `up --no-build` so Compose cannot trigger a
second build. The guard refuses to bootstrap a missing stack and rejects duplicate instances of any
service. Acceptance evidence is all Compose files resolving to `advertified-os2-dev`, a read-only
Docker label audit finding no alternate Advertified project, Compose configuration validation, and
an architecture check that rejects aliases, one-off migration runs and global image pruning. This
work does not authorise removal of containers, volumes or images, nor any production operation.

Retained local evidence on 2026-09-04: all base, application, corpus-override and build-only
Compose configurations passed `docker compose ... config --quiet`; the read-only project guard
found only `advertified-os2-dev`, found exactly one API service container and left Docker state
unchanged; all PowerShell files parsed without errors; the focused application-packaging suite
passed 13/13; and `git diff --check` passed. The complete architecture run passed 41/44 checks.
Its three unrelated failures remain the pre-existing 507-line
`NativePresentationSiteProjection.cs`, governed literals in
`DoclingInventoryProjection.PageCards.cs`, and over-limit functions in the in-progress agent
runtime/provider tests; this Docker work does not represent those checks as passed.

### 49.2.8 Production-neutral product language work packet — 2026-09-04

The repository owner requires Advertified to present as one production-intent product rather than
a development demonstration. Development-only identities, deterministic providers, fixtures and
loopback endpoints remain valid internal safety mechanisms inside their explicit environment
boundaries, but ordinary product copy and human-safe API responses must describe business state
without exposing `local`, `development`, `fixture`, `mock` or `deterministic` implementation terms.
Geographic uses such as local-market comparisons are not environment terminology and remain valid.

The bounded local work packet removes environment terminology from sign-in, profile, Agent
Operations, inventory review, mailbox configuration and safe connection/error messages; replaces
the `.local` problem-type host with a stable environment-neutral problem URN; and adds a regression
check over the affected production UI and API content. It does not select or provision the still
unresolved production topology in Section 49.2, enable a paid provider, weaken fail-closed startup
validation or remove deterministic test capability. Acceptance evidence is affected web tests and
build, focused API error/authentication tests, architecture checks and a source sweep distinguishing
customer language from explicitly development-scoped implementation.

Retained local evidence on 2026-09-04: web type-check and lint passed without warnings, web unit
tests passed 7/7, and the production build passed. The Agent Operations browser assertions passed
2/2 across desktop and compact layouts; the Windows Playwright process required manual termination
after reporting both passes, so its command exit is not represented as clean. The Commercial API
compiled successfully through the existing `advertified-os2-dev` Compose project and Docker-pinned
SDK `10.0.400`. The existing API and web service containers were replaced in that same project and
returned healthy on their existing ports; no second Compose project or parallel service container
was created. The environment-neutral
API-content check passed, `git diff --check` passed, and the complete architecture run passed 42/45.
The same three unrelated in-progress inventory/runtime boundary failures recorded in Section 49.2.7
remain unresolved. No live provider, production resource or external communication was used.

### 49.2.9 Consolidated production-readiness, Day 0 inventory and Bedrock-cost implementation plan — 2026-09-04

Dataset-specific completion conditions in this historical plan are superseded by the owner's
2026-09-05 correction in Sections 11.21, 11.23 and 49.2.13. Processing, certifying or publishing the
then-current supplier collection is not an application installation or system-completion gate.
Historical source manifests, file counts, evaluation costs and results below remain dated evidence,
not mandatory product configuration or authority to recreate the removed parallel processing tools.

This plan consolidates the 4 September 2026 production-readiness audit, the Bedrock cost-lever
audit and the owner decisions in Section 11.23. Those reports are dated executable observations,
not authority to override this specification. Every reported defect must be re-verified against the
current worktree before editing because later in-progress changes may have moved or resolved it.
A category remains release-blocking until current acceptance evidence proves it closed.

This documentation update authorises planning only. It does not itself authorise application code,
cloud mutation, deployment, external communication or paid AI use. Implementation starts when the
repository owner explicitly gives the resulting implementation prompt to the delivery agent.

#### Locked implementation decisions

- Day 0 inventory intake is initiated by an authorised administrator upload/execute command. There
  is no continuous source discovery, rapid idle queue polling or automatic recurring reprocessing.
- Admin-loaded inventory may be published and used before a supplier has credentials. The system
  links an existing permanent Supplier ID or creates one administrator-managed `UNCLAIMED`
  Supplier, then allows a later invitation to claim that same identity and inventory.
- A new administrator-originated supplier file is a full replacement by default. Successful
  publication atomically supersedes the previous current release, expires its inventory, removes
  older pending work from operational views through soft deletion and marks affected uncommitted
  proposals for review. Failed or blocked replacement imports leave current inventory untouched.
- No pending proposal line is silently replaced. A human creates a new planning/proposal version.
  Confirmed bookings and historical commercial records remain immutable.
- The repository remains production `NO-GO` while any truth-critical delivery, onboarding,
  infrastructure, identity, recovery, observability, deployment or acceptance blocker below is
  unresolved.
- Live Bedrock remains fail-closed until the selected agents/models, access, evaluation, aggregate
  budgets, cost accounting, monitoring and kill switch are proven in capped production-like
  staging.
- Current forced structured-output `toolUse` contracts are not eligible for Bedrock Batch. Do not
  build Batch merely to chase theoretical savings.
- Do not change geographic cross-region inference for an assumed saving. Geographic CRI and
  in-region inference currently have the same relevant token price for the audited Nova Pro route;
  any later change must be justified by measured latency, throttling or an approved residency rule.
- Prompt caching is not approved from estimates alone. Cache accounting must be correct first, and
  a pilot may be chosen only after deployed traffic proves repeated stable-prefix volume inside the
  model's cache window.

#### Phase 0 — Current-truth baseline and controlled worktree

Before functional edits:

1. Read `ADVERTIFIED.md`, `AGENTS.md`, accepted ADRs and applicable nested instructions.
2. Preserve all pre-existing modified and untracked work. Do not reset, overwrite, stage or commit
   unrelated changes.
3. Re-run a bounded repository trace for each audit finding and record it as `OPEN`, `RESOLVED`,
   `PARTIAL` or `NOT_REPRODUCED`, with current file/line and executable evidence.
4. Record the exact production topology decision required by Section 49.2.2: compute, PostgreSQL,
   ingress/TLS/DNS, private object storage, background processing, secrets, telemetry, backups,
   recovery and deployment route. Do not silently choose expensive managed infrastructure or add a
   second orchestration platform.
5. Establish one release branch/revision strategy and one immutable build path. Current uncommitted
   work cannot be represented as a production release until it is deliberately reconciled.
6. Capture a baseline of focused builds/tests and existing connected journeys without claiming an
   unexecuted suite green.

**Exit evidence:** current gap register, selected topology record, clean ownership of every changed
path, reproducible baseline commands and no discarded work.

#### Phase 1 — Day 0 inventory, supplier identity, claiming and replacement

Implement the complete Section 11.23 contract as one coherent vertical slice:

1. Add governed claim, invitation, inventory-release, supersession and proposal-impact states/codes;
   do not scatter magic strings.
2. Add forward-safe persistence for Supplier claim state, SupplierClaimInvitation,
   SupplierInventoryRelease, import replacement mode, candidate soft deletion/supersession and
   ProposalInventoryImpact.
3. Make inventory work explicit-command/event driven. Commit the durable attempt before signalling
   a processor; wake it after commit; drain recoverable work at startup; inspect provider status
   only for an active attempt; perform no rapid idle polling and no idle Docling/Bedrock work.
4. Extract supplier identity/contact evidence, match by permanent identity attributes, reuse an
   existing Supplier ID, create one `UNCLAIMED` Supplier where identity is established, and require
   administrator resolution for ambiguous possible duplicates.
5. Allow authorised admin publication without supplier credentials while retaining all extraction,
   material-field, evidence and publication controls.
6. Implement single-use, expiring, revocable supplier-claim invitations. Acceptance through the
   configured identity provider attaches membership to the existing Supplier ID and immediately
   exposes only that supplier's existing inventory and permitted operations.
7. Publish a supplier release in one database transaction: create the new current release; expire
   and supersede the old release/products; cancel/supersede and soft-delete older pending imports,
   candidates and uncommitted listings; retain immutable history; create proposal-impact records;
   emit audit/outbox events.
8. Keep the previous release current if any new-import step fails, is cancelled, remains ambiguous
   or cannot publish.
9. Mark every draft, in-review, approved, sent or selected-but-unbooked affected proposal
   `INVENTORY_REVIEW_REQUIRED`; show old/new evidence and differences; reject stale acceptance,
   approval, sending and booking commands until an authorised new version resolves each impact.
10. Update internal Supplier, Inventory Import/Review and Proposal surfaces with human-safe status,
    release history, invitation controls, replacement preview and affected-line comparison.

**Exit evidence:** E2E-06 and E2E-16 pass against the real API/database path; tenant and supplier
scope tests pass; restart/idempotency/concurrency tests prove one cutover; failed replacement leaves
old inventory current; confirmed historical bookings remain unchanged; no live AI call is used.

#### Phase 2 — Truth-critical communications, onboarding, legal and payments

Close false-success and disconnected-product paths before infrastructure polish:

1. Proposal delivery must select a production provider explicitly, obtain a durable provider
   receipt or enter a truthful ambiguous/failed state, and only then record `SENT`. A deterministic
   no-op client must be impossible in production.
2. Start the outbox dispatcher for the selected EventBridge/production mode, preserve idempotent
   delivery and expose queue/dead-letter health. Merely registering a transport is insufficient.
3. Connect public contact/campaign enquiries and invitation-based supplier onboarding to canonical
   API workflows. Never show a successful public submission that was not accepted durably.
4. Align public registration wording with OIDC/invitation identity rather than application-managed
   passwords.
5. Publish only owner-approved, versioned legal documents and retain acceptance evidence. Until
   approved Privacy, Terms and Cookie content exists, disable the consequential public flow rather
   than inventing legal text or accepting users under a false completion state.
6. Reconcile the governed payment registry, backend capability and marketing. Implement and certify
   an advertised method or mark it unavailable and remove the claim. Never leave VodaPay or
   Advertise Now, Pay Later active merely as a label when only Manual EFT works.
7. Implement real notification delivery/status where the product promises it, with the same
   fail-closed receipt and audit rules.

**Exit evidence:** connected sandbox/staging sends and callbacks, duplicate/retry/ambiguity tests,
public enquiry and supplier-invitation journeys, versioned legal acceptance checks, and a truthful
payment capability matrix.

#### Phase 3 — Production platform, data protection and recovery

Implement the exact owner-approved topology reproducibly as infrastructure-as-code or an equally
reviewable automated deployment definition. It must include:

- immutable image publication to an approved registry;
- secure ingress, TLS and DNS without assuming an unnecessary load balancer;
- least-privilege network and service boundaries;
- managed secret injection and rotation paths;
- production PostgreSQL/PostGIS/pgvector with encrypted storage, protected network access,
  connection resilience and a documented migration path;
- private versioned object storage with encryption, public-access blocking, retention/lifecycle and
  recovery behavior;
- production-capable ClamAV and Docling placement, updates, limits and health;
- the selected durable worker/outbox mechanism without creating a second source of truth;
- production-like staging using the same images and configuration shape;
- automated database and object backups, declared retention, RPO/RTO, restore procedure and a
  successful restore rehearsal.

**Exit evidence:** reproducible environment creation, secret-free source/configuration, encrypted
private data paths, migration from zero and prior version, backup/restore proof, dependency health
and no use of development authentication, MailHog, local credentials or deterministic providers in
production configuration.

#### Phase 4 — Production identity and security controls

1. Configure and certify the chosen OIDC provider, PKCE, verified-email binding, logout, MFA policy,
   disabled-user behavior, invitation acceptance and session revocation.
2. Replace production shared static service secrets where the topology supports stronger
   short-lived IAM/workload identity or mTLS; otherwise document and test secure rotation.
3. Enforce PostgreSQL and external-service TLS, secure cookies, CSRF, least privilege and private
   service endpoints as applicable.
4. Provide edge abuse protection and production-appropriate rate limiting; use shared/distributed
   counters if more than one API replica can accept traffic.
5. Add SAST/CodeQL, dependency/secret scanning, DAST or equivalent staging checks, container
   vulnerability policy, image signing/provenance verification and a penetration-test/remediation
   record appropriate to launch risk.
6. Record credential rotation, break-glass, user-offboarding and security-incident procedures.

**Exit evidence:** connected identity tests, permission/tenant isolation, revoked-user/session
behavior, invitation attack tests, security scans with release policy, and no credential leakage.

#### Phase 5 — Bedrock/model production readiness and cost measurement

Keep live inference disabled while implementing the controls that make a later decision evidence
based:

1. Select and approve a provider/model/profile per agent rather than applying one model to all
   tasks. Record region/residency, model access, quality thresholds, latency expectations and per-run
   caps.
2. Extend the canonical usage ledger to retain actual input/output units, cache-read units,
   cache-write units, prompt-prefix hash, agent, model/profile, inference region where available,
   latency, throttle/error class, attempt outcome and calculated/actual cost.
3. Enforce tenant/day/month aggregate budgets in addition to per-call caps and provide a tested
   global live-provider kill switch.
4. Build production-quality evaluation sets and run capped staging tests for schema validity,
   grounding, quality, prompt/model regression, throttling, failure, latency and cost. Readiness
   configuration alone is not proof of successful Bedrock access.
5. Do not implement Batch while forced `toolUse` remains required.
6. Retain the current approved geographic CRI route until measured evidence or residency policy
   justifies change; do not claim token savings from moving to in-region.
7. Collect 30–90 days of real, attributable production measurements after launch. Only then rank
   caching candidates by repeated stable-token volume inside the supported TTL. Pilot one agent for
   a representative period after cache-read/write accounting is correct, and retain rollback.

**Exit evidence:** model/evaluation decision record, successful capped staging inference, complete
usage/cost telemetry, aggregate budget and kill-switch tests, no unsupported Batch path, and no
unmeasured caching claim.

#### Phase 6 — Observability, reliability and operability

1. Export structured logs, metrics and traces with correlation across web/API, worker, agent runtime,
   Docling, object storage, email and Bedrock.
2. Add dashboards and alerts for availability, latency, errors, database health, object storage,
   extraction queue/active attempts, lease loss, dead letters, outbox lag, email delivery, AI
   rejection, spend and backup failures.
3. Expand readiness checks to the dependencies required for the process role instead of reporting
   ready from PostgreSQL/master data alone.
4. Add standard transient HTTP/database resilience, bounded retries, circuit breaking where safe,
   graceful worker drain and recovery of leased work during deploy/restart.
5. Run load, large-file, concurrency, failure, restart, recovery and soak tests against the selected
   production topology and declared capacity.
6. Establish SLOs, incident ownership, escalation, support mailbox/channel and synthetic critical
   journeys.

**Exit evidence:** exported telemetry, actionable alert tests, dependency-aware readiness, graceful
restart evidence, capacity envelope and completed failure/recovery exercises.

#### Phase 7 — Immutable delivery and release pipeline

1. Use one pinned build/test toolchain to create the exact deployable images.
2. Generate SBOMs, scan, sign and attest image provenance; publish immutable digests.
3. Deploy those digests to production-like staging, run forward-safe migrations and post-deploy
   smoke/connected tests, then promote the same digests to production under explicit approval.
4. Implement rollback/roll-forward and database compatibility rules; never pretend an irreversible
   migration was rolled back.
5. Record deployment, migration, health, smoke, alert and rollback evidence automatically.

**Exit evidence:** exact staging-to-production digest promotion, migration compatibility proof,
successful rollback/roll-forward rehearsal and complete Section 49.3 release evidence.

#### Phase 8 — Consolidated release verification and go/no-go

During implementation, run focused tests for the changed slice. Do not run the complete suite after
every small edit. At the end of each cohesive phase, run its affected suites; after all fixes are
batched, run one consolidated final validation including:

- complete API, agent-runtime, web and architecture suites;
- OpenAPI/master-data/migration consistency;
- production build and container vulnerability policy;
- connected E2E-01 through E2E-16 as applicable to the release;
- real staging identity, email/outbox, object-storage, Docling and capped Bedrock checks;
- security, accessibility, performance, large-file, concurrency, restart and restore tests;
- production configuration validation and smoke tests;
- a clean, deliberately committed release revision with no unexplained modified/untracked paths.

The release decision is binary. One unresolved false-success path, unsupported public claim,
supplier/inventory data-loss risk, security blocker, missing recovery proof or unexecuted mandatory
acceptance journey remains `NO-GO`. Cost optimisation cannot waive product correctness or
operability.

### 49.2.10 Day 0 supplier inventory lifecycle implementation — 2026-09-04

The Day 0 inventory rules in Section 11.23 are implemented locally as one additive, production-
intent vertical slice. An administrator upload now creates the protected import and immediately
queues its durable extraction before opening the import workspace. Inventory extraction no longer
polls an empty scheduler queue: the worker performs startup recovery, polls only while a claimed
external attempt is active, then blocks on a PostgreSQL `LISTEN/NOTIFY` wake. Initial attempts and
authorised retries emit the same transactional notification, and listener reconnection triggers a
durable recovery check.

Migration `202609040052_DayZeroInventoryLifecycle` adds permanent supplier claim state, hashed
single-use claim invitations, exact supplier-user scopes, supplier inventory releases, import-to-
product bindings, supplier-identity issues, product/import/candidate supersession fields and
proposal inventory impacts. Source-extracted supplier identity is reconciled against the declared
identity and existing permanent Supplier IDs. A proven new identity creates one administrator-
managed `UNCLAIMED` Supplier; missing, conflicting or duplicate identities block publication rather
than creating an unsafe duplicate. A claimed supplier's later uploads are bound to the exact
supplier scope and cannot be redirected through a typed supplier name.

Successful `FULL_REPLACEMENT` publication performs one transactional cutover. It establishes the
new current supplier release, supersedes the previous release, expires products absent from the new
release, supersedes prior versions, soft-deletes older pending imports/candidates from ordinary
views, preserves all historical rows and registers impacts against uncommitted proposals. Failed or
blocked publication rolls the transaction back and leaves the previous release current. Ordinary
catalogue search excludes superseded inventory; permissioned release-history and supplier-current-
product endpoints retain traceability.

Uncommitted affected proposals are marked `INVENTORY_REVIEW_REQUIRED`; approval, send, selection and
booking transitions are rejected until an authorised replacement proposal version resolves every
impact. The old proposal and line references remain readable. Confirmed bookings are excluded from
this invalidation path. The import and proposal workspaces show the replacement warning and impact,
including current/new inventory counts, pending records, old/replacement release references and
whether a possible product replacement exists.

An authorised administrator can create, view and revoke an expiring supplier registration link.
Only its SHA-256 token hash is stored; the raw link is returned once for copying. Acceptance requires
an authenticated user whose verified email matches the invitation, creates the exact supplier scope
and links the user to the existing Supplier ID and already-loaded inventory. Existing platform or
supplier authority is preserved; an incompatible agency/advertiser membership is never silently
converted and instead produces an explicit role-conflict failure. Creating a replacement link for
the same supplier/email revokes the earlier active link.

The local verification batch compiled the Release API, ran the focused Day 0 worker, inventory
acceptance, extraction-durability, migration and OpenAPI tests, regenerated and synchronized the
canonical OpenAPI contract, ran the dedicated Day 0 source-contract checks, the architecture suite,
web type-check/lint/unit/build checks and `git diff --check`. Disposable databases inside the one
existing `advertified-os2-dev` PostgreSQL service exercised unclaimed supplier creation, two atomic
release cutovers, expiry/supersession, pending soft deletion, stale-proposal blocking, compatible
invitation acceptance and incompatible-role preservation. The disposable databases were removed.
No shared application data, existing service container, persistent volume, production resource,
external communication or live/paid AI provider was changed or invoked. The implementation remains
uncommitted in the pre-existing dirty worktree and must be reconciled with concurrent inventory work
before release.

### 49.2.11 Inventory schema-discovery takeover — 2026-09-05

The owner requested incorporation of the generic inventory extraction architecture while preserving
the concurrent lifecycle/UI changes, then authorised necessary tests, builds and local Docker
deployment, followed by committing and pushing all changes when complete. This is not authority
for paid/live provider calls, production deployment or commercial publication.

The bounded takeover reconciles structural extraction, once-per-document schema discovery,
deterministic batch evidence projection, normalization and exception review. Unknown labels and
supplier names must not require code changes. Supplier ownership comes from authentication or an
explicit administrator decision, never a filename or an AI interpretation. Raw values, source
positions and proposed meanings survive rejection; ambiguous values must remain unresolved.
Retained reprojection uses stored schema/evidence without repeating provider work. A rejected or
absent schema requires document review and must not fall back to commercial heuristics.

The preceding release gate is not delivered or approved by this takeover. Acceptance still requires
affected builds, focused safety regressions, architecture checks, supplier isolation/replacement
journeys and truthful unfamiliar-format evidence. Processing the current supplier collection is
a separate data task, not a product release prerequisite. Deterministic interpreter fixtures
prove batching and evidence contracts, not model generalization or production readiness. Current
commands, results and unresolved findings are retained in the machine-readable takeover disposition;
earlier Section 49.2.10 verification describes its dated state, not the reconciled working tree.


### 49.2.12 Opportunity agent notification dispatch - 2026-09-05

The owner's focused source-only request replaces the opportunity dispatcher's 100 ms idle
claim loop with the existing PostgreSQL notification transport. This work does not advance the
unapproved release gate described in Section 49.2.11. The preceding dated inventory evidence was
inspected; its concurrent implementation is preserved apart from a shared listener channel overload.
The exact work packet, baseline and verification disposition are retained under
`artifacts/agent-dispatch-fix/`; these are local evidence, not a second normative specification.

`commercial.agent_runs` remains durable and `commercial.claim_next_agent_run` remains the sole
execution claim authority. The listener subscribes on `advertified_agent_run` before draining
queued/recoverable jobs, including at startup and after reconnect. Migration
`202609050056_AgentRunNotificationDispatch` adds transactional, empty-payload notifications for
inserts and relevant scheduling changes across all producers, including automatic retries and
human resumes. Rollbacks do not deliver notifications. Step checkpoints, unrelated updates and
ordinary lease extensions do not notify. A claim's new running lease also signals other listeners
so they can reconsider recovery deadlines. Signals neither create jobs nor grant authority.

After draining, the worker waits for a notification, the earliest eligible retry/lease deadline,
or `AgentRuntime:RecoverySweepSeconds` (default 300; range 30-3600), whichever comes first.
The deadline function uses two ordered partial indexes and matches claim eligibility: queued
next-attempt time, or the later of a running lease expiry and its next-attempt time. A running
record with no lease expiry is not recoverable under the existing claim and is excluded. A
one-second wait for already-due timestamps bounds contention/clock skew and crosses strict lease
expiry; future deadlines are awaited directly, including subsecond deadlines. Lost signals
are repaired by durable sweeps; reconnect always resubscribes before checking work. The existing
two-minute lease, serial processing per instance, attempt accounting and 30/120/600-second
HTTP-failure retry policy are unchanged. No new exactly-once or stale-worker fencing guarantee is
claimed; existing opportunity completion/lease protections have not been redesigned.

`AgentRuntime:ReconnectMinSeconds` defaults to 5 (range 5-60) and
`AgentRuntime:ReconnectMaxSeconds` to 60 (at least the minimum, at most 300). Failed sessions use
capped exponential backoff and log the first failure per outage; a successful drain/deadline/wait
cycle resets the backoff and logs recovery. Shutdown cancels waiting and backoff. The removed
`AgentRuntime:PollMilliseconds` setting must be removed from external overrides. Worker enablement
still requires a worker-capable process role and an AgentRuntime mode other than Disabled.
The existing `WorkerSchedulerDatabase` connection is reused, without granting worker table access.
The new SECURITY DEFINER deadline function has a fixed search path, migrator ownership, PUBLIC
execution revoked and only worker execution granted. Application `SET LOCAL ROLE` is unchanged.

Base and Development logging set `Microsoft.EntityFrameworkCore.Database.Command` to Warning;
application Information logging, database warnings/errors and job outcome logs remain available.
Repository Console settings change formatting only and do not override this category. External
logging overrides and the deployed configuration still require verification. Log filtering is not
proof of reduced database activity. An otherwise idle opportunity worker now intends one empty
claim and one deadline query per five-minute sweep, plus startup/signals, instead of about ten
claim transactions per second; no measured deployed reduction or financial savings is asserted.

Deployment prerequisites are a separately authorised forward migration followed by deployment
of the matching worker/API source. The new worker must not be deployed ahead of the migration.
No running service may be rebuilt, restarted or recreated, and no migration may be applied under
this task. Runtime proof remains pending for trigger commit/rollback, subscription races, buffered
signals, multiple workers/atomic claiming, offline work, reconnect, retry/lease recovery, query
plans and idle database activity. Deterministic session tests verify scheduling control flow only.
The existing database migration test also contains a listener commit/rollback transport probe,
which requires separately authorised database integration verification.

Source validation completed with the repository Dockerfile's SDK 10.0.400 test target:
Release API/migrator and test assembly compiled; all 15 focused `AgentRunDispatchSessionTests`
passed with zero build warnings/errors. `python -m pytest tests/architecture -q` passed all 46
checks; `git diff --check` passed. Earlier unittest discovery (no tests), the PowerShell wrapper's
native-stderr handling, and five nullable-number fixture conversion failures are recorded in the
local evidence with their corrected final outcomes. Database integration tests were not executed.
These outcomes do not constitute deployment verification or owner release approval.

Adjacent loops remain outside this fix: `api/Background/CommercialWorkerService.cs` uses
`WorkerDispatch:PollMilliseconds` (default 500) for email/outbox idle checks;
`api/Background/OutboxDispatchDispatcher.cs` uses `OutboxDispatch:PollMilliseconds` (default 250).
Active email/inventory/outbox lease maintenance and external active-extraction monitoring remain
necessary and unchanged. This is not a claim that the whole application is free of polling.

### 49.2.13 Dataset-independence correction — 2026-09-05

The owner directed removal of the mistaken coupling between Advertified and current supplier files,
including the parallel offline transcription, row-wise Bedrock processing, product assembly,
generated-workbook reimport, bulk approval/publication and fixed-dataset production-completion
tools. The application upload, source-reader/schema interpretation, validation and publication
workflow remains the canonical path. Supplier-specific repair/gold generators and private-file
regression dependencies were removed; original supplier inputs and historical evidence were not.
Web package shortcuts embedding the current supplier roster were removed. Retained evaluation
selection is explicit and manifest-scoped; it must not imply that all tenant inventory is in scope.

Sections 11.21 and 11.23 now separate replaceable input data from application dependencies.
Product readiness concerns implemented and verified capabilities, document readiness concerns the
specific source revision and its evidence/decisions, and campaign readiness concerns suitable supply
for the campaign. No loaded inventory is a valid application state: searches return no available
supply and dependent workflow steps explain what is missing. Replacing inventory uses the existing
release/history and affected-uncommitted-work rules, not application redevelopment.

The local correction is verified only by the exact repeatable commands recorded in
`artifacts/audit-remediation/2026-09-05-disposition.json`; it is not a production-readiness approval.
No physical inventory extraction, paid provider call, migration or deployment was performed for
this correction. Current-file visual certification remains a separately scoped, deferred data task.

The subsequent completion work uses synthetic fixtures and isolated disposable test databases only.
Its migration verification identified and corrected a fresh-install dependency in the pending
Day Zero migration: the human-task collection must exist before its reference item is inserted.
No running application database or supplier data is changed by this test setup. Test/build results
and remaining limitations are retained in the same disposition evidence; implementation alone is
not a declaration of readiness or owner approval.

### 49.2.14 Owner-approved pre-release database baseline (2026-09-05)

The owner confirmed that no production deployment exists and approved consolidating the
development migration history into a fresh-install baseline. The baseline represents the current
intended schema, including constraints, indexes, functions, ownership, grants and tenant isolation.
It contains no supplier inventory, tenant/user fixtures or other replaceable business data.
Required reference data continues to come from the governed master-data registry and its existing
idempotent bootstrap, not a dump of an operator database.

The superseded development migration implementations are deleted from the active application;
version control retains historical evidence. The owner explicitly clarified that there is no
working database requiring compatibility: this is a development reset, not an upgrade project.
There is no compatibility bridge, old-history restamping or retained backfill chain. Subsequent
schema changes use incremental migrations from this baseline. Its ordinary initial-migration
rollback drops its application schemas and is destructive; verification runs it only in disposable
databases. This code change does not execute a reset against any existing Docker stack.

Verification uses disposable databases to prove clean installation and idempotence and exercise
the affected security and persistence boundaries. A schema-only comparison is implementation
evidence for accidental omissions, not authority to retain obsolete product behaviour.
Exact outcomes are retained in `artifacts/audit-remediation/2026-09-05-migration-baseline.json`.
This work is distinct from inventory extraction and is not production-readiness approval.

Local completion evidence (2026-09-05): the pinned API/migrator build and 78 selected deterministic
tests passed. Twelve baseline/security/durability/restore integration cases passed in the broader
disposable run; all six inventory lifecycle cases and the command-replay case passed on the final
source. Earlier failing runs remain recorded: export search-path interference, publication SQL
alias ambiguity, empty publication-audit metadata, oversized multipart error classification and
obsolete direct-approval fixtures were corrected without weakening acceptance/publication guards.
All 50 architecture checks, 29 source-tool checks, 23 agent-runtime checks and three desktop
inventory browser scenarios passed. Web build/lint passed with the existing large-chunk warning
and two synchronous-effect warnings. Tracked/untracked source secret scans and diff checks passed.

The final API validation command is `powershell -NoProfile -File tools/run-api-release-tests.ps1`
with `-Filter 'FullyQualifiedName~MasterDataMigrationTests.ModelSnapshotMatchesCurrentPersistenceModel|FullyQualifiedName~InventoryAcceptancePolicyRegressionTests|FullyQualifiedName~InventoryInterpretationRevisionTests|FullyQualifiedName~InventorySchemaEvidenceRegressionTests|FullyQualifiedName~OpenApiContractTests.RetainedV1ContractMatchesTheRunningApi|FullyQualifiedName~DoclingInventoryExtractionAdapterTests|FullyQualifiedName~CommandIdentityRegressionTests'`
and `-IntegrationFilter 'FullyQualifiedName~InventoryAcceptanceTests|FullyQualifiedName~PersistedCommandAcceptanceTests'`.
Baseline/security/restore cases can be reproduced through the same pinned path using
`-IntegrationFilter 'FullyQualifiedName~MasterDataMigrationTests.MigrationBootstraps|FullyQualifiedName~TenantIsolationMigrationTests|FullyQualifiedName~WorkerSchedulingMigrationTests|FullyQualifiedName~OutboxDispatchDurabilityMigrationTests|FullyQualifiedName~EmailDeliveryDurabilityMigrationTests|FullyQualifiedName~InventoryExtractionDurabilityTests|FullyQualifiedName~CommercialPolicyAcceptanceTests|FullyQualifiedName~DatabaseRecoveryAcceptanceTests'`.
Run `python -m pytest tests/architecture -q` from the repository root. The web journey command is
`npx playwright test e2e/inventory-workflow.spec.ts --project=desktop` from `web`, after
`npm run build` and `npm run lint`. Detailed local command/results logs remain under
`artifacts/audit-remediation/`; this is scoped inventory/baseline verification, not closure of every
historical product-wide audit finding. No supplier corpus was processed, no paid provider called,
and no existing application stack redeployed.

## 49.3 Release evidence

A production release records:

- commit/build identifiers;
- container/artifact hashes;
- schema/migration range;
- master-data/policy versions;
- exact environment;
- required test/acceptance results;
- security/privacy checks;
- backup/restore evidence where release requires it;
- provider/model configuration and cost limits;
- known limitations/blockers;
- approver(s) and timestamp;
- rollback/incident plan.

One unresolved release-blocking check remains a NO-GO. AI cannot waive it.

## 49.4 Handoff completeness test [Principle]

The documentation is considered build-complete only if a competent team can receive only `ADVERTIFIED.md`, `AGENTS.md`, the approved brand assets/environment inputs and an empty implementation repository, then build the system without inventing Advertified business behaviour.

The team may choose ordinary implementation details such as internal class names, private helper structure, query composition and equivalent library mechanics. It may **not** need to invent or guess:

- product scope;
- user roles or authority;
- canonical lifecycle/state transitions;
- data ownership or immutability rules;
- approval/self-approval behaviour;
- evidence/materiality behaviour;
- agent responsibilities/tool boundaries;
- inventory extraction/publication behaviour;
- commercial calculations;
- client/supplier transaction rules;
- screen outcomes and exceptional states;
- error/recovery/idempotency behaviour;
- production acceptance journeys.

If implementation exposes a genuinely missing business decision, that is a specification defect. The missing decision must be resolved by the owner and added to `ADVERTIFIED.md`; developers must not quietly invent the answer in code.

---

# 50. Final Canonical Statement

Advertified's durable value is not that an LLM can write a media plan.

The durable value is the platform's ability to know and preserve:

- what was requested;
- what was actually supplied;
- what is verified;
- what is inferred;
- what is unknown;
- which evidence supports each material claim;
- what inventory exists;
- what its commercial state is;
- what suppliers quoted;
- what was negotiated;
- what was approved;
- who approved it and whether approval was self or independent;
- what the client selected;
- what was booked;
- what was delivered;
- what it cost;
- what happened afterwards;
- what was learned.

Specialised AI agents reason across that governed history to reduce manual work and improve commercial preparation. They do not replace the evidence, commercial rules, human authority or transaction record.

> **This `ADVERTIFIED.md` is the canonical Advertified business, product, commercial, governance, workflow, AI, data, UX, architecture and production build truth. Together with `AGENTS.md` for contributor behaviour, it is intended to be sufficient to build Advertified without inventing core business behaviour. If implementation reveals a missing business decision, the specification must be corrected rather than the decision being silently invented in code.**
