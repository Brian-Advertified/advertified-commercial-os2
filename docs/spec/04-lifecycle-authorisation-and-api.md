# 19. Lifecycle state machines

**Transition rule:** State changes occur only through named Commercial API commands. Each accepted command validates role, tenant, current version, required evidence and guards; writes the new state, audit event and outbox message in one transaction; and returns the resulting representation.

| **Entry journey**       | **One commercial spine**                                                                                                                                                                        | **Routing rule**                                                                 |
|-------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------|
| Discovery/unbriefed     | Advertified finds or receives a prospect, captures approved evidence, interprets the business, proposes opportunity/strategy and creates a complete reviewed draft BriefVersion                 | Human approves evidence, strategy and Brief before planning                      |
| Brief-led full campaign | User uploads/pastes original brief; system understands it, preserves source, raises unknowns, creates canonical BriefVersion, then strategy, audience, mix, plan and proposal                   | User is not forced to choose implementation details                              |
| Rapid OOH               | System identifies a genuine rapid OOH need from the Brief, resolves geography/routes/POIs, evaluates OOH, allows provisional availability-subject shortlist and produces editable shortlist/PDF | User does not choose the technical path; internal owner may override with reason |

## 19.1 Opportunity lifecycle

| **From**                | **Command**             | **Guard**                                                                 | **To**          | **Event**                       |
|-------------------------|-------------------------|---------------------------------------------------------------------------|-----------------|---------------------------------|
| CREATED                 | StartQualification      | owner assigned and at least one permitted source                          | QUALIFYING      | OpportunityQualificationStarted |
| QUALIFYING              | SubmitEvidenceForReview | capture completed or reviewer can inspect failures                        | EVIDENCE_REVIEW | OpportunityEvidenceSubmitted    |
| EVIDENCE_REVIEW         | ApproveEvidenceSet      | material items reviewed; unresolved gaps explicitly recorded              | STRATEGY_READY  | OpportunityEvidenceApproved     |
| STRATEGY_READY          | ApproveStrategy         | StrategyVersion approved and no unresolved critical critic item           | BRIEF_READY     | OpportunityStrategyApproved     |
| BRIEF_READY             | ApproveBrief            | BriefVersion approved                                                     | PLANNING        | OpportunityBriefApproved        |
| PLANNING                | ApproveProposal         | proposal references approved current plan and commercial totals reconcile | PROPOSAL_READY  | OpportunityProposalApproved     |
| PROPOSAL_READY          | MarkWon                 | selected tier and commercial owner confirmation                           | WON             | OpportunityWon                  |
| CREATED..PROPOSAL_READY | MarkLost                | reason required                                                           | LOST            | OpportunityLost                 |
| WON/LOST                | Archive                 | no open consequential task                                                | ARCHIVED        | OpportunityArchived             |

## 19.2 Versioned artefact lifecycle

