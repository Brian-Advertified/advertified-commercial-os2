# Gate 5 work packet — canonical Brief

## Status

**AUTHORISED FOR LOCAL IMPLEMENTATION — standing direction, 2026-08-29**

Brian Rabuthu directed sequential local gates to continue without repetitive approval pauses.
Gate 4 is committed at `57c8db5` with repeatable evidence in `docs/evidence/gate-4/`.
This packet binds the smallest Gate 5 vertical change before implementation begins.

This authority is local and reversible only. It does not authorise a commit, push, merge,
deployment, shared-database migration, production or cloud mutation, live or paid AI,
production data, file ingestion, crawling, or external communication.

## Bounded outcome

Both canonical entry paths can produce a reviewed Brief without fabricating missing facts:

1. A human pastes a supplied client Brief, preserves that source verbatim, and creates a
   structured draft whose unknowns and assumptions remain explicit.
2. An Opportunity at `BRIEF_READY` queues the closed-roster `brief_drafting` agent against
   the exact approved StrategyVersion and its approved evidence. The deterministic local
   provider returns a typed proposal only; the Commercial API creates canonical records.
3. An authorised human edits by creating a new version and confirms the exact current draft.
   The assigned agency operator may explicitly self-confirm without inventing a second person.
4. Confirmation fixes the immutable version as the CampaignBrief's approved version. An
   Opportunity-backed Brief then advances from `BRIEF_READY` to `PLANNING`.

Gate 5 ends at approved BriefVersion. It does not implement inventory, audience research,
planning, media mixes, proposals, supplier communication, booking, funding, or delivery.

## Governing sources

- Normative Sections 5.1–5.2, 7.1, 18.3, 19.2, 21.2, 22 and 24;
- `docs/IMPLEMENTATION_PLAN.md`, Gate 5;
- accepted ADR-0002 no-autonomy boundary;
- accepted ADR-0003 ordered Opportunity-to-Brief sequence;
- accepted ADR-0005 identity separation;
- accepted ADR-0006 PostgreSQL tenant isolation;
- accepted ADR-0007 migration ownership;
- accepted ADR-0008 safe browser and error behaviour.

If a conflict emerges, that affected capability remains blocked rather than being inferred.

## Canonical records and invariants

The expand-only Gate 5 migration adds:

- `campaign_briefs`, with tenant, client, optional Opportunity, title, owner, lifecycle,
  current draft, approved version and optimistic version;
- `brief_sources`, preserving supplied text, source type, locator and SHA-256 digest;
- `brief_versions`, with base-version lineage, problem, objective, audience direction,
  geography, timing, typed budget/VAT/fees, constraints, measurement, facts, unknowns,
  assumptions, conflicts, evidence bindings, status and author;
- tenant-safe BriefVersion-to-EvidenceItem links where an Opportunity provides approved
  evidence.

The Commercial API is the only writer. Direct tenant IDs, tenant-qualified foreign keys,
forced RLS and the existing transaction-local context remain mandatory. Supplied source
snapshots never change. BriefVersion content cannot change after submission. Editing and
material change create a new version by value; approved versions are never overwritten.

A Brief draft cannot be submitted unless business problem, objective and timing are present,
and budget is either typed or explicitly recorded as unknown. Confirmation requires the
current submitted version, its exact assigned task, an eligible human and no unresolved
critical conflict. Under Brian Rabuthu's explicit 2026-08-29 direction, the active assigned
agency operator self-confirms; advertiser approval belongs to the later Proposal/Client
Decision boundary, not the Brief. Replacement confirmation preserves prior versions and makes downstream work
stale when that downstream capability exists.

## Governed vocabulary and permissions

Master data advances once and adds:

- brief source types: `SUPPLIED_TEXT`, `OPPORTUNITY`;
- human task type: `BRIEF_APPROVAL`;
- permissions: `brief_view`, `brief_create`, `brief_edit`, `brief_submit`, `brief_approve`.

Permission ceilings are:

