# Advertified Unified - Executable Implementation Plan

## Document Authority and Source Hierarchy

**Status**: Active implementation plan (not specification restatement)

**Source precedence** (from specification Section 16.1):
1. Current explicit owner instruction
2. Advertified Unified Build Specification v1.1 (normative)
3. Clean-branch contracts and tests
4. Approved business artefacts and migration data
5. Legacy application (read-only reference only)
6. Provider documentation (vendor-specific only)

**Non-negotiable decisions** (from specification Sections 1, 15):
- Commercial API owns business state (agents/tools propose, never mutate directly)
- CampaignBrief is a working aggregate with immutable BriefVersions
- Evidence before interpretation (approved evidence before agent use)
- Approval before consequence (human gates for client delivery, spend, publication)
- No identity-graph dependency in initial release
- Clean redevelopment (legacy is read-only reference)
- Customer-facing language only (no internal terminology on screens)
- Only 11 named agents (closed roster)
- Zero live/paid Bedrock during certification
- AWS af-south-1 production region
- Deterministic provider for testing

## Current Repository Baseline

**Repository**: advertified-commercial-os2 (local)
**Branch**: master
**Commit**: ed3bc26d321140ab69b60e5c368017ade92a900e
**Status**: PARTIAL - Scaffolding only, not verified baseline

### Executed Commands and Results

```bash
# Project structure creation
✓ Created directories: web, api, agent-runtime, workers, shared, infrastructure, tests, docs

# Web application
✓ npm create vite@latest . --template react-ts
✓ React 19.2.8, TypeScript 6.0.2, Vite 8.2.2
✓ Added ESLint configuration

# Commercial API  
✓ dotnet new webapi -n Advertified.Commercial.Api --no-https
✓ .NET 8.0 (corrected from 10.0)
✓ Added EF Core, PostgreSQL, Swagger packages

# Agent runtime
✓ Created Python/FastAPI scaffold
✓ main.py with health endpoints
✓ requirements.txt with core dependencies

# Infrastructure
✓ docker-compose.yml with PostgreSQL, MinIO, Redis, Mailhog
✓ env.example with configuration template
✓ Database initialization script

# Shared contracts
✓ Common JSON schemas
✓ Master data registry with initial seeds

# Documentation
✓ README.md (basic overview)
✓ SETUP_GUIDE.md (developer onboarding)
✓ CAPABILITY_LEDGER.md (tracking - PARTIAL)
✓ ADR template and first decision (AgentCore)

# Architecture tests
✓ boundary-tests.py (basic structure)

# Git initialization
✓ git init, git add, git commit
✓ Commit SHA: ed3bc26d321140ab69b60e5c368017ade92a900e
```

### Missing Evidence for Gate 0 Completion

**Critical gaps that prevent claiming COMPLETED status:**

1. **Docker services health verification**
   - PostgreSQL: Not started, not tested
   - MinIO: Not started, not tested  
   - Redis: Not started, not tested
   - Mailhog: Not started, not tested

2. **Build execution results**
   - Web: `npm run build` not executed
   - API: `dotnet build` not executed
   - Agent runtime: Not tested

3. **Test execution results**
   - Unit tests: Not created, not executed
   - Integration tests: Not created, not executed
   - Architecture tests: Basic structure only, not comprehensive

4. **Current routes and screenshots**
   - No route documentation
   - No screenshots of running applications
   - No API endpoint verification

5. **Legacy disposition register**
   - No legacy systems identified
   - No migration data catalogued
   - No reference-only boundaries defined

6. **Known failures and blockers**
   - No failure log maintained
   - No blocker tracking system
   - No risk register

7. **Production resource confirmation**
   - No production AWS resources used (correct)
   - No production credentials used (correct)
   - No external APIs called (correct)

### Existing User Changes

**None** - Clean repository, no uncommitted work to preserve.

### Current Verdict

**Gate 0 Status**: PARTIAL - Scaffolding complete, verification pending

**No-go conditions**: All Docker services must be verified as healthy, all builds must execute successfully, basic test coverage must exist before advancing to Gate 1.

## Scope, Exclusions and Non-Negotiable Decisions

### In Scope (v1.1)

- Authenticated product for internal teams, agencies, advertisers, suppliers, influencers
- Opportunity-to-proposal workflow with human approvals
- Inventory ingestion and supplier operations
- Campaign booking and delivery tracking
- Amazon Bedrock AgentCore integration
- PostgreSQL/PostGIS/pgvector data layer
- AWS af-south-1 production deployment

### Explicit Non-Goals (from Section 3.3)

- Omnicom-scale consumer identity graph (deferred)
- Autonomous spend/publication without human approval (never)
- Legacy application rehabilitation (never)
- Generic strategies without evidence (never)
- Manual one-off inventory fixes (never)

### Technology Boundaries (Section 17)