| **Artefact**     | **From**          | **Command**         | **Guard**                                                        | **To**    |
|------------------|-------------------|---------------------|------------------------------------------------------------------|-----------|
| BriefVersion     | DRAFT             | SubmitBriefVersion  | required fields present; unknowns may remain but are labelled    | IN_REVIEW |
| BriefVersion     | IN_REVIEW         | ApproveBriefVersion | authorised approver; critical conflicts resolved                 | APPROVED  |
| BriefVersion     | IN_REVIEW         | RejectBriefVersion  | reason and requested changes required                            | REJECTED  |
| BriefVersion     | APPROVED/REJECTED | CreateBriefRevision | copy by value with new version number; evidence retained         | new DRAFT |
| StrategyVersion  | DRAFT             | SubmitStrategy      | evidence links and critic report exist                           | IN_REVIEW |
| StrategyVersion  | IN_REVIEW         | ApproveStrategy     | no unresolved critical objection                                 | APPROVED  |
| StrategyVersion  | IN_REVIEW         | RejectStrategy      | reason required                                                  | REJECTED  |
| MediaMixVersion  | DRAFT             | SubmitMediaMix      | allocations reconcile to planning budget                         | IN_REVIEW |
| MediaMixVersion  | IN_REVIEW         | ApproveMediaMix     | authorised approver and assumptions visible                      | APPROVED  |
| MediaPlanVersion | DRAFT             | SubmitMediaPlan     | eligible inventory, current rates, supply state and totals valid | IN_REVIEW |
| MediaPlanVersion | IN_REVIEW         | ApproveMediaPlan    | no material stale input or unresolved blocker                    | APPROVED  |
| ProposalVersion  | DRAFT             | SubmitProposal      | tiers distinct; plan, pricing, evidence and expiry valid         | IN_REVIEW |
| ProposalVersion  | IN_REVIEW         | ApproveProposal     | named commercial approver; PDF preview generated                 | APPROVED  |
| ProposalVersion  | APPROVED          | SendProposal        | recipient resolved; human confirms external send                 | SENT      |
| ProposalVersion  | SENT              | SelectTier          | selected tier exists and not expired                             | SELECTED  |
| ProposalVersion  | SENT              | DeclineProposal     | reason optional; actor authorised                                | DECLINED  |
| ProposalVersion  | APPROVED/SENT     | ExpireProposal      | expiry reached or authorised manual expiry                       | EXPIRED   |

*An approved artefact is never updated. A material change creates a new draft version. Downstream artefacts continue to reference the approved input version until the replacement is approved; then affected downstream drafts are marked STALE and require regeneration or explicit revalidation.*

## 19.3 Inventory import, booking, campaign and run lifecycles

| **Aggregate**   | **From**         | **Command**                 | **Guard**                                                                         | **To**               |
|-----------------|------------------|-----------------------------|-----------------------------------------------------------------------------------|----------------------|
| InventoryImport | UPLOADED         | Classify                    | supported object and malware check passed                                         | CLASSIFYING          |
| InventoryImport | CLASSIFYING      | Extract                     | document class and parser selected                                                | EXTRACTING           |
| InventoryImport | EXTRACTING       | ValidateCandidates          | source locators and candidate schema produced                                     | VALIDATING           |
| InventoryImport | VALIDATING       | OpenReview                  | all validation results stored                                                     | REVIEW_REQUIRED      |
| InventoryImport | REVIEW_REQUIRED  | PublishApproved             | no unresolved critical error; reviewer authorised                                 | PUBLISHING           |
| InventoryImport | PUBLISHING       | CompleteImport              | all approved candidates committed idempotently                                    | COMPLETED            |
| InventoryImport | active           | FailImport                  | classified error stored with retry policy                                         | FAILED               |
| InventoryImport | FAILED           | ResumeImport                | retryable failure or corrected review input                                       | last safe checkpoint |
| RFQ             | DRAFT            | SendRFQ                     | human approval and verified supplier contact                                      | SENT                 |
| RFQ             | SENT             | RecordSupplierResponse      | response source retained                                                          | RESPONSE_RECEIVED    |
| PurchaseOrder   | DRAFT            | SubmitPurchaseOrder         | selected immutable proposal option and signed PO asset                            | SUBMITTED            |
| PurchaseOrder   | SUBMITTED        | ApprovePurchaseOrder        | amount/currency/option reconcile and authorised commercial reviewer               | APPROVED             |
| Invoice         | DRAFT            | IssueInvoice                | accepted proposal plus approved PO; discounts before commission; totals reconcile | ISSUED               |
| PaymentIntent   | DRAFT            | StartPayment                | issued invoice and selected allowed payment method                                | PENDING              |
| PaymentIntent   | PENDING          | ConfirmPayment              | verified provider receipt or authorised manual reconciliation                     | CONFIRMED            |
| Booking         | DRAFT            | RequestSupplierConfirmation | selected approved proposal tier                                                   | PENDING_SUPPLIER     |
| Booking         | PENDING_SUPPLIER | ConfirmBooking              | supplier terms accepted by authorised human                                       | CONFIRMED            |
| Campaign        | PLANNED          | ConfirmBookings             | required bookings confirmed                                                       | BOOKED               |
| Campaign        | BOOKED           | RequestCreative             | format requirements known                                                         | CREATIVE_PENDING     |
| Campaign        | CREATIVE_PENDING | ApproveCreative             | all required assets approved                                                      | READY                |
| Campaign        | READY            | StartCampaign               | start time reached and delivery dependencies healthy                              | LIVE                 |
| Campaign        | LIVE             | CompleteCampaign            | delivery window closed and proofs requested                                       | COMPLETED            |
| AgentRun        | QUEUED           | StartRun                    | provider policy and input versions frozen                                         | RUNNING              |
| AgentRun        | RUNNING          | RequireHumanReview          | tool or critic marks review-required                                              | WAITING_FOR_HUMAN    |
| AgentRun        | RUNNING          | CompleteRun                 | typed output validates and is persisted                                           | COMPLETED            |
| AgentRun        | RUNNING          | FailRun                     | error classified and checkpoint stored                                            | FAILED               |
| AgentRun        | FAILED           | ResumeRun                   | inputs unchanged or paid-call policy explicitly permits rerun                     | RUNNING              |

