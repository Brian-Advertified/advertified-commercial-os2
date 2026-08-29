# Gate 4 work packet — evidence and opportunity

## Status

**DELIVERED AND VERIFIED LOCALLY — Brian Rabuthu direction, 2026-08-29**

Prepared after Brian Rabuthu directed the repository to commit the completed Gate 2–3 work
and proceed to Gate 4 on 2026-08-29. The prior implementation is committed locally as
`115d500`. Brian then directed Gate 4 to be delivered and replaced repetitive per-gate pauses
with standing sequential local-delivery authority on 2026-08-29.

This approval is local and reversible only. It does not authorise push, merge, deployment,
production use, live crawling, a live or paid AI provider, cloud mutation, production data, or
external communication.

## Outcome and completion boundary

A local non-production unbriefed prospect can progress through:

`Opportunity → reviewed evidence → confirmed business interpretation → selected opportunity
angle → Strategy plus Critic → resolved objections → approved StrategyVersion`.

The Commercial API then places the Opportunity in `BRIEF_READY`. Gate 4 does not draft or
approve a BriefVersion. That is Gate 5. A supplied client Brief remains a separate source
artefact and never enters this Opportunity-discovery workflow.

## Governing sources

- Normative Sections 6, 7.1 steps 1–6, 10, 18.1–18.3, 19.1–19.2, 20,
  21.1–21.3, 22, 24, 25, 28 and 31;
- `docs/IMPLEMENTATION_PLAN.md`, Gate 4;
- ADR-0002's binding no-autonomy rule;
- ADR-0003's proposed Opportunity-to-Brief sequence, which must be accepted for this local
  Gate 4 scope before implementation;
- accepted ADR-0005 for human and service identity separation;
- accepted ADR-0006 for PostgreSQL tenant isolation;
- accepted ADR-0007 for migration ownership and execution;
- accepted ADR-0008 for browser validation, notifications and human-safe errors.

If this packet conflicts with a governing source, the affected capability remains blocked.

## Exact workflow in scope

1. An authorised human creates an unbriefed Opportunity with a client, title, source type,
   owner and any explicitly supplied problem, objective, value or deadline.
2. The owner registers at least one permitted source. The original snapshot is immutable,
   content-addressed and tenant-scoped.
3. The owner starts qualification. Deterministic capture produces candidate EvidenceItems or
   an inspectable blocked/failure state; it never claims an unavailable crawl succeeded.
4. A different assigned reviewer approves, rejects or edits the structured review value while
   preserving the original claim, excerpt and locator. Conflicts and missing facts remain
   explicit.
5. Submission creates an immutable EvidenceSetVersion containing reviewed items and recorded
   gaps. A designated reviewer approves that exact version. Only its approved items can enter
   an agent invocation.
6. Business Interpretation produces a typed version covering offering, customer groups,
   buying occasions, geography, known facts, hypotheses and unknowns. A human confirms the
   exact version.
7. Opportunity Intelligence consumes only the approved evidence set and confirmed
   interpretation, then proposes a ranked, evidence-bound angle set. A human selects or
   rejects an exact angle; the agent cannot select it.
8. Strategy consumes the exact approved evidence set, confirmed interpretation and selected
   angle. Critic & Readiness reviews the resulting StrategyVersion and creates immutable
   objections.
9. A named human resolves or explicitly accepts every material/advisory objection. Critical
   objections cannot be waived in Gate 4. A different assigned approver approves the exact
   submitted StrategyVersion.
10. The Commercial API moves the Opportunity to `BRIEF_READY`, writes correlated audit and
    outbox records, and presents Gate 5 as unavailable until separately authorised.

No transition is inferred from agent text, worker completion, page navigation or confidence.

## Source capture and evidence policy

Gate 4 supports two source modes:

| Code | Accepted local input | Behaviour |
|---|---|---|
| `SUPPLIED_TEXT` | Explicit bounded UTF-8 text, title and source locator supplied by a human | Preserve the immutable text snapshot in PostgreSQL, assign a content-addressed key and hash server-side |
| `PERMITTED_URL` | Normalised HTTP(S) URL, title and recorded policy basis | Register and queue capture; only the deterministic fixture adapter can return content |

- The default and production URL-capture adapter fails closed with
  `CAPTURE_PROVIDER_DISABLED`. Gate 4 performs no real network request.