**Locked technology stack:**
- Web: React 19.2.0/TypeScript/Vite (no Tailwind without ADR)
- API: C#/.NET ASP.NET Core/EF Core (only write boundary)
- Runtime: Python/FastAPI AgentCore-compatible (no A2A containers)
- Database: PostgreSQL/PostGIS/pgvector (no SQLite)
- Storage: S3-compatible (no local file storage)
- AI: AWS Bedrock only (no Explee, no unapproved providers)

**Ownership boundaries:**
- C#/.NET owns commercial truth
- Python/FastAPI owns AI orchestration  
- React/TypeScript/Vite owns authenticated presentation
- PostgreSQL/PostGIS/pgvector is the only canonical database

## Product Capability Map

### Core Workflows

**1. Unbriefed Opportunity to Approved Brief**
- Input: Prospect + permitted sources
- Process: Evidence capture → Business interpretation → Opportunity angles → Strategy → Critic → Brief drafting
- Output: Approved BriefVersion with evidence lineage
- Human gates: Evidence approval, Strategy approval, Brief approval

**2. Full Campaign: Approved Brief to Proposal**
- Input: Approved BriefVersion
- Process: Audience → Media mix → Inventory shortlist → Supply confirmation → Media plan → Proposal tiers
- Output: Approved proposal with branded PDF
- Human gates: Audience approval, Media mix approval, Plan approval, Proposal approval

**3. Rapid OOH**
- Input: Brief (system determines OOH path)
- Process: Geography/routes/POIs → OOH eligibility → Supplier confirmation → Shortlist → Plan
- Output: Approved OOH-specific plan
- Special handling: Provisional availability clearly labelled

**4. Inventory Import and Supplier Operations**
- Input: Supplier files (PDF/XLSX/CSV/images)
- Process: Upload → Classify → Extract → Normalize → Validate → Review → Publish
- Output: Searchable catalogue with evidence lineage
- Human gates: Material fact confirmation, Publication approval

### Agent Roster (Closed - Section 6)

| Agent Code | Purpose | Typed Output | Human Gate |
|------------|---------|--------------|------------|
| opportunity_intelligence | Generate ranked opportunity angles | OpportunityAngleSet | Angle selection |
| business_interpretation | Interpret business model/customers | BusinessInterpretation | Confirmation |
| strategy | Create growth strategy | StrategyVersion | Strategy approval |
| brief_drafting | Draft campaign brief | BriefVersion | Brief approval |
| audience | Define audience segments | AudienceDefinitionSet | Audience review |
| inventory_intelligence | Score eligible inventory | InventoryShortlistDraft | Selection/confirmation |
| media_planning | Create media mix/plan | MediaMixVersion/MediaPlanVersion | Mix/plan approval |
| critic_readiness | Identify weaknesses/risks | CriticReport | Objection resolution |
| proposal_narrative | Generate client narratives | ProposalNarrativeDraft | Proposal approval |
| creative | Draft creative concepts | CreativeConceptSet | Creative approval |
| measurement | Interpret performance | MeasurementInterpretation | Optimisation approval |

## Roles and Authorisation Matrix

### Canonical Roles (Section 20.1)

| Role Code | Display Label | Maximum Scope |
|-----------|---------------|---------------|
| platform_admin | Platform Administrator | Cross-tenant administration |
| internal_planner | Internal Planner | Assigned clients/work queues |
| inventory_ops | Inventory Operations | Assigned channels/suppliers |
| agency_admin | Agency Administrator | Own agency/assigned advertisers |
| agency_campaign_user | Agency Campaign User | Assigned advertisers/campaigns |
| advertiser_admin | Advertiser Administrator | Own advertiser tenant |
| advertiser_approver | Advertiser Approver | Own advertiser/assigned resources |
| supplier_admin | Supplier Administrator | Own supplier tenant |
| supplier_user | Supplier User | Assigned supplier resources |
| influencer_rep | Influencer Representative | Owned/represented profiles |
| agent_runtime_service | Agent Runtime Service | No interactive login |
| worker_service | Worker Service | No interactive login |

### Permission Matrix (Excerpt - Full in Section 20)

**Critical invariant**: Every tool call carries tenant, actor, role, resource, correlation identifiers. Commercial API independently re-authorises.

## Domain Aggregates and Invariants

### Core Aggregates (Section 18)

**Tenant**: Multi-tenancy root
- Invariant: Every protected row carries tenant_id or parent tenant reference
- Lifecycle: Active, suspended, archived
- Version: Settings versioned with effective dates

**User**: Authentication and identity
- Invariant: Case-insensitive unique email, no provider password in domain tables
- Lifecycle: Active, locked, archived
- Security: MFA state, last login tracking

**Membership**: User-tenant-role binding
- Invariant: Unique tenant/user combination, role from canonical registry
- Lifecycle: Pending, active, revoked
- Audit: Invited by, invited at, accepted at

