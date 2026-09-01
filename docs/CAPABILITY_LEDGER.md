# Advertified capability ledger

**Evidence date:** 2026-09-01
**Clean-parent reference for the current stabilisation pass:** `53209e7`
**Current change state:** Gates 9–10 are verified locally; Gate 11 is implemented locally, with
connected Brief/proposal paths now passing while the complete Campaign Delivery browser regression
remains API-mocked evidence; Gate 12 certification preparation is in progress; Gate 13 remains
blocked
**Status vocabulary:** ABSENT, SCAFFOLDED, IMPLEMENTED, VERIFIED, BLOCKED

A capability is VERIFIED only when repeatable evidence was observed. Documentation does not make a capability implemented.

## Current cross-gate agent runtime parity

| Capability | Status | Evidence / blocker |
|---|---|---|
| One Python/FastAPI runtime boundary | VERIFIED locally | Audience, Media Planning, Inventory Intelligence, Proposal Narrative and Measurement join the delivered Opportunity and Creative handlers behind the one typed runtime route; no parallel agent service was added |
| Truthful closed-roster reporting | VERIFIED locally | Deterministic mode advertises exactly the eleven approved executable handlers, including Inventory Intelligence. Disabled mode advertises no active handlers |
| Commercial API adapters | VERIFIED locally | `HttpDeterministic` selects typed HTTP adapters for Opportunity, Audience/Media Planning, Inventory Intelligence, Proposal Narrative and Measurement; disabled/in-process modes retain explicit zero-cost local adapters |
| Untrusted-output validation | VERIFIED locally | Unknown response fields, unapproved evidence, non-zero cost, sensitive audience invention, disallowed channels, budget mismatch, altered proposal facts and malformed Inventory Intelligence candidate coverage fail closed |
| Exact resource lineage | VERIFIED locally | Audience and mix calls pin the BriefVersion; Inventory Intelligence pins the exact BriefVersion and InventoryShortlistVersion; proposal calls pin the BriefVersion and every selected MediaPlanVersion |
| Live AgentCore/Bedrock provider | BLOCKED | ADR-0001 remains proposed; named owner approval, security/privacy/legal, cost and operations evidence are absent. No SDK, credential, network call or production resource was used |

**Historical runtime-parity evidence:** the 2026-08-30 pack records Runtime Ruff and 26/26 tests,
Release API 56/56 and architecture 23/23. See
`docs/evidence/agent-runtime-parity-20260830/`.

**Historical pre-correction baseline:** web unit 6/6, architecture 23/23, isolated Release API
109/109, deterministic runtime 28/28, serial desktop 16/16, serial compact 16/16 and normal
configured Playwright 32/32 passed on the dated source. These are not current-tree counts. See
`docs/RELEASE_STABILIZATION_WORK_PACKET.md` and
`docs/evidence/gate12-email-delivery-durability-20260831/`.

**Latest retained complete C# run:** the combined Release graph built with zero warnings/errors and
the then-current complete Release API suite passed 128/128 in 3m57s. That result predates the latest
Inventory Intelligence, Brief-readiness and packaging changes and remains retained evidence rather
than a claim about the newest source.

**Current latest-source verification:** the API and migrator publish successfully in the pinned
Linux .NET SDK 10.0.400 build; web lint, type-check, unit 6/6 and host/Linux production builds pass;
the retained API-mocked Playwright matrix is 32/32 and 4/4 connected local critical journeys pass
through the packaged web/API/PostgreSQL/runtime stack, including keyboard/accessibility-shell behavior. The deterministic runtime passes 31/31 and
final-tree architecture passes 42/42. Master-data registry 2.12.0 projections match. The complete
latest-source C# suite is locally blocked because this Windows host has .NET SDK 10.0.103 while the
repository requires 10.0.400 with roll-forward disabled. No live provider, production resource,
remote CI result, staging certification or production approval is claimed.

The first current-tree API run passed 123/126 and therefore remained failed evidence. Two generated
OpenAPI drift failures and one deterministic planning fixture-clock regression were corrected. A
later Docling redirect-security regression increased the denominator; the complete rerun then
passed 128/128.