**Provisional supply rule:** Proposal generation must not wait indefinitely for slow or manual suppliers. Unconfirmed OOH sites or other supply may appear only as clearly labelled availability-subject options with dated rate evidence. They are not bookings. Only client-accepted lines from the selected immutable option may be invoiced or booked; substitution, price change, date change or other material change creates a new version and requires explicit client confirmation.

# 20. Authorisation and tenant isolation

## 20.1 Canonical roles

| **Role**              | **Purpose**                                                             | **Maximum scope**                                                         |
|-----------------------|-------------------------------------------------------------------------|---------------------------------------------------------------------------|
| platform_admin        | Advertified platform administration                                     | Cross-tenant administration only through explicitly privileged operations |
| internal_planner      | Advertified opportunity, strategy, brief, planning and proposal work    | Assigned clients and work queues                                          |
| inventory_ops         | Import, extraction review, inventory quality and supplier operations    | Assigned channels and suppliers                                           |
| agency_admin          | Agency users, advertisers and commercial workspace administration       | Own agency and assigned advertisers                                       |
| agency_campaign_user  | Submit briefs, collaborate and review plans/proposals                   | Assigned advertisers and campaigns                                        |
| advertiser_admin      | Advertiser users, briefs, campaigns and organisation settings           | Own advertiser tenant                                                     |
| advertiser_approver   | Approve assigned brief, strategy, plan, proposal and creative decisions | Own advertiser tenant and assigned resources                              |
| supplier_admin        | Supplier users, listings, rates, availability, RFQs and bookings        | Own supplier tenant                                                       |
| supplier_user         | Maintain assigned inventory and respond to assigned requests            | Own assigned supplier resources                                           |
| influencer_rep        | Profiles, rate cards, requests, deliverables and proofs                 | Owned or represented profiles                                             |
| agent_runtime_service | Read approved inputs and submit typed proposals through tools           | No interactive login; least-privilege service identity                    |
| worker_service        | Execute approved jobs, imports, rendering and notifications             | No interactive login; job-scoped service identity                         |

## 20.2 Internal permission matrix