**CampaignBrief**: Working aggregate (Section 5.1)
- Invariant: Contains immutable BriefVersions, never silently rewrites approved versions
- Version: Current draft pointer, current approved pointer
- Required: Business problem, objective, audiences, geography, timing, budget, VAT, fees, constraints, measurement, unknowns, assumptions, evidence, approval

**BriefVersion**: Immutable campaign intent
- Invariant: Immutable after submission, approved versions never edited
- Change: Material change creates new draft version
- Evidence: References approved EvidenceItems

**StrategyVersion**: Commercial growth thesis
- Invariant: Requires critic report and approval
- Content: Diagnosis, growth thesis, objectives, audiences, proposition, message, channel implications, risks, evidence

**InventoryProduct**: Verified supply
- Invariant: Supplier controls only own organisation, evidence-linked rates/availability
- Freshness: Rate/availability confirmed at, expires at, stale reason
- Verification: Verification level, source locators

**MediaPlanVersion**: Executable planning recommendation
- Invariant: Approved input versions frozen by reference, material change invalidates
- Reconciliation: Selected option controls downstream supply
- Supply: Supply status confirmed before approval

**ProposalVersion**: Client-facing commercial offer
- Invariant: Three materially different tiers, cannot approve if plan/pricing changed
- Selection: Exact immutable option controls bookings/invoicing
- Expiry: Time-bound, expired proposals require regeneration

## Master Data Catalogue

### Canonical Master Data (Section 17.3)

**Code-controlled states** (in enums/value objects):
- Lifecycle statuses, command names, event types, permission verbs, invariant reason codes

**Configurable master data** (in database tables):
- Channels, inventory product types, rate types, asset types, measurement units, rejection reasons, task priorities, proposal tier labels, supported document classes

**Account configuration** (tenant settings):
- Fee policy, VAT treatment, tier budget bands, approval policy, freshness windows, provider policy, notification preferences

### Seeded Master Data (Initial)

**Channels**: OOH, DOOH, RADIO, TV, PRINT, DIGITAL, SOCIAL, INFLUENCER, EXPERIENTIAL, PODCAST, RETAIL, TRANSIT, MALL, EMAIL, MOBILE

**Roles**: platform_admin, internal_planner, inventory_ops, agency_admin, agency_campaign_user, advertiser_admin, advertiser_approver, supplier_admin, supplier_user, influencer_rep

**Proposal Tiers**: ESSENTIAL (1.0x), GROWTH (1.5x), PREMIUM (2.0x)

**Lifecycle Statuses**: CREATED, QUALIFYING, EVIDENCE_REVIEW, STRATEGY_READY, BRIEF_READY, PLANNING, PROPOSAL_READY, WON, LOST, ARCHIVED, DRAFT, IN_REVIEW, APPROVED, REJECTED, SENT, SELECTED, DECLINED, EXPIRED

## Agent and Tool Registry

### Agent Contract Template (Section 22.3)

**Every agent must define:**

1. **Agent identifier and purpose**
   - Code: e.g., "opportunity_intelligence"
   - Purpose: Single-sentence business question

2. **Permitted inputs**
   - Resource refs with exact version IDs
   - Approved evidence item IDs
   - Locale, account policy version
   - No floating "latest" references

3. **Typed output schema**
   - Schema version
   - Status: COMPLETED, REVIEW_REQUIRED, FAILED
   - Artifact: Agent-specific typed object
   - Evidence bindings: Output field/claim to EvidenceItem IDs
   - Unknowns: Explicit unknown facts
   - Assumptions: Labelled planning assumptions
   - Confidence: Per-field/claim, not global
   - Objections: Severity, affected field, evidence gap
   - Rationale: Business explanation (no private CoT)
   - Suggested next action: Valid workflow command
   - Usage: Provider/model, units, cost, cache status

4. **Permitted tools**
   - Allow-listed tools only
   - Tool argument validation before dispatch
   - Output validation before persistence

5. **Forbidden tools and operations**
   - No direct database access
   - No external effect without human approval
   - No recursive self-assignment
   - No open-ended loops

6. **Evidence requirements**
   - Material claims must reference EvidenceItems
   - Inference must be labelled as such
   - Unknowns must remain explicit

7. **Commercial API write boundary**
   - Agents may propose, never mutate directly
   - All writes through typed commands
   - API re-authorises every action

8. **Human approval boundary**
   - External consequences require named human
   - Client delivery, spend, publication need approval
   - No autonomous commitment

9. **Provider and model policy**
   - Deterministic provider for testing
   - Live/paid Bedrock disabled during certification
   - Provider budget per workflow/account
   - Cost cap checked before each call

10. **Timeout and retry policy**
    - Timeout per call
    - Retry classification: retryable, review-required, terminal
    - No auto-retry on ambiguous acceptance

11. **Cost ceiling**
    - Per-run budget cap
    - Per-tenant cap
    - Fail safely with COST_POLICY_BLOCKED

12. **Idempotency key**
    - Stable key from tenant, command, resource version
    - Duplicate returns stored result