An intermediate API run passed 107/109 and remained failed evidence. The two causes were a
temporary registry effective date of 2026-09-01 ahead of PostgreSQL `CURRENT_DATE`, and a recovery
assertion hard-coded to registry version `2.9.0`. Registry `2.11.0` retains the correct 2026-08-31
effective date, and recovery now uses the generated registry version. Both corrections passed
independently and in the isolated 109/109 suite; the two delivery-state regressions pass 2/2.

## Gate 0 — repository baseline

| Capability | Status | Evidence |
|---|---|---|
| Correct os2 repository and branch | VERIFIED | configured os2 path, `master`; clean at start |
| Binding contributor/AI rules | IMPLEMENTED | `AGENTS.md` |
| Complete normative v1.1 source | VERIFIED | Seven ordered parts; all seven hashes match source chunks |
| React dependency lock | VERIFIED | Exact React 19.2.0 and locked install |
| Commercial API baseline | VERIFIED | Release build and health endpoint tests |
| Agent runtime baseline | VERIFIED | Provider disabled; zero agents claimed |
| PostgreSQL 16 local foundation | VERIFIED | PostGIS and pgvector image plus health checks |
| Local dependency services | VERIFIED | PostgreSQL, Redis, MinIO, ClamAV, Docling and MailHog are defined as the six required Compose services |
| CI definition | IMPLEMENTED | Real architecture, web, API, Python, and Compose jobs |
| GitHub CI result | BLOCKED | The current working tree is unpublished; remote CI and publication are not authorised |
| Product journeys | ABSENT | Correctly excluded from Gate 0 |

**Local Gate 0 evidence:** PASS; Gate 1 work is authorised.  
**Merge/release:** NO-GO until owner diff review and remote CI.

## Gate 1 — architecture guardrails

| Capability | Status | Evidence / blocker |
|---|---|---|
| C# dependency direction | VERIFIED | Domain → none; Application → Domain; Infrastructure → Application/Domain; API → Application/Infrastructure |
| C# analyzers and complexity | VERIFIED | .NET 10/C# 14 analyzers; Release API and migration-runner builds report 0 warnings/errors |
| TypeScript complexity/function limits | VERIFIED | Oxlint 0 warnings/errors with complexity 10 and function maximum 60 |
| Python complexity/function limits | VERIFIED locally | Ruff C90 and the 60-line architecture rule pass locally; current release evidence is recorded separately |
| 400-line authored-source rule | VERIFIED | Architecture suite |
| Controlled violating fixtures | VERIFIED | Dependency, DB import, ADR, function, and file-size detectors reject known violations |
| Tenant deny-by-default harness | VERIFIED | Missing, inactive, wrong actor, wrong tenant, and missing permission all deny identically |
| Cross-tenant command containment | VERIFIED | Denial occurs before idempotency lookup or handler execution |
| Command/idempotency contract | VERIFIED | Duplicate fixture returns one canonical outcome; changed payload conflicts |
| Audit/outbox correlation | VERIFIED | Mismatched tenant/command/correlation cannot commit |
| Optimistic concurrency contract | VERIFIED | Expected version is mandatory and outcome advances exactly once |
| Opportunity state-machine skeleton | VERIFIED | Typed canonical transitions and invalid-transition negatives |
| Governed master-data registry | VERIFIED | Single embedded JSON source; 14 coherent collections |
| PostgreSQL master-data migration | VERIFIED | Throwaway PostgreSQL 16 apply/bootstrap/reapply/protection/rollback test |
| Stable master-data codes and audit | VERIFIED | Database rejects code change/delete and records item history |
| Closed eleven-agent roster | VERIFIED | Exact code-controlled roster test; at Gate 1 no agent implementation was claimed |
| Versioned invocation/output contracts | VERIFIED | Strict typed Pydantic v1 envelopes and exact resource versions |
| Deterministic provider | VERIFIED | Exact fixture or safe failure; no fallback, live call, or cost |
| Agent evaluation fixture format | VERIFIED | Evidence/assumption/unknown classification and tool/cost boundaries validated |
| ADR ownership process | IMPLEMENTED | Accepted status requires actual names and ISO decision date |
| Evidence manifest/report format | IMPLEMENTED | Versioned schema and templates; Gate 1 report pending owner review |
| Accepted ADR and Gate 1 owner decision | BLOCKED | Brian Rabuthu is the accountable owner; final GO awaits remote CI and evidence review |
| Remote CI | BLOCKED | Local commit exists; publication and CI are owner-deferred, not passed |

