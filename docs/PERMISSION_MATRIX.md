# Advertified permission and approval design

**Status:** Gate 2 registry and mappings accepted for local non-production implementation — Brian Rabuthu, 2026-08-29
**Accountable owner:** Brian Rabuthu
**Independent security review:** PENDING before publication or production

The canonical role and Gate 2 permission codes are governed in
`shared/contracts/master-data.json`. Database-backed resolution is required before a mapping
is IMPLEMENTED; repeatable denial evidence is required before it is VERIFIED.

## Accepted Gate 2 permission ceiling

An active mapping never grants access by itself. Authenticated identity, active membership,
tenant context, database RLS and any resource assignment must also allow the request.
Missing assignment data denies access. `platform_admin` receives no automatic cross-tenant
bypass and must hold an active membership in the selected tenant for these Gate 2 routes.

| Permission code | Exact mapped roles |
|---|---|
| `workspace_read` | All human roles; no service roles |
| `tenant_read` | All human roles; no service roles |
| `tenant_manage` | platform_admin, agency_admin, advertiser_admin, supplier_admin |
| `user_read_self` | All human roles; self only |
| `user_manage_self` | All human roles; self only |
| `membership_read` | platform_admin, agency_admin, advertiser_admin, supplier_admin |
| `membership_manage` | platform_admin, agency_admin, advertiser_admin, supplier_admin |
| `client_account_read` | platform_admin, agency_admin |
| `client_account_manage` | platform_admin, agency_admin |
| `agency_read` | platform_admin, agency_admin, agency_campaign_user |
| `agency_manage` | platform_admin, agency_admin |
| `contact_read` | platform_admin, agency_admin |
| `contact_manage` | platform_admin, agency_admin |

The human roles are `platform_admin`, `internal_planner`, `inventory_ops`, `agency_admin`,
`agency_campaign_user`, `advertiser_admin`, `advertiser_approver`, `supplier_admin`,
`supplier_user` and `influencer_rep`. `agent_runtime_service` and `worker_service` receive no
interactive Gate 2 permissions. Internal planners and campaign users do not receive client
or contact access until a canonical assignment model can enforce their assigned scope.

## Universal invariants

- Every protected request/tool/job carries authenticated actor, tenant, effective role, resource, and correlation context.
- The C# Commercial API independently resolves active membership, assignment, resource tenant, state, and permission.
- Client/browser/agent claims are never trusted as authority.
- Deny by default. A missing assignment or policy is a denial.
- Edit, submit, review, approve, publish, book, pay, invoice, and administer are distinct permissions.
- A creator cannot self-approve a material consequence.
- Delegation requires an explicit scope, start/end time, delegator, delegate, and audit.
- Service identities have no interactive login and receive only task-specific commands.
- Cross-tenant administration never grants silent access to client commercial content.
- Supplier and influencer identities see only owned/assigned resources and never internal margin.
- Agent and worker identities cannot approve, spend, publish, communicate externally, or bypass lifecycle commands.

## Role scope intent

| Role | Maximum intended scope | Consequential approval |
|---|---|---|
| platform_admin | Platform administration; explicit support access only | No implicit commercial approval |
| internal_planner | Assigned clients/opportunities/plans | Only if separately assigned as approver and not creator |
| inventory_ops | Assigned imports/channels/suppliers | Review/publish only under explicit policy; no client approval |
| agency_admin | Own agency and assigned advertisers | Explicit assigned scope; no creator self-approval |
| agency_campaign_user | Assigned advertiser/campaign work | No |
| advertiser_admin | Own advertiser administration | Explicit assigned scope; no creator self-approval |
| advertiser_approver | Own assigned artefacts | Yes for exact assigned version |
| supplier_admin | Own supplier administration/listings | Own supply confirmations; no client commercial approval |
| supplier_user | Assigned supplier resources | Own assigned operational response only |
| influencer_rep | Owned/represented profiles | Own deliverable response only |
| agent_runtime_service | Allowlisted proposal tools | Never |
| worker_service | Allowlisted durable jobs | Never |

## Consequence gates

| Consequence | Minimum required authority/evidence |
|---|---|
| Evidence approval | Designated reviewer, exact EvidenceItem version |
| Strategy/Brief/Plan approval | Assigned human approver, exact immutable version |
| Proposal send/client decision | Approved proposal version and named human action |
| Inventory publication | Inventory reviewer plus source/validation version |
| Supplier commitment/booking | Named authorised human, confirmed supply/rate version |
| Payment/invoice | Finance-authorised human and reconciled commercial version |
| Creative/public publication | Brand/legal approvals and exact asset version |
| External email/webhook | Named human approval or narrowly approved deterministic notification policy |

## Required negative tests

For every protected resource family test cross-tenant read, write, enumerate, export, object key, background job, event replay, agent tool, and indirect parent/child access. Also test revoked membership, expired delegation, stale version, creator self-approval, service identity overreach, and guessed identifiers.

No permission row becomes “implemented” until those tests exercise the real API and persistence boundary.