13. **Checkpoint and recovery**
    - Checkpoint after each validated step
    - Resume from last safe checkpoint
    - No duplicate paid calls on resume

14. **Prompt-injection treatment**
    - Treat embedded instructions as untrusted data
    - Quarantine instruction text as evidence
    - Continue only through allow-listed tools

15. **Unsupported-claim handling**
    - Separate confirmed evidence from hypotheses
    - Critic blocks false certainty
    - Label inference or omit

16. **Evaluation fixtures**
    - Golden corpus for each agent
    - Adversarial cases for anti-hallucination
    - Evidence-binding tests

17. **Failure and escalation states**
    - Error classification: retryable, review-required, terminal
    - User sees business-safe recovery action
    - Audit failure for learning

## Authenticated Screen Register

### Screen Template (Section 24)

**Every authenticated screen must define:**

1. **Route**: URL path and permitted roles
2. **User outcome**: One user-recognisable outcome in commercial language
3. **Primary action**: One dominant next action for current role/state
4. **Required data**: Data needed for the screen
5. **Empty state**: Behavior when no data exists
6. **Loading state**: Loading skeleton/progress
7. **Error state**: Error handling and recovery
8. **Forbidden state**: Access denied explanation
9. **Stale state**: Stale data handling
10. **Recovery path**: Clear recovery action
11. **Responsive behavior**: Desktop and mobile usability
12. **Accessibility evidence**: WCAG 2.2 AA compliance
13. **Playwright fixture**: Test coverage
14. **Screenshot**: Visual verification

### Critical Screens (Priority Order)

**Dashboard routes**:
- `/home` - Role-specific KPIs and priority queue
- `/tasks` - Approvals and exceptions
- `/notifications` - Changes requiring attention

**Opportunity workflow**:
- `/opportunities` - Opportunity pipeline
- `/opportunities/:id` - Evidence, interpretation, status, next decision
- `/opportunities/:id/evidence` - Source facts, conflicts, provenance
- `/opportunities/:id/strategy` - Strategy, objections, readiness

**Brief workflow**:
- `/briefs/new` - Upload/paste original brief
- `/briefs/:id` - Current approved brief and workflow status
- `/briefs/:id/review` - Review unknowns, assumptions, changes
- `/briefs/:id/versions` - Immutable version history and comparison

**Planning workflow**:
- `/audiences/:id` - Audience definitions and evidence
- `/media-mix/:id` - Channel roles, allocations, rationale
- `/inventory/intelligence` - Search, benchmark, compare verified supply
- `/inventory/items/:id` - Inventory truth, evidence, rates, assets, versions
- `/inventory/imports` - Import runs and quality status
- `/inventory/imports/:id/review` - Human review of extraction exceptions
- `/plans/:id/inventory` - Map/grid selection with eligibility and rejection reasons
- `/plans/:id/supply` - Availability, rates, supplier responses, forecast
- `/plans/:id/review` - Plan, assumptions, critic objections, totals

**Proposal workflow**:
- `/proposals` - Proposal pipeline and deadlines
- `/proposals/:id` - Three-tier comparison and commercial detail
- `/proposals/:id/document` - Branded document preview and versions
- `/proposals/:id/send` - Confirm exact recipient and approved version
- `/proposals/:id/funding` - Record selected option, signed PO, payment route

**Supplier workflow**:
- `/supplier/home` - Listings, requests, freshness, earnings
- `/supplier/inventory` - Manage own catalogue
- `/supplier/inventory/new` - Create one listing with evidence
- `/supplier/imports` - Bulk upload and resolve errors
- `/supplier/availability` - Maintain current availability
- `/supplier/requests` - Respond to RFQs and booking requests
- `/supplier/quotes/:id` - Confirm rate, terms, validity
- `/supplier/bookings` - Track accepted bookings and delivery

**Campaign workflow**:
- `/campaigns` - Booked and active work
- `/campaigns/:id` - Bookings, creative, delivery, measurement

**Operations workflow**:
- `/admin/agents` - Dispatch health, failures, cost, configuration
- `/admin/audit` - Business and agent audit trail
- `/admin/commercial` - Markups, management fees, VAT, approval policy
- `/admin/access` - Users, roles, tenant assignments
- `/admin/integrations` - Provider, email, maps, partner connections

## Event, Job and Integration Catalogue

### Canonical Business Events (Section 25.2)

**Evidence events**: EvidenceApproved
**Strategy events**: StrategyApproved
**Brief events**: BriefApproved
**Planning events**: MediaMixApproved, InventoryRateChanged, AvailabilityChanged, MediaPlanApproved
**Proposal events**: ProposalApproved, ProposalTierSelected
**Supply events**: SupplierResponseReceived, BookingConfirmed
**Campaign events**: CreativeApproved, DeliveryProofSubmitted
**Agent events**: AgentRunReviewRequired, AgentRunCompleted

### Durable Jobs (Section 25.1)

**Job types**: Import processing, crawling, extraction, rendering, notifications, dispatch recovery, scheduled freshness checks