**Local executed evidence:** web lint/type-check/2 tests/build pass; API build and 18 tests pass; agent runtime 9 tests pass; architecture 20 tests pass.  
**Gate 1 decision:** PENDING. An AI cannot close the gate.

## Gate 2 — canonical commercial foundation

The approved local work packet is implemented and verified locally. Brian Rabuthu recorded
the dated local Gate 2 completion GO on 2026-08-29. Publication, independent review and
production readiness remain separate blocked decisions.

| Capability | Status | Evidence / blocker |
|---|---|---|
| .NET 10/C# 14 API baseline | VERIFIED | ADR-0009; all C# projects target `net10.0`; Release builds and complete C# suite pass |
| Six canonical aggregate models | IMPLEMENTED | Tenant, User, Membership, ClientAccount, Agency and Contact domain/persistence models |
| Canonical commercial migration | VERIFIED | Disposable PostgreSQL empty apply, repeat apply, master-data bootstrap, protection and rollback pass |
| Tenant constraints and forced RLS | VERIFIED | Disposable PostgreSQL cross-tenant read/write/association negatives pass under the application role |
| Idempotency, audit and outbox consequences | VERIFIED | Persisted duplicate/conflict and atomic consequence acceptance tests pass |
| Deterministic development identity | VERIFIED | Production fails closed; expired/invalid identities deny; service identities cannot use interactive endpoints |
| Human-safe error contract | VERIFIED | Typed ProblemDetails code/correlation contract and production authentication denial pass |
| Dedicated migration runner | VERIFIED | Requires explicit apply and migration-only connection; validates least-privilege effective role; disposable apply/reapply passes |
| Versioned OpenAPI v1 | VERIFIED | Retained contract matches the running API contract semantically |
| DB-backed membership reads | VERIFIED | Application-role workspace and membership reads run with transaction-local user/tenant context; cross-tenant resolution returns no membership |
| Role-to-permission resolution | VERIFIED | Accepted 13-code Gate 2 registry, exact conservative role mappings, database-backed resolution and service-role denial |
| Aggregate commands and queries | VERIFIED | Tenant/User updates, ClientAccount/Agency/Contact creates, and tenant-scoped reads for all six aggregates pass through authorization, forced RLS and retained HTTP contracts |
| HTTP concurrency and retry contract | VERIFIED | Strong ETags, required `If-Match`, tenant-scoped `Idempotency-Key`, correlation, replay indication, changed-payload and stale-version conflicts pass end to end |
| Cursor query contract | VERIFIED | Opaque cursor and deterministic tenant-scoped sort return distinct pages in the API acceptance journey |
| Main local database migration | VERIFIED | Owner-authorised exact target applied through the dedicated runner; both migrations record EF Core 10.0.11; post-apply RLS/owner/privilege checks pass |
| Gate 2 completion decision | VERIFIED | Brian Rabuthu directed progression to Gate 3, recording local Gate 2 GO on 2026-08-29; publication and production reviews remain pending |

**Local executed evidence:** architecture 20 passed; .NET 10 Release API and migrator
builds pass with 0 warnings/errors; complete C# suite 29 passed; disposable database
migration and rollback pass. See `docs/evidence/gate-2/`.

## Gate 3 — authenticated application shell

| Capability | Status | Evidence / blocker |
|---|---|---|
| Gate 3 work packet | VERIFIED | Brian Rabuthu approved the exact bounded packet on 2026-08-29 |
| Gate 3 implementation authority | VERIFIED | Brian Rabuthu approved the exact local-only packet and dependencies on 2026-08-29 |
| Provider-neutral local browser session | VERIFIED | Opaque HttpOnly cookie, hashed process-local lookup, expiry, logout invalidation and production fail-closed tests |
| Browser request protection | VERIFIED | Antiforgery and same-origin denial for unsafe cookie-authenticated requests |
| Authenticated product shell | VERIFIED | Sign-in, real workspace selection, tenant-correct home and profile routes pass desktop/compact journeys |
| Browser contract validation | VERIFIED | Zod validates API, form and session-storage boundaries; malformed payloads fail closed |
| Human-safe notification boundary | VERIFIED | Stable codes map to safe wording; only NotificationService infrastructure imports React-Toastify |
| Real tasks, notifications and unsupported KPIs | ABSENT | Destinations remain disabled/truthful because no owning persisted workflow exists |
| Gate 3 completion decision | VERIFIED | Brian Rabuthu recorded local Gate 3 GO on 2026-08-29; publication and production reviews remain pending |

