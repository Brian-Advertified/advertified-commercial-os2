# Complete Domain Model - All Aggregates

## Core Commercial Aggregates

### Tenant
**Purpose**: Multi-tenancy root with configuration and isolation
**Invariants**: Every protected row carries tenant_id or parent tenant reference
**Lifecycle**: Active, Suspended, Archived
**Versioning**: Settings versioned with effective dates
**Ownership**: platform_admin manages cross-tenant; others scoped to assigned

### User
**Purpose**: Authentication and identity management
**Invariants**: Case-insensitive unique email, no provider password in domain tables
**Lifecycle**: Active, Locked, Archived
**Security**: MFA state, last login tracking, session management
**Ownership**: Self-managed profile data, admin-managed access

### Membership
**Purpose**: User-tenant-role binding with authorization
**Invariants**: Unique tenant/user combination, role from canonical registry
**Lifecycle**: Pending, Active, Revoked
**Audit**: Invited by, invited at, accepted at
**Ownership**: Tenant admin manages memberships

### ClientAccount (Advertiser)
**Purpose**: Client/Advertiser commercial entity
**Invariants**: Tenant-scoped unique external reference, billing profile required
**Lifecycle**: Active, Suspended, Archived
**Commercial**: Legal name, trading name, website, industry, billing profile
**Ownership**: Agency admin manages assigned advertisers; advertiser admin manages own

### Agency
**Purpose**: Agency entity managing multiple advertisers
**Invariants**: Tenant-scoped unique external reference
**Lifecycle**: Active, Suspended, Archived
**Commercial**: Legal name, trading name, agency type, commission structure
**Ownership**: Agency admin manages own agency

### Contact
**Purpose**: Contact information for business relationships
**Invariants**: Purpose-limited contact data, consent basis required
**Lifecycle**: Active, Inactive, Archived
**POPIA**: Consent basis, purpose limitation, retention policy
**Ownership**: Associated entity manages own contacts

## Evidence and Opportunity Aggregates

### Opportunity
**Purpose**: Prospect or commercial opening requiring discovery
**Invariants**: Stage transition only through commands, source evidence required
**Lifecycle**: CREATED, QUALIFYING, EVIDENCE_REVIEW, STRATEGY_READY, BRIEF_READY, PLANNING, PROPOSAL_READY, WON, LOST, ARCHIVED
**Commercial**: Expected value, currency, deadline, problem/objective summaries
**Evidence**: Links to EvidenceSources and EvidenceItems
**Ownership**: internal_planner owns assigned opportunities

### EvidenceSource
**Purpose**: Immutable source of evidence (file, URL, upload)
**Invariants**: Deduplicate by tenant/type/hash, original is immutable
**Lifecycle**: Captured, Quarantined, Approved, Rejected
**POPIA**: Policy basis, retention according to purpose
**Content**: URI/object key, title, content hash, captured at, type
**Ownership**: Any role with evidence capture permission

### EvidenceItem
**Purpose**: Structured evidence extracted from sources
**Invariants**: Approved before material agent use, source locator required
**Lifecycle**: Draft, In Review, Approved, Rejected
**Evidence**: Source reference, locator, claim type, structured value, confidence
**Review**: Review status, reviewer, reviewed at, decision
**Ownership**: inventory_ops reviews evidence; others can submit

### BusinessInterpretation
**Purpose**: AI-generated interpretation of business model and customers
**Invariants**: Based on approved evidence only, hypotheses labelled explicitly
**Lifecycle**: Draft, Submitted, Approved, Rejected
**Content**: Business model, customer groups, occasions, geography, unknowns, hypotheses
**Evidence**: Links to approved EvidenceItems
**Ownership**: internal_planner submits, designated approver approves

### OpportunityAngle
**Purpose**: Ranked opportunity angles from Opportunity Intelligence agent
**Invariants**: Only approved evidence, rejected angles retained
**Lifecycle**: Generated, Selected, Rejected, Superseded
**Content**: Title, rationale, evidence links, confidence
**Selection**: Selected at, selected by, current status
**Ownership**: internal_planner selects angle

