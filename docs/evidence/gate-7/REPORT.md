# Gate 7 evidence report — canonical planning

**Evidence date:** 2026-08-29  
**Repository:** advertified-commercial-os2  
**Branch:** master  
**Owner direction:** complete sequential local delivery and commit each completed gate  
**Live provider used:** No  
**Production resources used:** No  
**Incremental AI cost:** 0

## Delivered capability

An approved BriefVersion now moves through evidence-labelled audience definition, editable media mix, inventory eligibility and transparent OOH/DOOH benchmarking, human inventory selection, deterministic pricing, critic resolution and approved MediaPlanVersion.

The media mix is a working planning tool rather than a read-only recommendation. Each media type can have its own budget, role and one or more non-overlapping running periods. The browser shows distinct media-type iconography, colour-coded allocation bars and an independent running-period timeline. Approved mixes remain immutable; revisions create new draft versions.

OOH/DOOH comparative intelligence uses a PostGIS `geography(Point,4326)` projection with a GiST index and `ST_DWithin`/`ST_Distance`. Comparable cohorts use governed adaptive radii and retain excluded peers and reasons. Schedule-aware rate validity and deterministic billing quantity are shared by shortlist eligibility and final plan pricing. The inventory product page exposes the same market comparison independently of a campaign.

## Important invariants verified

- allocations reconcile exactly to the approved Brief budget;
- every media allocation has at least one valid running period and periods cannot overlap within a channel;
- schedule-aware rate validity runs before shortlist scoring;
- the same deterministic billing-unit calculator is used for eligibility and media-plan pricing;
- governed scoring and benchmark thresholds are loaded from canonical master data rather than embedded commercial magic numbers;
- every considered inventory item retains an eligibility or rejection outcome;
- ineligible inventory cannot be selected;
- OOH/DOOH benchmark cohorts retain exact product/rate IDs, distances, exclusions, statistics, position and confidence;
- stale rate/availability inputs block plan approval;
- material plan objections require explicit human resolution;
- plan totals reconcile deterministically in minor units including fees and VAT;
- no reach, impressions, audience size or supplier availability is invented;
- no paid or live provider was called.

## Verification

| Check | Result |
|---|---|
| Release C# API build | PASS — 0 warnings, 0 errors |
| Complete C# API suite | PASS — 36/36 |
| Canonical planning PostgreSQL journey | PASS — approved Brief to approved plan, PostGIS benchmark, negative allocation/rate/selection cases |
| Architecture guardrails | PASS — 22/22 |
| Agent runtime tests | PASS — 18/18 |
| Web lint | PASS — 0 warnings, 0 errors |
| Web unit tests | PASS — 4/4 |
| Web production build | PASS — planning route code-split |
| Authenticated Playwright | PASS — 14/14 desktop + compact |
| Retained OpenAPI v1 | PASS after regeneration and full C# contract suite |

## UI result

The authenticated planning workspace follows the existing Advertified visual direction and the approved Omnicom Omni interaction reference without copying branding or proprietary screens. It uses progressive disclosure: planning summary, editable media allocation, timeline, shortlist/benchmark detail, then reconciled plan and approval. The individual inventory product page now includes a concise Market comparison panel with local median, difference, percentile, confidence and expandable comparable sites.

## Remaining boundaries

Proposal/client decision, Rapid OOH supplier confirmation, supplier marketplace, booking/delivery, measurement, production hardening and launch remain later delivery boundaries. Shared/local canonical database deployment and production resources were not changed in this gate.

The implementing AI verified local delivery but did not approve security, privacy, legal compliance or production readiness.