## Gate 4 — evidence and opportunity

| Capability | Status | Evidence / blocker |
|---|---|---|
| Canonical Opportunity and evidence records | VERIFIED | Expand-only migration, forced tenant RLS, immutable submitted artefacts and disposable PostgreSQL tests |
| Human-separated evidence and strategy approvals | VERIFIED | Assigned reviewer/approver flow and creator self-review denial in the Gate 4 acceptance journey |
| Deterministic four-agent sequence | VERIFIED | Strict Python contracts and 15 tests; C# validates and persists only evidence-bound zero-cost outputs |
| Durable run execution | VERIFIED | Persisted runs/steps/usage, leases, checkpoints, duplicate active-run denial and safe retry/recovery states |
| Opportunity lifecycle | VERIFIED | Multi-role journey reaches `BRIEF_READY` only after approved evidence, confirmed interpretation, selected angle, resolved objection and strategy approval |
| Gate 4 API/OpenAPI | VERIFIED | Versioned retained contract covers Opportunity, evidence, artefact, run and human-task query/command surfaces |
| Authenticated Gate 4 web journey | VERIFIED | Opportunity/list/detail, Strategy, Run and Task routes; desktop and compact Playwright flows pass |
| Live provider/network/commercial action | ABSENT | Deterministic fixtures only; zero AI cost, no crawl, publication, spend or external communication |
| Main local database migration | VERIFIED locally | The named local developer database records migration `202608290003_EvidenceOpportunity`; disposable apply/rollback evidence remains retained. No staging or production database changed |
| Gate 4 completion direction | VERIFIED | Brian Rabuthu directed Gate 4 delivered on 2026-08-29; production and publication reviews remain pending |

See `docs/evidence/gate-4/` for commands and exact outcomes.

## Gate 5 — canonical Brief

| Capability | Status | Evidence / blocker |
|---|---|---|
| Canonical Brief, source and immutable version records | VERIFIED | Expand-only migration, verbatim source plus SHA-256, version lineage, forced RLS and submitted-content immutability pass in disposable PostgreSQL |
| Supplied-Brief entry | VERIFIED | Authenticated form and API retain the supplied wording, typed interpretation and explicit unknown budget without fabrication |
| Client-name continuity | VERIFIED | Brief intake accepts the client named in the source without pre-registration; tenant-scoped API projections keep its display name visible in Brief and Planning without exposing a client identifier as primary UI content |
| Opportunity-to-Brief entry | VERIFIED | Only `BRIEF_READY` with exact approved Strategy/evidence can run the deterministic zero-cost `brief_drafting` proposal and create canonical records |
| Clear supplied-Brief readiness | VERIFIED connected locally | A clear supplied Brief is preserved and interpreted, receives its campaign mode, is marked ready and reaches Planning without a fabricated approval or second agency user |
| Material-ambiguity human boundary | IMPLEMENTED | Human correction remains reserved for materially unresolved Brief details; version/task, concurrency and conflict checks still protect that correction path |
| Current Brief lifecycle | IMPLEMENTED | `ready_version_id` separates machine-resolved readiness from explicit later commercial approvals without weakening immutable source/version lineage |
| Gate 5 API/OpenAPI and browser regression | IMPLEMENTED with connected evidence | Retained historical routes/contracts remain; the latest connected clear-Brief browser journey passes against the current packaged web/API/database stack. The newest complete C# regression remains pending |
| Live provider/network/commercial action | ABSENT | Deterministic fixtures only; zero AI cost, no file processing, publication, spend or external communication |
| Main local database migration | VERIFIED locally | The named local developer database records migration `202608290004_CanonicalBrief`; disposable apply/rollback evidence remains retained. No staging or production database changed |
| Gate 5 owner/independent review | PENDING | The latest clear supplied-Brief rule is implemented and connected locally; the complete latest-source C# regression, remote CI and independent review remain pending, and the implementing AI cannot approve the gate |