**Job contract**: Claim with lease, checkpoint after each step, idempotent side effects, retry policy, cancellation handling, poison job retention

### Integration Catalogue (Section 26)

**Identity/OIDC**: Authenticate, logout, invite acceptance, session refresh, MFA
**AWS Bedrock**: Structured completion, cancellation, usage, provider request ID
**Docling**: Classify, render, extract structure, coordinates, assets
**S3-compatible**: Put/get intent, immutable version, hash, metadata, short-lived URL
**Resend**: Send templated transactional email, receive delivery events
**Maps/geography**: Geocode, reverse geocode, routes, POIs, map tiles
**Payment/funding**: VodaPay intent, manual EFT reconciliation, manual Advertise Now Pay Later
**Supplier systems**: Catalogue/rate/availability import, RFQ, booking status
**Measurement**: Import delivery/performance facts with methodology metadata

## Test Fixture and Evaluation Register

### Named Behavioural Fixtures (Section 31.2)

**Rayetsa Furniture**: Evidence-led unbriefed opportunity. Household/SME/hospitality/event buying occasions. WhatsApp/purchase/rental context. Gauteng-first reach. Paid social/search/WhatsApp partnerships. Radio for trust/reach. No newspaper or affluent-Sandton assumptions. Three materially different choices (~R100k/R200k/R350k). Measure enquiry → consultation → quote → deposit → sale.

**Takealot Black Friday**: Rapid OOH. R320k planning budget, explicit flexibility to R400k. JHB/CPT/DBN. Mall of Africa, Sandton City, Gateway, Cavendish, Menlyn. Digital mega boards and digital 6x3 only (no static). Nov 4-28 with final three-week interpretation from source. Rotating animation. Urgent proposal deadline.

**Jameson Select**: Digital large-format only. Exclude 3x6. Sandton/Fourways, Ballito/Umhlanga/Durban, Cape Town. Timing mid-August to September as supplied. Hard-format and geography exclusions must survive interpretation.

**Department of Health**: Vaccination-awareness tender. VAT included. No agency commission. Polio/measles/HPV creative rotates on same panels. Parents of under-fives and girls aged 9-14 are distinct audiences. Tender terms, tax/commission constraints, creative rotation, audience separation must reconcile in every version.

**Indlu Properties**: Property campaign spans Soshanguve, Tembisa, Vosloorus plus later Mamelodi supply. Budgets and timing source-controlled. Trust and show-house conversion decline are evidence-linked problems. Multi-location/phased brief, audience, measurement logic must be versioned.

**Multilingual church campaign**: Declining attendance. R4.2m total/R3.4m operative planning context. Five audiences and six languages preserved as source constraints. Budget reconciliation, audience/language coverage, client-confirmed assumptions must appear in plan and proposal.

### Adversarial Anti-Hallucination Suite (Section 31.3)

**Test cases**: Prompt injection, conflicting sources, missing evidence, unsupported demographic claims, retrieval unavailable, stale rate/availability, silent supplier, invalid schema/model output, provider timeout/ambiguous acceptance, cost cap reached, duplicate command/callback, cross-tenant reference, malicious upload, version drift during run, crash/restart, unsafe owner request, UI hidden blocker, model self-certification.

## Ordered Implementation Gates

### Gate Template

**Gate ID**: Stable identifier (G0, G1, G2, etc.)
**Outcome**: User-visible result
**Included requirements**: Exact specification IDs
**Preconditions**: Verified dependencies
**Domain work**: Aggregates, rules, invariants
**Data work**: Migrations, seeds, indexes
**API work**: Commands, queries, schemas
**Agent work**: Tools, policies, evaluations
**UI work**: Routes and complete states
**Integration work**: Adapters, failure behaviour
**Security**: Gate-specific controls
**Tests**: Unit, contract, integration, Playwright
**Fixtures**: Named Advertified cases
**Evidence**: Commands, artefacts, traces, screenshots
**No-go conditions**: Failures that block advancement
**Owner approval**: Named accountable reviewer
**Status**: Not started, active, blocked, verified
**Verdict**: GO/NO-GO with evidence

### Gate 0: Repository Baseline

**Status**: PARTIAL - Requires verification

**Current evidence**: Scaffolding complete, commit SHA ed3bc26d321140ab69b60e5c368017ade92a900e

**Missing evidence**:
- Docker services health verification
- Build execution results
- Test execution results
- Current routes and screenshots
- Legacy disposition register
- Known failures and blockers

**No-go conditions**: All missing evidence must be provided before advancing.

### Gate 1: Architecture Guardrails

**Status**: Not started

**Outcome**: Enforceable architectural boundaries and quality gates

**Included requirements**: Section 17 (technology baseline, guardrails, engineering guardrails)

**Preconditions**: Gate 0 verified

**Domain work**: None

**Data work**: None

**API work**: None

**Agent work**: None

**UI work**: None

**Integration work**: None

