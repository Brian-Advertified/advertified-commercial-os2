# Advertified Permission Matrix

## Canonical Roles (Section 20.1)

| Role Code | Display Label | Maximum Scope | Can Self-Approve |
|-----------|---------------|---------------|-----------------|
| platform_admin | Platform Administrator | Cross-tenant administration only | No (except by explicit policy) |
| internal_planner | Internal Planner | Assigned clients and work queues | No (separate approver required) |
| inventory_ops | Inventory Operations | Assigned channels and suppliers | No (except own review tasks) |
| agency_admin | Agency Administrator | Own agency and assigned advertisers | Yes (within agency scope) |
| agency_campaign_user | Agency Campaign User | Assigned advertisers and campaigns | No (approver required) |
| advertiser_admin | Advertiser Administrator | Own advertiser tenant | Yes (within advertiser scope) |
| advertiser_approver | Advertiser Approver | Own advertiser and assigned resources | Yes (within approval scope) |
| supplier_admin | Supplier Administrator | Own supplier tenant | Yes (within supplier scope) |
| supplier_user | Supplier User | Assigned supplier resources | No (except own listings) |
| influencer_rep | Influencer Representative | Owned or represented profiles | Yes (within profile scope) |
| agent_runtime_service | Agent Runtime Service | No interactive login | N/A (service identity) |
| worker_service | Worker Service | No interactive login | N/A (service identity) |

## Internal Permission Matrix (Section 20.2)

| Capability | platform_admin | internal_planner | inventory_ops |
|------------|----------------|------------------|----------------|
| Tenant/user administration | Manage | View assigned | No |
| Opportunities and evidence | All | Create/edit assigned | Evidence review only |
| Brief/strategy/audience | All | Create/edit/submit assigned | View where inventory context required |
| Media mix/plan/proposal | All | Create/edit/submit assigned | View supply and price evidence |
| Commercial approval | Allowed if separately assigned | Allowed if assigned approver | No |
| Inventory/imports | All | View/select | Create/review/publish assigned |
| Suppliers/RFQs/bookings | All | Create and coordinate assigned | Manage supplier operations assigned |
| Agents/integrations/policy | Manage | Run approved workflows | Run inventory workflows |
| Audit/AI cost/security | View by privilege | Own runs/cost | Own imports/runs |
| Supplier cost/margin/profit | Admin-only finance privilege | No; sees approved client-facing price | Supplier rate only; no client margin/profit |

## Agency and Advertiser Permission Matrix (Section 20.3)

| Capability | agency_admin | agency_campaign_user | advertiser_admin | advertiser_approver |
|------------|--------------|----------------------|------------------|-------------------|
| Users/settings | Manage agency | No | Manage advertiser | No |
| Opportunities | Create/view assigned | Create/view assigned | View own | View assigned |
| Briefs | Create/edit/submit | Create/edit/submit assigned | Create/edit/submit own | Review assigned |
| Strategy/audience | View/comment | View/comment assigned | View/comment | Approve/reject assigned |
| Media mix/plan | View/comment | View/comment assigned | View/comment | Approve/reject assigned |
| Proposal | View/comment | View/comment assigned | View own | Approve/select/decline assigned |
| Campaign/results | View assigned | View assigned | View own | View assigned |
| Supplier cost/internal margin | No | No | No | No |

## Supplier and Influencer Permission Matrix (Section 20.4)

| Capability | supplier_admin | supplier_user | influencer_rep |
|------------|----------------|--------------|----------------|
| Users/settings | Manage supplier | No | Manage represented profile |
| Listings/rates/assets | Create/edit/publish request | Edit assigned | Create/edit own profile and rates |
| Availability | Manage own | Update assigned | Update own deliverable availability |
| RFQs/requests | View/respond own | Respond assigned | View/respond own |
| Bookings/deliverables | Confirm/manage own | Update assigned | Manage own deliverables |
| Other suppliers or client margin | No | No | No |

## Critical Authorization Invariants

**Every tool call carries**: tenant_id, actor_id, role, resource_id, correlation_id

**Commercial API independently re-authorises**: Every requested action before reading or writing protected state

**Approval permission is distinct from edit permission**: Creator of consequential artefact cannot self-approve unless explicit account policy allows it

**Browser claims are never trusted**: API resolves effective permissions from authenticated identity, active membership, tenant, assignment, and resource state

**Tenant predicate on every protected query**: Database policies or equivalent constraints provide defence in depth for highest-risk tables

**Launch-blocking negative tests**: Attempt cross-tenant reads, writes, enumeration, object-key access, background jobs, and agent tool calls for every protected resource family

## Approval Workflow Matrix

| Artefact Type | Creator Role | Approver Role | Self-Approval Allowed |
|--------------|--------------|---------------|----------------------|
| EvidenceItem | Any role with evidence capture | inventory_ops or designated reviewer | No |
| StrategyVersion | internal_planner | Designated strategy approver | No |
| BriefVersion | internal_planner or agency_admin | advertiser_approver or designated approver | Only if policy allows |
| MediaMixVersion | internal_planner | Designated media approver | No |
| MediaPlanVersion | internal_planner | Designated plan approver | No |
| ProposalVersion | internal_planner | advertiser_approver | No |
| InventoryProduct | supplier_admin or inventory_ops | inventory_ops (for publish) | Yes for supplier own listings |
| Booking | internal_planner | supplier_admin (for confirmation) | No |

## Cross-Tenant Access Rules

**Allowed cross-tenant operations**:
- platform_admin: Full cross-tenant administration
- RFQs: Cross-tenant supplier requests (initiated by internal_planner)
- Public inventory viewing: Non-tenant-restricted catalogue search

**Forbidden cross-tenant operations**:
- Direct database access across tenant boundaries
- Agent tool calls without tenant-scoped authorization
- API calls without tenant context validation
- File access across tenant object storage boundaries
- Background job processing without tenant isolation

## Service Identity Permissions

**agent_runtime_service**:
- Read approved inputs from Commercial API
- Submit typed proposals through tools
- No interactive login
- No direct database access
- No external effect without human approval

**worker_service**:
- Execute approved jobs, imports, rendering, notifications
- Job-scoped service identity
- No interactive login
- Idempotent operations only
- Audit all side effects