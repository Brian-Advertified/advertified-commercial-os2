# Advertified domain model design register

**Status:** DRAFT — NOT COMPLETE OR IMPLEMENTED  
**Canonical product source:** Sections 5, 18, 19, 21, and 25 in `docs/spec/`  
**Named domain owner:** UNASSIGNED

The former filename/heading overstated completion. This file is a bounded design index. Gate 1 defines contracts; Gate 2 introduces the first migrations. No aggregate listed here currently exists in production code or PostgreSQL.

## Model rules

- The C# Commercial API is the canonical owner.
- Every protected aggregate is tenant-scoped directly or through an immutable parent.
- State changes occur through authorised, idempotent commands—not public property mutation.
- Submitted/approved artefacts are immutable versions.
- Approvals bind an exact version, actor, role, time, decision, and rationale.
- Evidence, unknowns, hypotheses, assumptions, and confirmations are distinct.
- Money uses integer minor units plus governed ISO currency.
- Rates/availability/pricing have source, version, effective/freshness dates.
- Consequences emit audit and outbox records atomically.
- Aggregate state machines are separate; the shared master-data list is vocabulary, not one universal transition graph.

## Identity and tenancy

| Aggregate | Responsibility | Key invariant |
|---|---|---|
| Tenant | Security/commercial boundary | No protected operation without resolved tenant |
| User | Provider-linked identity | No provider password in domain tables |
| Membership | User/tenant/role binding | Active, time-valid and uniquely scoped |
| Agency | Agency organisation | Tenant and external-reference uniqueness |
| ClientAccount | Advertiser/commercial account | Billing and policy versions explicit |
| Contact | Purpose-limited relationship data | Lawful purpose and retention metadata |

## Evidence, opportunity, and Brief

| Aggregate/artefact | Responsibility | Key invariant |
|---|---|---|
| Opportunity | Unbriefed discovery path | Never required for a supplied Brief |
| EvidenceSource | Immutable file/URL capture | Hash, locator, policy, retention |
| EvidenceItem | Structured claim | Material use requires review state and source |
| EvidenceSet | Versioned approved evidence | Exact items bound to downstream input |
| BusinessInterpretation | Evidence-backed interpretation | Hypotheses and unknowns explicit |
| OpportunityAngleSet | Ranked credible opportunities | Human selects/rejects exact version |
| StrategyVersion | Growth/communications strategy | Selected angle precedes strategy |
| CriticObjection | Immutable challenge | Human resolution required by severity |
| CampaignBrief | Working aggregate | Source path/lineage retained |
| BriefVersion | Immutable Brief snapshot | Approval binds exact version |

## Inventory and supply

| Aggregate/artefact | Responsibility | Key invariant |
|---|---|---|
| SupplierAccount | Supplier boundary | Supplier sees own scope only |
| SourceFile | Immutable quarantined upload | Original retained by policy; safe processing |
| ImportRun | Durable extraction workflow | Checkpointed, resumable, auditable |
| InventoryCandidate | Pre-publication suggestion | Never searchable/bookable supply |
| InventoryProduct | Approved media product | Channel extension plus evidence |
| RateVersion | Commercial rate | Source/effective/freshness and typed money |
| AvailabilityVersion | Supply state | Time-bound and source-confirmed |
| RFQ/Response | Supply request/answer | Silence never equals confirmation |
| ShortlistVersion | Eligible ranked candidates | Rejections retained for every excluded item |

## Planning, proposal, and delivery

| Aggregate/artefact | Responsibility | Key invariant |
|---|---|---|
| AudienceDefinition | Evidence-backed audience | Sensitive inference policy enforced |
| MediaMixVersion | Approved channel/budget role | Approval before expensive supply work |
| MediaPlanVersion | Selected inventory/flighting/cost | Deterministic reconciliation and critic |
| ProposalVersion | Client options/narrative/pricing | Bound to exact approved plan/rates |
| ClientDecision | Select/decline/expire | Named human and exact proposal |
| FundingRecord | Funding readiness | Verified before booking |
| Booking | Supplier commitment | Approved plan and confirmed supply |
| Creative/AssetVersion | Approved material | Exact format/version and publication gate |
| CampaignRun | Live delivery lifecycle | Readiness before live |
| ProofItem | Evidence of delivery | Source/time/location and review |
| MeasurementVersion | Performance interpretation | Data completeness and assumptions explicit |
| LearningRecord | Approved reusable learning | Tenant/purpose scope and provenance |

## Platform records

CommandReceipt, IdempotencyRecord, AuditEvent, OutboxMessage, InboxReceipt, JobExecution, HumanTask, Notification, IntegrationDelivery, AgentRun, AgentStep, AgentCheckpoint, AIUsageLedger, and PolicyVersion support reliability and governance. They are not alternate commercial truth.

Each gate packet must turn only its in-scope rows into detailed contracts, migrations, commands, tests, and evidence. Unspecified fields or transitions remain blocked, not inferred.