**Security**:
- Tenant isolation tests (negative tests required)
- Secret scanning in CI
- Dependency vulnerability scanning
- No credentials in code/prompts

**Tests**:
- Architecture boundary tests (400-line limit, SOLID, no magic strings)
- Contract tests (OpenAPI, events, schemas)
- Security tests (tenant isolation, IDOR, injection)

**Fixtures**: None

**Evidence**:
- CI pipeline passing on violations
- Architecture tests failing on known violations
- Secret scan findings (must be zero)
- Dependency scan report

**No-go conditions**:
- CI does not fail on 400-line violations
- Secret scan finds credentials
- Dependency scan has critical vulnerabilities
- Architecture tests pass on known violations

**Owner approval**: System Architect

**Status**: Not started

**Verdict**: Pending

### Gate 2: Canonical Foundation

**Status**: Not started

**Outcome**: Commercial API with tenant isolation, audit, idempotency, outbox

**Included requirements**: Section 18 (canonical data model), Section 19 (lifecycle state machines), Section 20 (authorisation)

**Preconditions**: Gate 1 verified

**Domain work**:
- Tenant aggregate with settings versioning
- User aggregate with MFA state
- Membership aggregate with role binding
- Value objects: Money, VAT, AuditEvent, IdempotencyRecord
- Audit event system (append-only)
- Idempotency mechanism (key-based deduplication)
- Outbox pattern (transactional event publishing)

**Data work**:
- EF Core migrations for tenant/user/membership
- PostgreSQL tenant isolation (row-level security)
- Indexes on tenant_id + lifecycle/status
- Master data seeds (channels, roles, statuses)
- Audit event table with append-only constraints
- Idempotency record table with unique constraint
- Outbox message table with publishing state

**API work**:
- Commands: CreateTenant, CreateUser, CreateMembership, UpdateSettings
- Queries: GetTenant, GetUser, ListMemberships, GetAuditEvents
- OpenAPI generation and versioning
- Error codes: TENANT_FORBIDDEN, VERSION_CONFLICT, DUPLICATE_REQUEST
- Correlation ID propagation

**Agent work**: None

**UI work**: None

**Integration work**: None

**Security**:
- Tenant predicate on every protected query
- Database row-level security for high-risk tables
- Authorisation re-check on every command
- Audit event for every consequential action
- No tenant claims from browser trusted

**Tests**:
- Domain unit tests (invariants, value objects)
- Application tests (commands, permissions, idempotency)
- Integration tests (PostgreSQL, migrations)
- Security tests (tenant isolation, negative tests)
- Migration tests (empty upgrade, rollback)

**Fixtures**:
- Two tenants with similar IDs (cross-tenant test)
- User with multiple memberships
- Audit event verification

**Evidence**:
- E2E-01 (Tenant isolation) passing
- Audit events recorded for all actions
- Idempotency prevents duplicate operations
- Outbox publishes events reliably
- Migration tests pass (empty and upgrade)
- OpenAPI spec generated and versioned

**No-go conditions**:
- Cross-tenant read/write possible
- Audit events missing for consequential actions
- Idempotency allows duplicate operations
- Migrations fail rollback
- OpenAPI not generated

**Owner approval**: System Architect + Security Lead

**Status**: Not started

**Verdict**: Pending

### Gate 3: Authenticated Shell

**Status**: Not started

**Outcome**: Sign-in, workspace selection, role dashboards, route guards

**Included requirements**: Section 8 (authenticated experience blueprint), Section 20 (authorisation matrix), Section 24 (screen implementation contracts)

**Preconditions**: Gate 2 verified

**Domain work**: None (uses Gate 2 domain)

**Data work**: None

**API work**:
- Commands: SignIn, AcceptInvitation, CompleteTask
- Queries: GetMe, ListWorkspaces, GetHome, ListTasks, GetNotifications
- OIDC integration
- Session management
- Permission resolution

**Agent work**: None

**UI work**:
- `/sign-in` - Sign in securely
- `/invite/:token` - Accept invitation and set up access
- `/workspaces` - Choose authorised organisation
- `/home` - Role-specific KPIs and priority queue
- `/tasks` - Approvals and exceptions
- `/notifications` - Changes requiring attention
- `/profile` - Manage personal settings and security
- Route guards for all protected routes
- Error/accessibility states for all screens

**Integration work**:
- OIDC provider adapter
- Email adapter (Resend)
- Session storage

**Security**:
- OIDC with secure HttpOnly cookie or bearer token
- Session expiry and refresh
- MFA capability for privileged roles
- Account lock/revocation
- Audited invitation

**Tests**:
- Contract tests (OIDC, session, permissions)
- Integration tests (OIDC provider, email)
- Playwright tests (auth journeys, role dashboards)
- Accessibility tests (WCAG 2.2 AA)
- Security tests (session hijacking, CSRF)

**Fixtures**:
- Multi-role user login
- Workspace selection
- Role dashboard KPIs
- Task completion
- Notification handling