| **Capability**              | **platform_admin**             | **internal_planner**                  | **inventory_ops**                           |
|-----------------------------|--------------------------------|---------------------------------------|---------------------------------------------|
| Tenant/user administration  | Manage                         | View assigned                         | No                                          |
| Opportunities and evidence  | All                            | Create/edit assigned                  | Evidence review only                        |
| Brief/strategy/audience     | All                            | Create/edit/submit assigned           | View where inventory context required       |
| Media mix/plan/proposal     | All                            | Create/edit/submit assigned           | View supply and price evidence              |
| Commercial approval         | Allowed if separately assigned | Allowed if assigned approver          | No                                          |
| Inventory/imports           | All                            | View/select                           | Create/review/publish assigned              |
| Suppliers/RFQs/bookings     | All                            | Create and coordinate assigned        | Manage supplier operations assigned         |
| Agents/integrations/policy  | Manage                         | Run approved workflows                | Run inventory workflows                     |
| Audit/AI cost/security      | View by privilege              | Own runs/cost                         | Own imports/runs                            |
| Supplier cost/margin/profit | Admin-only finance privilege   | No; sees approved client-facing price | Supplier rate only; no client margin/profit |

## 20.3 Agency and advertiser permission matrix

| **Capability**                | **agency_admin**     | **agency_campaign_user**    | **advertiser_admin**   | **advertiser_approver**         |
|-------------------------------|----------------------|-----------------------------|------------------------|---------------------------------|
| Users/settings                | Manage agency        | No                          | Manage advertiser      | No                              |
| Opportunities                 | Create/view assigned | Create/view assigned        | View own               | View assigned                   |
| Briefs                        | Create/edit/submit   | Create/edit/submit assigned | Create/edit/submit own | Review assigned                 |
| Strategy/audience             | View/comment         | View/comment assigned       | View/comment           | Approve/reject assigned         |
| Media mix/plan                | View/comment         | View/comment assigned       | View/comment           | Approve/reject assigned         |
| Proposal                      | View/comment         | View/comment assigned       | View own               | Approve/select/decline assigned |
| Campaign/results              | View assigned        | View assigned               | View own               | View assigned                   |
| Supplier cost/internal margin | No                   | No                          | No                     | No                              |

## 20.4 Supplier and influencer permission matrix

| **Capability**                   | **supplier_admin**          | **supplier_user** | **influencer_rep**                  |
|----------------------------------|-----------------------------|-------------------|-------------------------------------|
| Users/settings                   | Manage supplier             | No                | Manage represented profile          |
| Listings/rates/assets            | Create/edit/publish request | Edit assigned     | Create/edit own profile and rates   |
| Availability                     | Manage own                  | Update assigned   | Update own deliverable availability |
| RFQs/requests                    | View/respond own            | Respond assigned  | View/respond own                    |
| Bookings/deliverables            | Confirm/manage own          | Update assigned   | Manage own deliverables             |
| Other suppliers or client margin | No                          | No                | No                                  |

- The API resolves effective permissions from authenticated identity, active membership, tenant, assignment and resource state. The browser never supplies trusted role or tenant claims.

- Every protected query includes a tenant predicate. Database policies or equivalent constraints provide defence in depth for the highest-risk tables.

- Approval permission is distinct from edit permission. The creator of a consequential artefact cannot self-approve unless an explicit account policy allows it and the audit records the exception.

- Launch-blocking negative tests attempt cross-tenant reads, writes, enumeration, object-key access, background jobs and agent tool calls for every protected resource family.

# 21. Commercial API contract

## 21.1 API-wide rules

| **Concern**       | **Normative contract**                                                                                           |
|-------------------|------------------------------------------------------------------------------------------------------------------|
| Base and format   | /api/v1; JSON UTF-8; OpenAPI is generated and version controlled                                                 |
| Authentication    | OIDC/session adapter with secure HttpOnly cookie or bearer token; API resolves user and memberships              |
| Tenant context    | Tenant/workspace is part of the route; API verifies active membership and ignores untrusted client role claims   |
| Correlation       | Accept or create X-Correlation-ID; return it on every response and propagate through jobs and provider calls     |
| Idempotency       | Idempotency-Key required for consequence-bearing POST commands; retain result at least 24 hours                  |
| Concurrency       | Return ETag on mutable aggregates; require If-Match for update/transition commands                               |
| Money/time        | ISO currency plus integer minor units; UTC ISO timestamps; explicit VAT/fee fields                               |
| Pagination        | Cursor pagination; stable sort; default 25, maximum 100; filters are allow-listed                                |
| Success envelope  | data plus meta and optional links; commands return the resulting canonical resource                              |
| Errors            | application/problem+json with stable code, title, detail, correlationId and fieldErrors; never leak stack traces |
| Long-running work | Return 202 with run resource and status URL; user can inspect, resume or cancel where safe                       |
| Files             | Pre-signed, short-lived upload/download intents; validate size, type, hash and malware status before processing  |