- The deterministic adapter exists only in Development/Test and maps an exact allow-listed URL
  to an immutable fixture. An unmatched URL fails with `CAPTURE_PROVIDER_DISABLED`, never empty
  evidence or fabricated content.
- URL validation rejects credentials, fragments, IP literals, non-HTTP(S) schemes, non-default
  ports and hosts that resolve or redirect to loopback, private, link-local, multicast or other
  prohibited address ranges. DNS/redirect validation belongs to a future live adapter and must
  be repeated on every hop.
- `policyBasis` is required. Source terms, robots policy, lawful basis and permitted use remain
  human decisions; the software does not declare a site lawful to crawl.
- Content is untrusted data. Embedded instructions are preserved and labelled but never enter
  system/tool policy. HTML/script execution, arbitrary binary upload, OCR and document parsing
  are excluded.
- Deduplication uses tenant, source type and SHA-256 content hash. A duplicate links to the
  existing immutable source; a changed capture creates a new source version and never rewrites
  the old snapshot.
- MinIO/object-storage preservation starts when a later gate accepts binary source files.
  Gate 4 does not add an object-storage SDK for bounded text evidence.
- The raw snapshot is not approved evidence. Each material claim needs an EvidenceItem review
  decision, and agents receive only approved item identifiers.

## Canonical records and migration

The C# Commercial API owns all canonical state. The expand-only Gate 4 migration adds:

- `Opportunity` and tenant/resource ownership;
- `ClientAccountAssignment` for explicit user-to-client scope with effective dates;
- `EvidenceSource`, immutable object reference/hash and capture outcome;
- `EvidenceItem`, original candidate value, reviewed value, locator, excerpt, confidence,
  decision, reviewer and optimistic version;
- immutable `EvidenceSetVersion` plus item membership, conflict and unknown records;
- immutable `BusinessInterpretationVersion`;
- immutable `OpportunityAngleSetVersion` and retained `OpportunityAngle` rows;
- immutable `StrategyVersion`, `CriticReport`, objections and resolution decisions;
- durable `AgentRun`, `AgentRunStep`, checkpoint and zero-cost `AIUsageLedger` records;
- assigned `HumanTask` records for real evidence, interpretation, angle, critic and strategy
  decisions.

Every protected row carries `tenant_id` directly or has a database-enforced tenant parent.
High-risk families use forced RLS and tenant-qualified foreign keys. Submitted/approved
versions are immutable. Mutable aggregates use bigint versions and strong ETags. Accepted
commands atomically write canonical state, audit, idempotency and outbox consequences.

Governed master data adds these exact collections and stable codes:

| Collection | Initial Gate 4 codes |
|---|---|
| `opportunitySourceTypes` | `DISCOVERY`, `REFERRAL` |
| `evidenceSourceTypes` | `SUPPLIED_TEXT`, `PERMITTED_URL` |
| `evidencePolicyBases` | `OWNER_SUPPLIED`, `PUBLIC_SITE_REVIEWED` |
| `evidenceClaimTypes` | `OFFERING`, `CUSTOMER_GROUP`, `BUYING_OCCASION`, `GEOGRAPHY`, `PRICE`, `CONTACT_ROUTE`, `BUSINESS_CONTEXT`, `OTHER` |
| `evidenceReviewDecisions` | `APPROVE`, `REJECT`, `EDIT` |
| `opportunityAngleStatuses` | `PROPOSED`, `SELECTED`, `REJECTED` |
| `criticSeverities` | `CRITICAL`, `MATERIAL`, `ADVISORY` |
| `objectionResolutions` | `ADDRESSED`, `ACCEPTED_WITH_REASON`, `SUPERSEDED` |
| `humanTaskTypes` | `EVIDENCE_ITEM_REVIEW`, `EVIDENCE_SET_APPROVAL`, `INTERPRETATION_CONFIRMATION`, `ANGLE_SELECTION`, `CRITIC_RESOLUTION`, `STRATEGY_APPROVAL`, `RUN_RECOVERY` |

Capture, artefact and task states reuse applicable governed `lifecycleStatuses`. Definitions
and seed records must agree; no application-local alternative vocabulary is allowed.

Approval of this packet authorises creation and disposable-PostgreSQL apply/upgrade/reapply/
rollback verification of the Gate 4 migration. Applying it to the existing os2 main local
database requires a later instruction naming that exact migration and target. Runtime
processes never auto-migrate.