**Evidence**:
- E2E-01 (Tenant isolation) passing
- E2E-11 (Role dashboards) passing
- Users can sign in and select workspaces
- Role dashboards show correct KPIs
- Route guards prevent unauthorized access
- Error states handled gracefully
- Accessibility checks pass (WCAG 2.2 AA)
- Playwright journeys with screenshots

**No-go conditions**:
- Cross-tenant access possible
- Route guards bypassable
- Role dashboards show incorrect data
- Accessibility critical issues
- Session security vulnerabilities

**Owner approval**: Product Owner + Security Lead

**Status**: Not started

**Verdict**: Pending

[Continuing with remaining gates in same structured format...]

## Cross-Cutting Security, Accessibility and Observability

### Security (Continuous from Gate 1)

**Gate-level exit criteria**:
- Tenant isolation: Negative tests must pass
- Authorisation: API re-authorises every command
- Audit: 100% of consequential actions auditable
- File safety: Malware scan, type validation, quarantine
- Secret handling: Managed secrets, rotation, no plaintext
- AI: Prompt/tool allow-list, tenant-scoped retrieval, evidence policy

### Accessibility (Continuous from Gate 3)

**Gate-level exit criteria**:
- WCAG 2.2 AA target for authenticated workflows
- Keyboard navigation, clear focus, semantic headings
- Automated and manual keyboard/screen-reader checks
- No critical accessibility issues in gate verification

### Observability (Continuous from Gate 1)

**Gate-level exit criteria**:
- Structured logs with correlation IDs
- Metrics for API latency, job pickup, inventory freshness
- Traces from user action to business outcome
- Alerts for critical failures, cost spikes, security events

## Migration and Legacy Disposition

### Legacy Disposition Register

**Current legacy systems**: None identified (clean build)

**Legacy code status**: Read-only reference only (never the release gate)

**Migration data**: None (clean build)

**Legacy full suite**: Not the new release gate (record disposition separately)

## Environments and Deployment

### Environment Profiles

**Local**: Docker Compose, deterministic providers, synthetic data, local object storage. No production secrets or network access.

**CI**: Ephemeral database/services, deterministic tests, builds, scans, generated-contract checks. No paid provider by default.

**Staging**: Production-like containers and managed dependencies, synthetic or approved masked data. Deterministic/sandbox providers; live Bedrock disabled during certification.

**Production**: AWS af-south-1, TLS ingress, web/API/runtime/workers, managed PostgreSQL, S3, managed secrets, telemetry. Least privilege, backups, alerting, change approval.

### Deployment Topology

**Edge**: CloudFront + ALB, DNS/TLS, request size/rate controls, security headers.

**Web**: React/Vite immutable static build through CloudFront, environment-safe API origin, no embedded secrets.

**Commercial API**: C#/.NET container on ECS/Fargate, restartable instances, separated health/readiness, only canonical write boundary.

**Agent runtime/workers**: Python/FastAPI AgentCore-compatible runtime and owned workers on ECS/Fargate, bounded concurrency, checkpoints, provider circuit breakers.

**Events/workflows**: Commercial outbox → EventBridge/SQS; Step Functions only for approved coarse-grained durable workflow coordination.

**PostgreSQL**: Managed production service with encryption, point-in-time recovery, restricted network, migration role.

**Object storage**: Versioning, encryption, lifecycle policy, private access, malware quarantine prefix.

**Secrets**: Managed store, per-service IAM, rotation procedure, no secret in image/log/browser.

**Telemetry**: Central structured logs, metrics, traces, dashboards, alerts correlated to business action.

## Release Certification

### Certification Area (Section 28.4)

**AI provider**: Zero live or paid Bedrock calls during redevelopment and certification; deterministic surrogate proves equivalent contracts and recovery.

**Quality cycle**: Each material defect follows Agent/implementation → QA → correction → QA retest with retained evidence.

**Durability**: Docker/container restart, route change, worker crash, provider timeout tests resume from persisted checkpoints without duplicates.

**Artefacts**: Every final DOCX/PDF/PPTX is rendered and visually inspected; exact accepted version, input versions, hash retained.

**Commercial truth**: Only approved inventory/pricing and client-accepted sites/lines are booked or invoiced; no invented response or silent substitution.

**Team sign-off**: Named Product, Engineering, QA, Commercial, Inventory Operations, Security/Privacy, Operations reviewers all sign GO.

### Thirty-Case Zero-Bedrock Certification

**Cohorts**:
- Rapid OOH: 10 genuine briefs reaching accepted exact immutable shortlist/proposal version
- Full multi-channel: 10 genuine briefs reaching accepted exact immutable proposal option, approved plan, premium branded PDF
- Unbriefed discovery: 10 genuine opportunities including Rayetsa Furniture reaching approved complete BriefVersion, plan, accepted proposal

**Total**: 30 accepted cases (not generated demos, real owner/UAT acceptance, canonical audit, inventory/pricing evidence, final artefact retained)