See `docs/evidence/gate-5/` for commands and exact outcomes.

## Gate 6 — inventory truth

| Capability | Status | Evidence / blocker |
|---|---|---|
| Bounded Gate 6 work packet | VERIFIED | `docs/GATE6_WORK_PACKET.md`; local implementation authorised 2026-08-29 |
| Protected supplier-file intake | VERIFIED | 100 MiB boundary, byte classification, mismatch denial, quarantine, malware isolation, immutable hash protection and six-class held-out corpus pass |
| Evidence-linked candidate review | VERIFIED | Raw/normalised values, transformations, source locators/hashes, blocking validation, exact assignment and creator separation pass |
| Versioned publication and inventory search | VERIFIED | Immutable product/rate/availability/asset versions, detail and deterministic cursor paging over 10,001 products pass |
| Gate 6 API/OpenAPI and browser journey | VERIFIED | Retained contracts and desktop/compact import-review-publish-search journeys pass |
| OOH benchmark prerequisites | SCAFFOLDED | Published inventory has geography/lat/long/rate truth, but typed OOH comparable attributes (dimensions, digital/loop/share, structure/route context) and a PostGIS spatial benchmark index/query primitive are not yet implemented. Must be completed before canonical planning may claim OOH comparative intelligence. |
| Main local database migration | VERIFIED locally | The named local developer database records migration `202608290005_InventoryTruth`; disposable apply and RLS evidence remains retained. No staging or production database changed |
| Live provider/network/commercial action | ABSENT | Deterministic fixtures only; no OCR/AI, supplier contact, booking, spend, external publication or production resource |
| Gate 6 owner/independent review | PENDING | Local implementation and repeatable evidence are complete; the implementing AI does not approve production readiness |

## Gate 7 — canonical planning

| Capability | Status | Evidence / blocker |
|---|---|---|
| Audience and editable media mix | VERIFIED | Approved Brief produces evidence-labelled audience definitions without requiring inventory first; HTTP runtime output is constrained to supplied geographies, no invented sensitive fields, allowed channels and the exact budget; planner edits role and running periods before approval |
| Schedule-aware eligibility and pricing | VERIFIED | The same governed billing-unit policy drives shortlist affordability and final media-plan quantity/pricing; stale planned-period rates fail closed |
| OOH/DOOH comparative intelligence | VERIFIED | PostGIS geography projection + GiST index + adaptive `ST_DWithin`/`ST_Distance` cohorts; exact comparable IDs/rates/distances/exclusions/statistics retained |
| Inventory Intelligence agent boundary | VERIFIED connected locally | The eleventh approved runtime handler receives only exact-version shortlist facts, explains governed eligibility/benchmark results without changing them, and its validated persisted rationale is visible in the connected proposal journey before selection |
| Inventory product market comparison | VERIFIED | OOH/DOOH detail page exposes local median, percentile, above/below-market position, confidence and expandable comparable sites |
| Human selection, critic and plan approval | VERIFIED | Ineligible selection denied; exact selection retained; material objections require resolution; stale supply inputs block approval |
| Gate 7 API/OpenAPI and browser journey | VERIFIED | Retained contract plus desktop/compact editable planning and market-comparison journeys pass |
| Live provider/network/commercial action | ABSENT | Deterministic local planning only; zero provider cost and no supplier communication, booking, spend or external send |
| Local canonical database deployment | VERIFIED locally | The named local developer database records Docling/planning migrations 006–007; the PostGIS path also passed disposable-database verification. No staging or production resource changed |
| Gate 7 owner direction | VERIFIED | Owner directed completion and commit of each sequential gate on 2026-08-29; non-local independent reviews remain pending |

See `docs/evidence/gate-7/` for retained commands and exact outcomes.

## Gate 8 — proposal and client decision