## Exact permission expansion

| Permission code | Role ceiling | Resource restriction |
|---|---|---|
| `opportunity_view` | platform_admin, internal_planner, agency_admin, agency_campaign_user, advertiser_admin, advertiser_approver | Active tenant membership and owned/assigned client or Opportunity |
| `opportunity_create` | platform_admin, internal_planner, agency_admin, agency_campaign_user | Active tenant membership plus allowed client assignment |
| `opportunity_edit` | platform_admin, internal_planner, agency_admin, agency_campaign_user | Owned/assigned open Opportunity |
| `evidence_create` | platform_admin, internal_planner, agency_admin, agency_campaign_user | Owned/assigned Opportunity |
| `evidence_review` | platform_admin, inventory_ops | Exact assigned item/set; reviewer cannot be source/item creator |
| `agent_run` | platform_admin, internal_planner | Owned/assigned Opportunity and eligible approved inputs |
| `opportunity_angle_select` | platform_admin, internal_planner | Owned/assigned Opportunity and exact current angle-set version |
| `strategy_view` | platform_admin, internal_planner, agency_admin, agency_campaign_user, advertiser_admin, advertiser_approver | Owned/assigned Opportunity/advertiser scope |
| `strategy_approve` | platform_admin, advertiser_approver | Exact assigned submitted version; approver cannot be creator |
| `run_view` | platform_admin, internal_planner | Owned/assigned run |
| `run_manage` | platform_admin, internal_planner | Owned/assigned resumable run |
| `task_view` / `task_act` | All human roles | Exact assignee and visible underlying resource; action additionally checks domain permission |

`agent_runtime_service` and `worker_service` receive no interactive permissions. The browser,
model and deterministic provider supply no trusted role, tenant or approval claim. Missing
assignment data denies. Platform administration never grants a silent cross-tenant bypass.

## Agent and durable-execution boundary

- Implement only `business_interpretation`, `opportunity_intelligence`, `strategy` and
  `critic_readiness` for this gate; the closed eleven-agent roster remains unchanged.
- Add strict versioned Pydantic artefact contracts and per-agent evaluation fixtures. Free-form
  output cannot become canonical state.
- The deterministic provider remains the only provider, with `allowLive=false`, zero tool calls
  unless an exact read-only fixture declares them, one attempt and cost cap `0` minor units.
- A Development/Test-only service-auth adapter protects the loopback C#→FastAPI invocation.
  Its secret comes from ignored environment configuration. It cannot start in Production.
- The C# API enqueues a persisted run and returns `202`. A local hosted dispatcher claims work
  with a lease/`SKIP LOCKED`, invokes FastAPI, validates the response, and persists each step
  through the canonical Application command boundary.
- Checkpoints store exact input hashes and validated output references. Restart resumes the
  first incomplete step; duplicate delivery returns the stored outcome.
- Retryable failures use 30-second, 2-minute and 10-minute policy intervals through an
  injectable clock. Invalid output, prompt injection, conflicts, cost block, unknown fixture
  and ambiguous acceptance become stable review/failed states rather than silent retries.
- No Python component imports a PostgreSQL client, SQL library or Commercial database
  configuration. It cannot approve, mutate state directly, spend, publish or communicate.

## API and browser surface

Implement the Section 21 contracts for Opportunity create/list/detail/update, evidence-source
registration, evidence-item review, interpretation, angle generation/selection, strategy
generation/view/submit/approve/reject, run view/resume/cancel and human-task list/complete.
Add explicit lifecycle commands for start qualification, submit evidence and approve an exact
evidence-set version. All mutations use the existing correlation, idempotency, ETag/If-Match,
ProblemDetails, audit and outbox conventions. Regenerate and retain OpenAPI v1; every consumed
browser response and form is Zod-validated.

Implement real `/opportunities`, `/opportunities/:id`, `/strategies/:id`, `/runs/:id` and
`/tasks` routes. They show source lineage, review status, evidence versus hypotheses/unknowns,
selected angle, critic objections, run/checkpoint state and one valid human action. Required
loading, empty, partial/stale, forbidden, validation, failure, retry and reduced-motion states
remain accessible at desktop and compact viewports. No raw provider output, prompt, stack
trace, database wording or private reasoning is rendered.