| Permission | Allowed human roles | Resource restriction |
|---|---|---|
| `brief_view` | platform_admin, internal_planner, agency_admin, agency_campaign_user, advertiser_admin, advertiser_approver | Active membership and owned/assigned client, Opportunity or exact confirmation task |
| `brief_create` | platform_admin, internal_planner, agency_admin, agency_campaign_user | Active membership and owned/assigned client or Opportunity |
| `brief_edit` | platform_admin, internal_planner, agency_admin, agency_campaign_user | Current owned/assigned Brief and eligible base version |
| `brief_submit` | platform_admin, internal_planner, agency_admin, agency_campaign_user | Current draft and eligible agency confirmer; defaults to the acting operator |
| `brief_approve` | internal_planner, agency_admin, agency_campaign_user | Exact assigned submitted version; the agency operator may self-confirm |

Draft-agent queuing continues to require `agent_run`. `task_view` and `task_act` continue to
require exact assignment plus the underlying domain permission. Platform administration does
not imply commercial approval. Service roles receive no interactive Brief permission.

## Contracts and local execution

The API implements the normative create/version/view/submit/approve/reject routes plus one
Opportunity draft command. All mutations keep Idempotency-Key, If-Match where state changes,
correlation, audit, outbox and human-safe ProblemDetails behaviour. Brief detail returns every
version and an explicit comparison basis; the browser parses consumed contracts with Zod.

`brief_drafting` receives only canonical approved Opportunity inputs. Its strict output keeps
facts, audience hypotheses, assumptions and unknowns separate and evidence-bound. The local
deterministic provider has cost zero and no network/tool call. Invalid output fails closed and
cannot create a Brief. Python never connects to PostgreSQL or approves its proposal.

The browser adds `/briefs/new` for pasted UTF-8 source text and `/briefs/:id` for review,
comparison, confirmation and rejection. Opportunity detail links to its Brief. User-facing
copy says what is known, assumed and missing without exposing prompts, raw model output,
internal exceptions or database terminology.

## Explicit exclusions

- binary upload, malware scanning, OCR, Docling, office/PDF parsing or object-storage writes;
- invented extraction from unstructured text—the supplied form records the human's explicit
  structured interpretation and preserves the original text;
- live AI, model/provider SDKs, embeddings, vector retrieval, crawling or external tools;
- implicit confirmation, accepting unresolved critical conflicts, or
  treating audience direction as completed audience research;
- inventory, pricing, availability, planning, proposal, funding, suppliers, bookings,
  campaigns, analytics or any later-gate record;
- applying `202608290004_CanonicalBrief` to the shared local database.

## Acceptance evidence

| Risk | Repeatable evidence required |
|---|---|
| Supplied path | Verbatim source and hash retained; explicit draft fields/unknowns survive create, review, approval and comparison |
| Opportunity path | Only `BRIEF_READY` plus exact approved Strategy/evidence can draft; typed zero-cost output creates one canonical draft |
| Versioning | Submitted content and approved versions are immutable; edits create a linked version; stale If-Match/base input is denied |
| Human control | Exact agency assignee self-confirms; no advertiser approval is requested; cross-tenant access and double action are denied |
| Knowledge quality | Facts, hypotheses, assumptions and unknowns remain distinct; unresolved critical conflicts block approval |
| Lifecycle | No Brief bypass; Opportunity changes to `PLANNING` only after Brief approval |
| Browser | Supplied and Opportunity journeys expose clear next actions, comparison, loading/error/recovery and compact layouts |
| Migration | Disposable empty apply, prior-version upgrade, repeat apply/bootstrap, forced-RLS assertions and rollback pass |
| Architecture | .NET 10/C# 14, Python and React boundaries, file/function limits, master-data definitions, secrets and live-provider checks pass |

## Recovery, evidence and cost

Gate 5 uses an expand-only schema and removable routes/dispatcher behaviour. No existing row
is transformed or deleted. Disposable rollback is tested; a migrated shared environment would
use roll-forward unless the owner explicitly authorised otherwise. Exact commands and results
will be retained in `docs/evidence/gate-5/` and capability status will stay truthful.

The cost ceiling is ZAR 0. No main database, production resource, live provider, external
party or production data may be used. The owner can direct delivery; the implementing AI does
not approve the gate or its own correctness.