## Brief and Planning Aggregates

### CampaignBrief
**Purpose**: Working aggregate for campaign intent
**Invariants**: Contains immutable BriefVersions, never silently rewrites approved versions
**Lifecycle**: DRAFT, IN_REVIEW, APPROVED, STALE, ARCHIVED
**Version Pointers**: Current draft version ID, current approved version ID
**Required**: Business problem, objective, audiences, geography, timing, budget, VAT, fees, constraints, measurement, unknowns, assumptions, evidence, approval
**Ownership**: internal_planner or agency_admin creates; advertiser_approver approves

### BriefVersion
**Purpose**: Immutable campaign intent version
**Invariants**: Immutable after submission, approved versions never edited
**Lifecycle**: DRAFT, IN_REVIEW, APPROVED, REJECTED
**Change**: Material change creates new draft version
**Content**: Business problem, objective, audiences, geography, language, age/life-stage, timing, budget, VAT status, fees, constraints, measurement, unknowns, assumptions, evidence IDs
**Evidence**: Links to approved EvidenceItems
**Ownership**: Creator submits, approver approves

### StrategyVersion
**Purpose**: Commercial growth thesis and direction
**Invariants**: Requires critic report and approval, evidence-linked
**Lifecycle**: DRAFT, IN_REVIEW, APPROVED, REJECTED
**Content**: Diagnosis, growth thesis, objectives, audiences, proposition, message, channel implications, risks
**Evidence**: Links to approved EvidenceItems
**Critic**: Critic report attached before approval
**Ownership**: internal_planner creates, designated approver approves

### AudienceDefinition
**Purpose**: Evidence-backed audience segment definitions
**Invariants**: Fact/inference/hypothesis classification required, never infer individual sensitive attributes
**Lifecycle**: Draft, In Review, Approved, Rejected, Stale
**Content**: Name, description, need state, buying context, geography, language, age/life-stage, lawful aggregate demographic evidence, exclusions, evidence links, confidence
**Classification**: Fact, inference, hypothesis explicitly labelled
**Ownership**: Audience agent creates, approver reviews

### MediaMixVersion
**Purpose**: Channel roles and budget allocation
**Invariants**: Allocation sum equals approved planning budget
**Lifecycle**: DRAFT, IN_REVIEW, APPROVED, REJECTED, STALE
**Content**: Total budget, allocations, channel roles, assumptions, evidence links
**Validation**: Allocation reconciliation to budget
**Ownership**: Media Planning agent creates, designated approver approves

## Inventory Aggregates

### Supplier
**Purpose**: Supplier entity owning inventory and operations
**Invariants**: Supplier controls only own organisation, verification level tracked
**Lifecycle**: Active, Suspended, Archived
**Commercial**: Legal name, trading name, contacts, verification level, payment terms
**Ownership**: supplier_admin manages own supplier

### InventoryImport
**Purpose**: Inventory file import pipeline tracking
**Invariants**: Same hash is idempotent unless explicit reprocess
**Lifecycle**: UPLOADED, CLASSIFYING, EXTRACTING, VALIDATING, REVIEW_REQUIRED, PUBLISHING, COMPLETED, FAILED
**Content**: Supplier, source object key, hash, document class, pipeline status, schema version, counts, error summary
**Idempotency**: Hash-based deduplication
**Ownership**: inventory_ops manages imports

### InventoryDocument
**Purpose**: Document classification and routing
**Invariants**: Detected document class with confidence, routing decision
**Lifecycle**: Classified, Routed, Extracted, Failed
**Content**: Document class, confidence, parsing strategy, routing decision
**Ownership**: System automated classification

### InventoryAsset
**Purpose**: Extracted assets (logos, images, creative files)
**Invariants**: Source locator retained, review status tracked
**Lifecycle**: Extracted, In Review, Approved, Rejected
**Content**: Object key, asset type, mime type, hash, dimensions, source locator, review status
**Rights**: Logo/image rights and source retained
**Ownership**: inventory_ops reviews assets