## 21.2 Identity, opportunity, evidence and brief endpoints

| **Method** | **Path**                                           | **Permission**     | **Outcome**                                        |
|------------|----------------------------------------------------|--------------------|----------------------------------------------------|
| GET        | /me                                                | authenticated      | Current user, memberships and permitted workspaces |
| GET        | /workspaces                                        | authenticated      | Authorised workspace list                          |
| GET        | /tenants/{tid}/home                                | member             | Role-specific KPIs and priority queue              |
| POST       | /tenants/{tid}/invitations                         | tenant user manage | Create invitation and notification event           |
| POST       | /tenants/{tid}/opportunities                       | opportunity create | Create Opportunity                                 |
| GET        | /tenants/{tid}/opportunities                       | opportunity view   | Filtered cursor list                               |
| GET        | /tenants/{tid}/opportunities/{id}                  | opportunity view   | Opportunity detail, evidence and next action       |
| PATCH      | /tenants/{tid}/opportunities/{id}                  | opportunity edit   | Update mutable opportunity metadata with If-Match  |
| POST       | /tenants/{tid}/opportunities/{id}/evidence-sources | evidence create    | Register upload or permitted URL and queue capture |
| POST       | /tenants/{tid}/evidence-items/{id}/review          | evidence review    | Approve/reject/edit structured evidence            |
| POST       | /tenants/{tid}/opportunities/{id}/interpret        | agent run          | Queue Business Interpretation run                  |
| POST       | /tenants/{tid}/opportunities/{id}/angles:generate  | agent run          | Queue Opportunity Intelligence run                 |
| POST       | /tenants/{tid}/opportunity-angles/{id}:select      | opportunity edit   | Select angle and audit decision                    |
| POST       | /tenants/{tid}/briefs                              | brief create       | Create CampaignBrief aggregate                     |
| POST       | /tenants/{tid}/briefs/{id}/versions                | brief edit         | Create immutable draft BriefVersion                |
| GET        | /tenants/{tid}/briefs/{id}                         | brief view         | Aggregate, versions, comparison and approvals      |
| POST       | /tenants/{tid}/brief-versions/{id}:submit          | brief submit       | Submit for review                                  |
| POST       | /tenants/{tid}/brief-versions/{id}:approve         | brief approve      | Approve named version                              |
| POST       | /tenants/{tid}/brief-versions/{id}:reject          | brief approve      | Reject with reason                                 |

## 21.3 Strategy, planning and proposal endpoints