**Decision**: One unresolved NO-GO, missing reviewer, failed gate, or unverifiable case means overall NO-GO.

## Ownership, Estimates and Dependency Plan

### Roles and Available Capacity

**To be defined**: Team size, individual capacity, external dependencies

### Work Packages and Dependency Path

**Sequential dependencies**:
- Gate 0 → Gate 1 → Gate 2 → Gate 3 (foundation)
- Gate 2 → Gate 4 → Gate 5 (evidence and brief)
- Gate 2 → Gate 6 (inventory can progress in parallel)
- Gate 5 → Gate 7 → Gate 8 (planning and proposal)
- Gate 6 → Gate 10 (supplier marketplace)
- Gate 8 → Gate 9 (Rapid OOH)
- Gate 8 → Gate 11 (campaign delivery)
- All gates → Gate 12 (hardening)
- Gate 12 → Gate 13 (production launch)

**Parallel opportunities**:
- Gate 6 (inventory) can progress alongside Gate 4-5 (opportunity/brief)
- Gate 3 (authenticated shell) can progress alongside Gate 2 (canonical foundation)
- Observability, accessibility, security must be continuous
- Test fixtures and evaluation corpora must be created before agent implementation

### Estimates by Deliverable

**To be defined**: Specific estimates by gate, confidence ranges, external lead times

### Timeline Status

**Current label**: Unvalidated planning range (28-38 weeks has no team size, capacity, assumptions, external lead times, or uncertainty model)

**Requirement**: Credible timeline must state roles, available capacity, work packages, estimates by deliverable, confidence range, external approval lead times, provider integration uncertainty, data-labelling effort, security/UAT time, production stabilisation period.

## Risk and Decision Register

### Current Risks

**High-risk areas** (from specification):
- AI provider integration complexity
- Inventory extraction for unseen files
- Data migration from legacy systems
- Large catalogue performance

**Mitigation strategies** (from specification):
- Deterministic provider for testing, gradual rollout
- Extensive labelled corpus, gradual channel support
- Clean build approach, legacy as read-only reference
- Server-side pagination, indexing, virtualization

### Decision Register

**Approved ADRs**:
- ADR-0001: Use Amazon Bedrock AgentCore for AI Agent Orchestration

**Pending decisions**: None currently

## AI Execution Protocol

### Pre-Flight Requirements (Section 31.5)

**Before mutation**:
- Read entire v1.1 document and repository instructions
- Inventory current branch, user changes, services, routes, schemas, migrations, tests, environment
- Produce capability and historical-traceability ledgers

**Before each gate**:
- List exact requirements and adversarial cases affected
- Identify canonical owner, permitted tools, input versions, acceptance evidence, cost ceiling, human approvals, rollback/recovery path

**During implementation**:
- Make smallest coherent vertical change
- Preserve unrelated work
- Use typed contracts and authorised commands
- Record assumptions and decisions
- Never create temporary parallel truth or client-specific shortcut

**After each change**:
- Inspect diff
- Run targeted unit/contract/integration tests
- Run affected builds
- Run relevant authenticated Playwright journey
- Render any generated document
- Update ledgers with exact evidence

**When blocked**:
- Finish safe provider-neutral work
- State precise missing credential, commercial/legal choice, external approval, or failing check
- State smallest owner action
- Do not invent, bypass, or broaden authority

**Before completion claim**:
- Reconcile every row in Sections 28-31
- Confirm no unresolved NO-GO
- Include exact commands/results and retained artefacts
- Obtain independent named reviewer approval

### Non-Negotiable Containment Rule

**AI agent is never the authority for commercial truth, production readiness, or its own correctness.** It may implement and propose; canonical commands, deterministic validation, evidence, authorised humans, and independent release gates decide. A missing fact stays unknown, a failed gate stays failed, and an unverified capability stays incomplete.

## Completion Report Template

### Report Sections

**Release**: Commit/branch, images, migration range, environment, release timestamp

**Capabilities**: Gate-by-gate ledger with implemented and verified evidence; no ambiguous percentage

**Changes**: Domain, API, agents, screens, integrations, data, operations changed

**Verification**: Exact commands and results for builds, tests, evaluations, Playwright, security, accessibility, restore

**Data**: Migration result, master-data version, inventory corpus result, canonical record counts

**AI cost**: Provider/model attempts and final incremental cost by workflow; deterministic runs identified

**Risks**: Known limitations, deferred approved scope, operational risks, owner

**Blockers**: Only unresolved owner/credential/provider blockers, with smallest next action

**Production**: Deployment health, smoke tests, dashboards, alerts, backup, rollback, incident owner

---

## Status Summary

**Document version**: 2.0 (Complete rewrite)
**Last updated**: 2026-08-29
**Next action**: Complete Gate 0 verification before advancing to Gate 1
**Overall verdict**: Gate 0 PARTIAL - scaffolding complete, verification pending