### InventoryProduct
**Purpose**: Verified inventory product
**Invariants**: Supplier controls only own organisation, evidence-linked rates/availability
**Lifecycle**: Draft, In Review, Published, Superseded, Archived
**Content**: Supplier, channel, product type, name, description, geography, attributes, verification status, lifecycle status
**Evidence**: Source locators, field locators, captured at, reviewed at, reviewer, verification level, confidence
**Freshness**: Rate confirmed at, availability confirmed at, expires at, stale reason, supplier confirmation status
**Ownership**: supplier_admin creates own listings; inventory_ops reviews

### InventoryRate
**Purpose**: Versioned pricing information
**Invariants**: No overlapping active rate for same rate key, evidence-linked
**Lifecycle**: Draft, Active, Expired, Superseded
**Content**: Product, rate type, amount, currency, VAT status, commission, valid from, valid to, evidence item ID, status
**Validation**: No overlapping active rates
**Ownership**: supplier_admin manages own rates

### InventoryAvailability
**Purpose**: Availability windows and capacity
**Invariants**: Stale or expired availability cannot be silently planned
**Lifecycle**: Draft, Confirmed, Expired, Stale
**Content**: Product, window start, window end, status, capacity, source, confirmed at, expires at
**Freshness**: Confirmation tracking and expiry
**Ownership**: supplier_admin manages own availability

### InventoryShortlistVersion
**Purpose**: Eligible inventory selection for planning
**Invariants**: Only eligible inventory may be selected, exact retrieval versions retained
**Lifecycle**: Draft, In Review, Approved, Stale
**Content**: Brief version, candidate scores, rejection reasons, embedding version IDs, assumptions, status
**Selection**: Only eligible inventory, rejection reasons retained
**Ownership**: Inventory Intelligence agent creates, planner selects

## Planning and Proposal Aggregates

### MediaPlanVersion
**Purpose**: Executable planning recommendation
**Invariants**: Approved input versions frozen by reference, material change invalidates
**Lifecycle**: DRAFT, IN_REVIEW, APPROVED, STALE
**Content**: Brief version, mix version, totals, forecast, assumptions, supply status, status
**Reconciliation**: Selected option controls downstream supply
**Validation**: Totals reconcile to mix and budget
**Ownership**: Media Planning agent creates, designated approver approves

### MediaPlanLine
**Purpose**: Individual placement in media plan
**Invariants**: Typed calculations reconcile to plan totals, selected option controls downstream supply
**Lifecycle**: Draft, Confirmed, Cancelled
**Content**: Plan version, inventory product, rate, availability, dates, quantity, supplier cost, client price, fees, VAT, forecast
**Reconciliation**: Must reconcile to plan totals
**Ownership**: Part of MediaPlanVersion aggregate

### ProposalVersion
**Purpose**: Client-facing commercial offer
**Invariants**: Three materially different tiers, cannot approve if plan/pricing changed
**Lifecycle**: DRAFT, IN_REVIEW, APPROVED, SENT, SELECTED, DECLINED, EXPIRED
**Content**: Brief version, plan version IDs, title, executive summary, terms, expiry at, status, document asset ID
**Validation**: Cannot approve if referenced artefact changed
**Selection**: Exact immutable option controls bookings/invoicing
**Ownership**: Proposal agent creates, advertiser_approver approves

### ProposalTier
**Purpose**: Distinct proposal options
**Invariants**: Distinct scope and budget, account-configured labels
**Content**: Proposal version, name, budget, outcomes, included plan version, display order
**Validation**: Materially different from other tiers
**Ownership**: Part of ProposalVersion aggregate

## Supplier Interaction Aggregates