| **Method** | **Path**                                                | **Permission**   | **Outcome**                                        |
|------------|---------------------------------------------------------|------------------|----------------------------------------------------|
| POST       | /tenants/{tid}/opportunities/{id}/strategies:generate   | agent run        | Queue Strategy and Critic workflow                 |
| GET        | /tenants/{tid}/strategies/{id}                          | strategy view    | Version, evidence, objections and approval         |
| POST       | /tenants/{tid}/strategy-versions/{id}:approve           | strategy approve | Approve current version                            |
| POST       | /tenants/{tid}/brief-versions/{id}/audiences:generate   | agent run        | Queue Audience agent                               |
| POST       | /tenants/{tid}/brief-versions/{id}/media-mixes:generate | agent run        | Queue Media Planning mix stage                     |
| POST       | /tenants/{tid}/media-mix-versions/{id}:approve          | plan approve     | Approve allocation and channel roles               |
| POST       | /tenants/{tid}/brief-versions/{id}/shortlists:generate  | agent run        | Eligibility then Inventory Intelligence            |
| POST       | /tenants/{tid}/shortlist-versions/{id}:select           | plan edit        | Record selected/rejected candidates                |
| POST       | /tenants/{tid}/brief-versions/{id}/media-plans:generate | agent run        | Supply/forecast then Media Planning                |
| GET        | /tenants/{tid}/media-plans/{id}                         | plan view        | Plan versions, line evidence, totals and supply    |
| POST       | /tenants/{tid}/media-plan-versions/{id}:approve         | plan approve     | Approve named plan version                         |
| POST       | /tenants/{tid}/briefs/{id}/proposals:generate           | agent run        | Generate structured tiers and narrative            |
| GET        | /tenants/{tid}/proposals/{id}                           | proposal view    | Proposal versions, tiers, preview and approvals    |
| POST       | /tenants/{tid}/proposal-versions/{id}:approve           | proposal approve | Approve exact proposal version                     |
| POST       | /tenants/{tid}/proposal-versions/{id}:render            | proposal edit    | Generate branded DOCX/PDF from approved facts      |
| POST       | /tenants/{tid}/proposal-versions/{id}:send              | external send    | Resolve recipient, confirm human approval and send |
| POST       | /tenants/{tid}/proposal-versions/{id}:select-tier       | proposal select  | Record selected tier and start booking             |

## 21.4 Inventory, supplier, delivery and operations endpoints

| **Method** | **Path**                                        | **Permission**          | **Outcome**                                                                     |
|------------|-------------------------------------------------|-------------------------|---------------------------------------------------------------------------------|
| POST       | /tenants/{tid}/inventory-imports                | inventory import        | Create upload intent and import record                                          |
| POST       | /tenants/{tid}/inventory-imports/{id}:execute   | inventory import        | Start/resume pipeline idempotently                                              |
| GET        | /tenants/{tid}/inventory-imports/{id}           | inventory review        | Pipeline state, counts, errors and candidates                                   |
| POST       | /tenants/{tid}/inventory-candidates/{id}:review | inventory review        | Approve, reject or edit candidate                                               |
| POST       | /tenants/{tid}/inventory-imports/{id}:publish   | inventory publish       | Publish approved candidates                                                     |
| GET        | /tenants/{tid}/inventory-products               | inventory view          | Large-catalogue cursor search and filters                                       |
| GET        | /tenants/{tid}/inventory-products/{id}          | inventory view          | Dedicated product detail, rates, assets and evidence                            |
| POST       | /tenants/{tid}/inventory-products               | supplier inventory edit | Create supplier-owned draft listing                                             |
| POST       | /tenants/{tid}/inventory-products/{id}:publish  | inventory publish       | Validate and publish listing                                                    |
| POST       | /tenants/{tid}/rfqs                             | rfq create              | Create draft RFQ                                                                |
| POST       | /tenants/{tid}/rfqs/{id}:send                   | external send           | Human-approved supplier request                                                 |
| POST       | /tenants/{tid}/rfqs/{id}/responses              | supplier respond        | Record supplier response and evidence                                           |
| POST       | /tenants/{tid}/purchase-orders                  | commercial finance      | Attach signed PO to exact accepted proposal option                              |
| POST       | /tenants/{tid}/purchase-orders/{id}:approve     | finance approve         | Reconcile and approve PO                                                        |
| POST       | /tenants/{tid}/invoices:issue                   | finance issue           | Issue invoice only after accepted proposal and approved PO                      |
| POST       | /tenants/{tid}/payment-intents                  | payment create          | Start VodaPay, manual EFT or Advertise Now Pay Later route                      |
| POST       | /tenants/{tid}/payment-intents/{id}:reconcile   | finance reconcile       | Verify provider/manual receipt and update canonical status                      |
| POST       | /tenants/{tid}/bookings                         | booking create          | Create only from accepted sites/lines in the selected immutable proposal option |
| POST       | /tenants/{tid}/bookings/{id}:confirm            | booking confirm         | Record authorised supplier confirmation                                         |
| GET        | /tenants/{tid}/campaigns/{id}                   | campaign view           | Delivery state, tasks, creative, proof and metrics                              |
| POST       | /tenants/{tid}/campaigns/{id}/creative          | creative edit           | Upload versioned creative                                                       |
| POST       | /tenants/{tid}/campaigns/{id}/delivery-proofs   | delivery proof          | Upload and review proof                                                         |
| GET        | /tenants/{tid}/agent-runs/{id}                  | run view                | Status, steps, errors, evidence and incremental cost                            |
| POST       | /tenants/{tid}/agent-runs/{id}:resume           | run manage              | Resume from safe checkpoint                                                     |
| POST       | /tenants/{tid}/agent-runs/{id}:cancel           | run manage              | Cancel future work; preserve completed artefacts                                |
| GET        | /tenants/{tid}/human-tasks                      | task view               | Assigned actionable queue                                                       |
| POST       | /tenants/{tid}/human-tasks/{id}:complete        | task act                | Validate action schema and complete                                             |
| GET        | /tenants/{tid}/audit-events                     | audit view              | Permissioned immutable audit search                                             |

