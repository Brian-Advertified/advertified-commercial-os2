# Gate 8 evidence report — proposal and client decision

**Evidence date:** 2026-08-29  
**Repository:** advertified-commercial-os2  
**Branch:** master  
**Owner direction:** complete sequential local delivery and commit each completed gate  
**Live provider used:** No  
**Production resources used:** No  
**Incremental AI cost:** 0

## Delivered capability

An approved CampaignBrief and one to three genuinely different approved MediaPlanVersions can now become a versioned, client-safe proposal. Proposal choices are not platform package labels and are not cosmetic price copies: every option binds an exact approved media plan, its commercial total, media types, inventory and running periods.

The assigned agency operator can choose approved plans, write outcome-led option names and narratives, edit the executive summary and terms, approve the exact proposal version, render a deterministic branded PDF and explicitly share it with a selected advertiser approver. The assigned client recipient can read only the shared proposal, open the PDF, select exactly one option or decline. Selection records an immutable decision but does not create a booking, invoice or supplier commitment.

## Important invariants verified

- a proposal references only approved plans belonging to the approved BriefVersion;
- one to three options are allowed and every option uses a distinct plan signature;
- platform package codes Launch, Boost, Scale and Dominance are not proposal option identities;
- plan IDs, plan versions, inventory product versions, rate IDs and availability IDs are revalidated before approval, render and sharing;
- changed planning or supply truth makes the proposal stale instead of silently reusing approval;
- client-facing views contain plan totals and included media but do not expose supplier cost, margin or internal scoring;
- draft wording remains editable, while approval freezes the exact proposal version;
- PDF bytes are generated deterministically from approved structured facts and retained with hash and size;
- sharing requires an explicit agency action and a same-tenant active advertiser recipient;
- only the assigned recipient can decide, only one decision is accepted and expired proposals fail closed;
- proposal selection creates no booking, invoice, payment or external supplier action;
- no live mail provider, model provider or production resource was used.

## Verification

| Check | Result |
|---|---|
| Release C# API build | PASS — 0 warnings, 0 errors |
| Complete C# API suite | PASS — 38/38 |
| Proposal PostgreSQL acceptance | PASS — generation, editing, approval, PDF, sharing, recipient isolation, selection, duplicate-plan and expiry cases |
| Architecture guardrails | PASS — canonical master data, source-size and boundary rules |
| Agent runtime tests | PASS — deterministic runtime, zero provider cost |
| Web lint | PASS — 0 warnings, 0 errors |
| Web unit tests | PASS |
| Web production build | PASS — proposal builder and proposal record are separately code-split |
| Authenticated Playwright | PASS — 16/16 desktop + compact, including agency-to-client proposal decision |
| Retained OpenAPI v1 | PASS after regeneration and complete contract suite |

## UI result

The proposal experience follows the approved Advertified navy, white, neutral and electric-blue visual system and the Omnicom Omni interaction reference without copying its branding. Approved plans appear as spacious selectable cards with media icons, exact totals and flight dates. The proposal preview presents clear option cards and uses progressive disclosure for included placements. The agency action bar shows one next action at a time: approve, create PDF or share. The client view removes editing and internal detail, then presents one direct choice or decline action.

## Remaining boundaries

Rapid OOH supplier-confirmation orchestration, supplier self-service/RFQ operations, booking and campaign delivery, hardening/certification and production launch remain later delivery boundaries. No proposal was sent outside the deterministic local adapter, and no shared/local canonical database migration or production deployment was performed.

The implementing AI verified local delivery but did not approve security, privacy, legal compliance or production readiness.