The multi-role acceptance journey uses separate allow-listed Development/Test identities for
planner, evidence reviewer and strategy approver. The local adapter never accepts arbitrary
user, tenant or role claims from the browser and remains unavailable in Production.

## Explicit exclusions

- Brief creation/drafting/approval and every supplied-Brief path (Gate 5);
- arbitrary file upload, malware scanning, OCR, HTML execution, Docling and inventory imports;
- real crawling/search, redirects, DNS access or contacting an external site;
- live/paid Bedrock or any provider SDK, fallback model, embeddings or vector retrieval;
- inventory, audience, media mix, planning, proposal, supplier, booking, payment or campaign;
- production OIDC/service clients, Redis orchestration, cloud resources, email or analytics;
- accepting critical critic objections, creator self-approval or fabricated task/KPI data.

## Acceptance evidence

| Risk | Minimum repeatable evidence |
|---|---|
| Canonical flow | Opportunity reaches `BRIEF_READY` only after approved evidence, confirmed interpretation, selected angle, critic resolution and different-human strategy approval |
| Source truth | Hash/dedup/immutability, unmatched capture blocked, no network, original versus reviewed values retained |
| Evidence binding | Every material output field is approved-evidence-bound or labelled assumption/unknown; rejected/unreviewed/cross-tenant items fail |
| Anti-injection/conflict | Embedded instructions cannot change tools/policy; conflicting sources create a review task and block the affected consequence |
| Authorisation | Cross-tenant read/write/enumeration/tool/job denial, revoked membership, missing assignment, service overreach and creator self-approval negatives |
| Lifecycle/versioning | Invalid transition, stale ETag/input drift, changed submitted artefact and stale approval all fail safely |
| Durability | Checkpoint resume after restart, duplicate dispatch, retry exhaustion, cancellation and invalid output produce one stable audited result and zero duplicate invocation |
| Agent evaluation | Four strict contracts, schema validity, evidence/unknown coverage, quality rubric, Critic severity and zero-cost deterministic fixtures pass |
| Human task | Exact assignee/resource/action, double-submit idempotency, reassignment/expired authority and immutable completion behaviour pass |
| Browser | One multi-role unbriefed journey plus failure/recovery at desktop and compact reduced-motion viewports; keyboard/focus/status announcements pass |
| Migration | Empty apply, prior-version upgrade, repeat apply/bootstrap, forced-RLS/constraint assertions and disposable rollback pass |
| Architecture | C#/Python/React dependency, line/function/complexity, generated-contract, secret and provider-boundary checks remain green |

The named Rayetsa Furniture quality fixture cannot be fabricated. Its product-quality
evaluation remains blocked until the owner supplies or approves the source-evidence pack and
expected labels. A clearly synthetic local fixture may prove workflow mechanics; it cannot be
reported as genuine opportunity acceptance.

## Recovery, evidence and cost

- Feature configuration defaults Gate 4 execution and deterministic capture off outside
  Development/Test. Removing routes/dispatcher returns runtime behaviour to Gate 3 while the
  expand-only schema remains inert.
- No existing row is transformed or deleted. Disposable rollback is verified; any migrated
  shared local target uses roll-forward unless the owner explicitly authorises rollback.
- Exact commands/results are retained in `docs/evidence/gate-4/` and the capability ledger is
  updated. Brian Rabuthu directed Gate 4 delivered; the AI did not approve the gate.
- Cost ceiling is ZAR 0. No live provider, paid call, real crawl, production resource, external
  communication or production data is permitted.

## Authorisation record

Brian Rabuthu's direction to deliver Gate 4 confirms, for local Gate 4 only:

1. ADR-0003's ordered separation through approved Strategy (Brief Drafting remains Gate 5);
2. the two source modes and zero-network deterministic capture boundary;
3. the canonical records, permission ceilings and different-human approvals above;
4. the Development/Test service-auth and durable C# dispatcher/FastAPI topology;
5. creation and disposable verification of the expand-only Gate 4 migration, but not applying
   it to the os2 main local database;
6. zero new provider, crawler, UI or persistence package unless implementation proves an exact
   need and records it before installation.

The standing sequential-delivery direction removes further repetitive gate approval pauses.
It does not permit an AI to mark unverified work delivered or broaden external, production,
paid-provider or destructive authority.