## 21.5 Required command payloads

| **Command**              | **Fields**                                                                                                                                                                                               | **Validation floor**                                                 |
|--------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------|
| CreateOpportunity        | clientId, title, sourceType, sourceRef?, ownerUserId, expectedValueMinor?, currency?, deadline?, problemSummary?, objectiveSummary?                                                                      | title, sourceType, ownerUserId                                       |
| CreateBriefVersion       | briefId, baseVersionId?, businessProblem, objective, audiences\[\], geography\[\], timing, budget, vatStatus, fees, constraints\[\], measurement\[\], unknowns\[\], assumptions\[\], evidenceItemIds\[\] | businessProblem, objective, timing, typed budget or explicit unknown |
| ReviewEvidenceItem       | decision APPROVE\|REJECT\|EDIT, structuredValue?, reason, expectedVersion                                                                                                                                | reason for reject/edit; reviewer permission                          |
| ApprovalCommand          | resourceType, resourceId, versionId, decision APPROVE\|REJECT, reason?, expectedVersion                                                                                                                  | exact version; reason required for reject                            |
| GenerateArtefact         | inputVersionIds\[\], providerPolicy, requestedBy, idempotencyKey                                                                                                                                         | approved inputs unless workflow explicitly creates a draft           |
| CreateInventoryImport    | supplierId?, fileName, contentType, sizeBytes, sha256, documentHint?                                                                                                                                     | allow-listed type/size and unique or explicit reprocess              |
| ReviewInventoryCandidate | decision, fieldPatch?, reasonCodes\[\], notes?, expectedVersion                                                                                                                                          | reason for reject; validation after patch                            |
| SendExternal             | resourceVersionId, recipientId, subjectTemplate, messageTemplate, approvalId, idempotencyKey                                                                                                             | verified recipient and approval                                      |

- Generate OpenAPI and typed clients in CI. A changed public contract requires a schema version, migration note and contract tests.

- Use stable domain error codes such as TENANT_FORBIDDEN, VERSION_CONFLICT, EVIDENCE_REQUIRED, ARTIFACT_STALE, RATE_EXPIRED, APPROVAL_REQUIRED and RUN_NOT_RESUMABLE.

- Never expose provider prompts, credentials, internal stack traces or private reasoning in API responses. Expose business-safe rationale, evidence, confidence, status and recovery actions.