### SupplierRequest (RFQ)
**Purpose**: Request for quote to suppliers
**Invariants**: External send requires named human approval
**Lifecycle**: DRAFT, SENT, RESPONSE_RECEIVED, COMPLETED, CANCELLED
**Content**: Tenant, supplier, brief, requested items, due at, status
**Approval**: Human approval required before external send
**Ownership**: internal_planner creates, supplier responds

### SupplierResponse
**Purpose**: Supplier response to RFQ
**Invariants**: Material changes invalidate affected plan lines
**Lifecycle**: Received, In Review, Accepted, Rejected
**Content**: RFQ, terms, rates, availability, evidence IDs, received at, review status
**Impact**: Material changes invalidate affected plans
**Ownership**: supplier_admin submits

## Campaign Delivery Aggregates

### Booking
**Purpose**: Supplier commitment and delivery tracking
**Invariants**: Supplier commitment only after approved command
**Lifecycle**: DRAFT, PENDING_SUPPLIER, CONFIRMED, CANCELLED, COMPLETED
**Content**: Tenant, proposal, supplier, terms, amount, status, confirmed at, cancellation reason
**Approval**: Supplier terms accepted by authorised human
**Ownership**: internal_planner creates, supplier confirms

### Campaign
**Purpose**: Campaign execution and delivery tracking
**Invariants**: Status follows approved workflow, creative and proof requirements
**Lifecycle**: PLANNED, BOOKED, CREATIVE_PENDING, READY, LIVE, COMPLETED, CANCELLED
**Content**: Tenant, brief, proposal, status, start at, end at, owner, measurement plan
**Dependencies**: Creative approval, proof submission, delivery confirmation
**Ownership**: internal_planner manages

### CreativeAsset
**Purpose**: Creative content for campaigns
**Invariants**: Versioned, approval required before publication
**Lifecycle**: Draft, Submitted, In Review, Approved, Rejected
**Content**: Campaign, format, object key, version, approval status, rights, supplier status
**Approval**: Publication requires approved version
**Ownership**: Creative team creates, approver approves

### DeliveryProof
**Purpose**: Evidence of campaign delivery
**Invariants**: Evidence retained and reviewer attributable
**Lifecycle**: Submitted, In Review, Approved, Rejected
**Content**: Campaign, booking, type, object key, captured at, location, review status
**Evidence**: Source and limitations visible
**Ownership**: Supplier submits, reviewer approves

### PerformanceMetric
**Purpose**: Campaign performance data
**Invariants**: Source and limitations required, quality status tracked
**Lifecycle**: Imported, Validated, Approved, Rejected
**Content**: Campaign, metric, value, unit, period, source, evidence ID, quality status
**Validation**: Source and limitations disclosed
**Ownership**: Measurement system imports

## Financial Aggregates

### PurchaseOrder
**Purpose**: Client commitment and billing foundation
**Invariants**: Accepted proposal plus signed PO required before invoicing
**Lifecycle**: DRAFT, SUBMITTED, APPROVED, CANCELLED
**Content**: Tenant, proposal version, selected option, PO number, object key, amount, currency, status, approved by, approved at
**Validation**: Reconciles to selected proposal option
**Ownership**: advertiser_admin submits, commercial approver approves

### PaymentIntent
**Purpose**: Payment processing and tracking
**Invariants**: Method is VODAPAY, MANUAL_EFT, or ADVERTISE_NOW_PAY_LATER
**Lifecycle**: DRAFT, PENDING, CONFIRMED, FAILED, CANCELLED
**Content**: Tenant, proposal version, purchase order, method code, amount, currency, status, external reference, expires at
**Methods**: VODAPAY, MANUAL_EFT, ADVERTISE_NOW_PAY_LATER
**Ownership**: Payment processing system

### Invoice
**Purpose**: Billing and revenue recognition
**Invariants**: Commission calculated after discounts, exact accepted option controls lines
**Lifecycle**: DRAFT, ISSUED, PAID, OVERDUE, CANCELLED
**Content**: Tenant, proposal version, purchase order, invoice number, subtotal, discount, commission, VAT, total, status
**Calculation**: Commission calculated after discounts
**Ownership**: Finance system generates