| Capability | Status | Evidence / blocker |
|---|---|---|
| Distinct approved-plan choices | VERIFIED | One to three client options bind different approved MediaPlanVersions; duplicate plan choices fail closed and platform package codes remain separate |
| Proposal wording and version approval | VERIFIED | Narrative input pins the exact BriefVersion and every selected MediaPlanVersion; the API rejects changed objective, outcome, channel or exact minor-unit totals before the assigned agency operator edits and approves wording |
| Branded deterministic PDF | VERIFIED | Approved structured facts render to retained PDF bytes with filename, media type, SHA-256 and size; no browser or model-generated commercial truth |
| Controlled client sharing | VERIFIED | Agency explicitly chooses an active same-tenant advertiser recipient; deterministic adapter records zero-cost local delivery and exposes no external side effect |
| Client decision | VERIFIED | Only the assigned recipient can read and decide; exactly one option may be selected or the proposal declined; expiry and repeat decisions fail closed |
| Gate 8 API/OpenAPI and browser journey | VERIFIED | Complete C# contract plus desktop/compact agency-to-client proposal journey pass |
| Booking/payment side effects at Gate 8 | ABSENT | Gate 8 records only the immutable client decision; controlled Booking and funding capabilities were introduced by later gates |
| Local canonical database deployment | VERIFIED locally | The named local developer database records proposal migration 008; disposable migration verification remains retained. No staging or production resource changed |

See `docs/evidence/gate-8/` for retained commands and exact outcomes.

## Gates 9–13

Gates 9–13 remain sequence-bound. A document, route label, container, contract, or scaffold is not a product implementation.

| Gate | Status |
|---|---|
| 4 Evidence and Opportunity | VERIFIED locally — owner directed delivered; non-local review pending |
| 5 Canonical Brief | IMPLEMENTED LOCALLY for the latest clear supplied-Brief rule — a clear Brief reaches planning without a fabricated approval in the connected local journey; the complete latest-source C# regression remains pending |
| 6 Inventory truth | VERIFIED locally — implementation and evidence complete; non-local review pending |
| 7 Planning | VERIFIED locally — deterministic eligibility, benchmarks, editable planning and the eleven-agent Inventory Intelligence explanation boundary are implemented; connected proposal evidence and non-local review remain separate |
| 8 Proposal and client decision | VERIFIED locally — implementation and evidence complete; non-local review pending |
| 9 OOH-only campaign mode and proposal inbox | VERIFIED LOCALLY — campaign mode is decided automatically for clear Briefs; a human resolves only material ambiguity; mailbox automation defaults off and requires explicit tenant-administrator opt-in; provider submission is not presented as inbox-delivery evidence. Owner/independent review remains pending |
| 10 Supplier marketplace | VERIFIED LOCALLY — tenant-safe exchange, commercial policy, marketplace-to-plan lineage and selected-option booking confirmation are implemented; owner/independent review pending |
| 11 Campaign delivery and learning | IMPLEMENTED LOCALLY — funding, booking, creative readiness, delivery proof, performance evidence and measurement reports are canonical; the complete Campaign Delivery browser regression remains API-mocked, while connected Brief/proposal paths now prove the packaged local web/API/database/runtime boundary. Booking and Planning projections suppress supplier-private cost, notes, assumptions and objections from client-facing viewers. Owner/independent review remains pending |
| 12 Hardening and certification | IN PROGRESS - current packaging, connected critical journeys and final-tree architecture are locally verified; the latest complete C# suite rerun, remote CI, staging and required external/independent certification evidence remain pending |
| 13 Production launch | BLOCKED by Gate 12 and independent launch decisions |

### Gate 12 capability status