## System and Operations Aggregates

### AgentRun
**Purpose**: AI agent execution tracking
**Invariants**: Durable state outside model provider, input versions frozen
**Lifecycle**: QUEUED, RUNNING, WAITING_FOR_HUMAN, COMPLETED, FAILED
**Content**: Tenant, workflow type, resource reference, status, input version, provider policy, correlation ID, started at, completed at
**Checkpoint**: Durable state for resume capability
**Ownership**: Agent runtime manages

### ToolInvocation
**Purpose**: Individual tool execution within agent run
**Invariants**: Authorised, idempotent, auditable
**Lifecycle**: Queued, Running, Completed, Failed
**Content**: Run ID, step ID, tool name, schema version, input hash, status, attempt, started at, completed at, result reference
**Audit**: Every tool call recorded
**Ownership**: Part of AgentRun aggregate

### AIUsageLedger
**Purpose**: AI cost tracking and budget control
**Invariants**: One ledger row per provider attempt, cost visible per proposal
**Lifecycle**: Recorded, Verified, Disputed
**Content**: Run ID, step ID, provider, model, input units, output units, currency, incremental cost, cache status
**Budget**: Per-run and per-tenant caps enforced
**Ownership**: System records automatically

### HumanTask
**Purpose**: Human decision and exception management
**Invariants**: Represents real decision, not navigation link
**Lifecycle**: Assigned, In Progress, Completed, Cancelled, Escalated
**Content**: Tenant, type, resource reference, assignee, priority, due at, status, action schema, completed at
**Action**: One clear business action and recovery path
**Ownership**: System creates, human completes

### Notification
**Purpose**: Communication of required actions and events
**Invariants**: Idempotent, recipient resolved, channel policy applied
**Lifecycle**: Queued, Sent, Delivered, Failed
**Content**: Recipient, channel, type, content, status, delivery attempts
**Channels**: In-app, email (human-confirmed external)
**Ownership**: Notification system manages

### AuditEvent
**Purpose**: Append-only business event logging
**Invariants**: No private chain-of-thought, consequential actions only
**Lifecycle**: Recorded (immutable)
**Content**: Tenant, actor, action, resource reference, correlation ID, occurred at, outcome, metadata
**Constraints**: No private reasoning, no secrets in logs
**Ownership**: System records automatically

### OutboxMessage
**Purpose**: Transactional event publishing
**Invariants**: Written in same transaction as business state
**Lifecycle**: Created, Published, Failed
**Content**: Event type, aggregate reference, payload version, payload, occurred at, published at, attempts
**Reliability**: At-least-once processing with idempotent effects
**Ownership**: Commercial API manages

### DocumentArtifact
**Purpose**: Generated documents (proposals, reports)
**Invariants**: Generated from approved structured artefacts only
**Lifecycle**: Generated, In Review, Approved, Superseded
**Content**: Type, object key, version, source artefacts, generated at, status
**Generation**: Only from approved structured artefacts
**Ownership**: Document generation system

## Stale Downstream Rules

### Invalidation Triggers
**Evidence changes**: Invalidate dependent strategies, briefs, plans
**Rate changes**: Invalidate dependent plans and proposals
**Availability changes**: Invalidate dependent plans and proposals
**Plan changes**: Invalidate dependent proposals
**Brief changes**: Invalidate dependent strategies, plans, proposals

### Stale Status
**Aggregate becomes stale when**: Referenced input version materially changed
**Human notification**: Owner notified of stale status
**Recovery**: Human must review and reapprove or regenerate

## Ownership Summary

**Cross-tenant management**: platform_admin
**Internal planning**: internal_planner, inventory_ops
**Agency operations**: agency_admin, agency_campaign_user
**Advertiser operations**: advertiser_admin, advertiser_approver
**Supplier operations**: supplier_admin, supplier_user
**Influencer operations**: influencer_rep
**System operations**: agent_runtime_service, worker_service