| Capability | Status | Evidence / blocker |
|---|---|---|
| Release API build and complete suite | IMPLEMENTED; build VERIFIED locally | Current API/migrator publish passes in the pinned Linux .NET SDK 10.0.400 image. The last complete retained suite passed 128/128 before the latest source changes; the newest full suite still needs a 10.0.400-capable runner or remote CI |
| Web static/build checks | VERIFIED locally | Lint, type-check, unit 6/6 and host/Linux production builds pass; explicit Vite 8 splitting removes the Linux oversized-main-chunk regression without changing the warning threshold |
| Browser regressions | VERIFIED locally for current critical paths | Retained API-mocked Playwright is 32/32; 4/4 connected local journeys pass through packaged web, API, PostgreSQL and deterministic runtime, including keyboard/focus semantics and visible persisted Inventory Intelligence rationale before selection |
| Deterministic agent runtime | VERIFIED locally | Pytest 31/31 passes; deterministic mode reports all eleven approved handlers and no live or paid provider is used |
| Dependency and local-service checks | VERIFIED locally | Checked .NET, Python and web graphs report zero known vulnerabilities; the canonical local dependency/application stack is healthy and migration/bootstrap/seed jobs complete successfully |
| Complete current-source secret scan | PENDING | Bounded affected-source scans pass; the blocking pinned CI Gitleaks scan has not run against an owner-authorised commit containing the current dirty tree |
| Current final-tree architecture | VERIFIED locally | Complete architecture rerun passes 42/42 after current agent, packaging, accessibility and durable-session changes |
| API readiness and observability | IMPLEMENTED | Fail-closed dependency readiness and privacy-safe correlated local telemetry exist; central export, dashboards, alerts and named response ownership are absent |
| Recovery | IMPLEMENTED locally | Isolated PostgreSQL restore and representative MinIO object-byte reconciliation pass; managed PITR, every object family, measured RPO/RTO and staging restore remain unverified |
| Email automation security/recovery | IMPLEMENTED and API-VERIFIED locally | Provider/security 14/14, workflow recovery 11/11 and complete API 128/128 pass; official Resend trust/reconciliation, sandbox proof and a background email worker remain absent |
| Transactional outbox dispatch | IMPLEMENTED and API-VERIFIED locally | Tenant-bound deterministic claim/lease/heartbeat/retry/dead-letter kernel passes focused checks and the complete API suite; production cross-tenant scheduler identity, broker transport and consumer evidence remain blocked |
| Docling transport security | IMPLEMENTED and focused VERIFIED | Non-development HTTP/credential-bearing redirects fail closed and the focused Release slice passes 5/5; managed deployment evidence is absent |
| Immutable build inputs | IMPLEMENTED with local packaging verification | GitHub action and dependency-image references are content-addressed; API, migrator, runtime and web Linux images build from pinned inputs and non-root final processes. Remote SBOM/vulnerability results, signing and release provenance remain absent |
| Production authentication and sessions | PARTIALLY IMPLEMENTED; provider BLOCKED | The accepted Cognito/OIDC BFF direction remains. The browser session itself is now PostgreSQL-backed: exact pre-restart cookies remain authenticated after API restart and logout revocation remains invalid after a second restart. Cognito/OIDC code flow, provider logout/refresh, MFA policy, provider-token protection and production configuration/sandbox evidence remain pending |
| Genuine integrated E2E | BLOCKED for staging certification | 4/4 connected local critical journeys now prove packaged web, API, PostgreSQL and deterministic runtime together. A production-shaped staging journey with real OIDC, workers and approved provider sandboxes is still absent |
| Accessibility | IMPLEMENTED locally; certification BLOCKED | Connected keyboard/focus and accessibility-tree semantics pass, including route-change focus and cold-load skip behavior. A standards-based automated WCAG 2.2 AA scan plus named manual keyboard/screen-reader review remain required |
| Privacy and independent security | BLOCKED | Processing register, retention/deletion operation, external security review and named Privacy/Legal launch decisions are absent |
| Performance and SLOs | BLOCKED | No retained catalogue/API/worker/import load evidence proves the Section 27 targets |
| Staging and production operations | BLOCKED | Remote CI, immutable application release artefacts, staging rollout, alerts, rollback rehearsal, managed backup/restore and named Operations approval are absent |
| Production certification | BLOCKED | The genuine 30-case cohort, independent cross-functional sign-offs and named human GO are absent |

The last exact retained migration-history capture records migrations 001 through 026. On
2026-08-31 the least-privilege runner applied the 14 pending migrations 013 through 026, its
immediate idempotency rerun applied 0 and it synchronised the then-current governed master data. On
2026-09-01 the existing `advertified-dev` migration job also exited successfully after rebuilding
against the current migration graph, followed by successful bootstrap/seed and healthy API startup.
This pass did not retain a direct post-run migration-history query, so it does not invent an exact
027/028 applied-history claim. Current email correction evidence is retained under
`docs/evidence/gate12-email-automation-security-recovery-20260901/`; the 2026-08-31 delivery pack is
historical.

Local evidence is not remote CI evidence. No tests discovered is not a pass. A proposed ADR is not accepted. An AI cannot approve legal compliance, security, a gate, or production readiness.
