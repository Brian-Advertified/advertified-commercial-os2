# Release stabilisation work packet

**Owner direction:** 2026-08-31 attached implementation mandate from Brian Rabuthu.

**Target:** local `master` at clean-parent reference `53209e7`, with intentional concurrent,
uncommitted Gate 11, hardening, professional-workbench and public-site work preserved.

**Authorised boundary:** local non-production implementation and verification only. No commit,
pull, push, branch change, cloud mutation, production data, live/paid model call, production email,
payment, supplier commitment or publication.

## Preceding-gate evidence

The current capability ledger records Gate 10 verified locally, with owner and independent review
pending. Retained Gate 10 evidence covers tenant-safe marketplace exchange, commercial policy,
marketplace-to-plan lineage and selected-option booking confirmation. This packet completes the
Gate 11 browser delivery vertical and the locally verifiable Gate 12 release checks; it does not
claim an owner gate decision or production greenlight.

## Bounded implementation

1. Complete the existing Campaign Delivery browser journey from accepted proposal through funding,
   booking, creative reviews, launch/completion, supplier proof, performance evidence and approved
   measurement report. Consolidate the current workbench; do not create a parallel workflow.
2. Finish and prove the supplier delivery-proof request query/endpoint with permission, tenant,
   booking, campaign, proof-state, safe-projection, RLS and migration boundaries.
3. Remove the Vite oversized-main-chunk warning through route-level lazy loading and shared
   Suspense handling, without raising the warning threshold.
4. Fix every locally reproducible regression exposed by the required frontend, architecture,
   backend, migration, contract, runtime, Compose and recovery checks.
5. Reconcile status and operating documentation only after the corresponding evidence is rerun.

## Acceptance evidence

- Campaign Delivery Playwright passes independently on desktop and compact.
- Complete desktop, complete compact and normal configured Playwright suites pass.
- Web lint, TypeScript, focused tests, master-data check, OpenAPI generation/check and production
  build pass with no oversized-chunk warning.
- Architecture boundaries pass without weakening guards.
- Release API build has zero warnings/errors; the full C# suite, migration/OpenAPI/master-data,
  tenant-safe/human-safe boundaries, real PostgreSQL Booking-to-measurement journey and isolated
  backup/restore acceptance pass.
- Complete deterministic Python runtime and mocked contract suites pass with live providers disabled
  and zero paid calls.
- Development Compose validates and required dependency containers are healthy.
- The complete diff is inspected; no unrelated work is reverted, no secret is added, and the tree
  remains unstaged and uncommitted.

## Truthful completion boundary

Locally green engineering checks may verify implementation. Production remains blocked until the
named Product, Engineering, QA, Commercial, Inventory Operations, Security/Privacy and Operations
reviewers complete required independent review, Legal/Privacy decisions are recorded, staging and
production-provider exercises are authorised, the thirty genuine accepted cases pass, and a human
owner records GO.

## Evidence log

### Implemented result

- The API-mocked Campaign Delivery browser UI/contract regression covers accepted proposal, purchase order,
  invoice, payment, Campaign, confirmed Booking coverage, creative production, separate brand and
  supplier review, creative approval, launch, completion, supplier proof, proof review, performance
  evidence, evidence review, measurement report and report approval. It does not constitute a
  connected deployed-system E2E journey; real API/browser integration remains blocked evidence.
- `GET /api/v1/tenants/{tenantId}/delivery-proof-requests` requires
  `delivery_proof_submit`, is tenant-scoped and bounded, and returns only confirmed Bookings on
  completed Campaigns with an explicit proof request. Its projection excludes buyer-private
  commercial fields, returns deterministic latest-proof state and permits replacement only after a
  rejection. The migration fixes the security-definer search path, restricts execution and has a
  tested down path.
- Booking responses expose supplier cost and supplier notes only to active supplier roles in the
  addressed supplier tenant. Planning responses omit supplier cost/subtotal for every outward role
  and omit internal assumptions/objections for advertiser roles.
- Governed registry `2.9.1` supplies currency minor-unit and Brief-marker metadata to generated C#,
  TypeScript and Python projections. Reusable presentation and deterministic Brief parsing no longer
  assume one market, locale or hard-coded minor-unit scale.
- Route-level `React.lazy` boundaries separate the public site, authenticated shell and deferred
  pages. The warning threshold was not raised.
- Brief and Planning responses project the client display name through tenant-scoped API queries,
  keeping the client visible through the workflow without requiring client pre-registration or
  exposing a client identifier as primary UI content.
- Proposal-inbox automation defaults to off and requires explicit tenant-administrator opt-in. A
  provider submission is labelled `Sent`; delivery is not claimed without delivery evidence.
- Notifications replace the previous item before showing a new status and do not intercept page
  actions, including under the configured concurrent browser suite.

### Executed verification — 2026-08-31

- `npm --prefix web run lint` — PASS, zero warnings/errors.
- `npm --prefix web run type-check` — PASS.
- `npm --prefix web test` — PASS, 6/6.
- `npm --prefix web run master-data:check` — PASS, registry `2.9.1` and generated projections match.
- `npm --prefix web run openapi:generate` — PASS; retained OpenAPI regenerated from the running API.
- `npm --prefix web run build` — PASS, 1,876 modules; main JavaScript 482.86 kB
  (137.56 kB gzip) and no oversized-chunk warning.
- Campaign Delivery Playwright — PASS independently, desktop 1/1 and compact 1/1.
- Complete serial Playwright — PASS, desktop 16/16 and compact 16/16.
- Normal configured `npm --prefix web run test:e2e` — PASS, 32/32 in 2.3 minutes with four workers.
- Focused OOH Proposal-inbox regression — PASS, desktop 1/1 and compact 1/1 with explicit opt-in.
- Focused Marketplace and commercial-policy regressions — PASS, desktop 2/2 and compact 2/2.
- Focused Campaign Delivery regression after the notification fix — PASS, desktop 1/1 and compact 1/1.
- `npm --prefix web audit --omit=dev --audit-level=high` — PASS, zero known
  production-dependency vulnerabilities.
- `python -m pytest tests/architecture/test_boundaries.py tests/architecture/test_gate1_enforcement.py -q`
  — PASS, 23/23.
- Authored-source line-limit audit — PASS; 228 changed or untracked authored source files were
  inspected and none exceeds 400 physical lines. `web/src/App.css` is exactly 400 lines.
- `dotnet build api/Advertified.Commercial.Api.csproj --configuration Release --no-restore` — PASS, zero
  warnings/errors.
- Complete `Advertified.Commercial.Api.Tests` Release suite — PASS, 87/87. This includes the
  migration, retained OpenAPI, governed master-data, tenant/human-safe boundaries, supplier-proof
  queue and projection regressions, real PostgreSQL Booking-to-measurement journey, and isolated
  database backup/restore acceptance.
- `python -m ruff check agent-runtime` — PASS.
- `python -m pytest agent-runtime` with the provider disabled/deterministic fixtures — PASS, 28/28.
- Isolated hash-locked runtime and development dependency installation/audit — PASS; no known
  vulnerabilities and lock regeneration produced no dependency diff.
- `docker compose -f infrastructure/docker-compose.yml config --quiet` — PASS. PostgreSQL, Redis,
  MinIO, ClamAV, Docling and MailHog were healthy; PostgreSQL `16.15` exposed `pgcrypto 1.3`,
  `postgis 3.6.4` and `vector 0.8.6`.
- `git diff --check` — PASS. Gitleaks found no secret in the tracked diff or untracked text files;
  conflict-marker and sensitive-filename scans were empty.
- `git status --short` confirms the intentional changes remain unstaged and uncommitted;
  `.artifacts/`, `artifacts/`, Playwright results and reports remain ignored and untracked.

### Inherited non-release formatting diagnostic

`dotnet format api/Advertified.Commercial.Api.csproj --verify-no-changes --no-restore --verbosity minimal`
returns exit 1 for clean-parent whitespace/end-of-line drift in `BrowserSessionEndpoints.cs`,
`OpportunityTaskEndpoints.cs`, `OpportunityWorkflowEndpoints.cs` and `ProposalEndpoints.cs`.
These files are unrelated to this work packet and were not mass-rewritten. The scoped formatter check
covering every changed or new C# file passes, as do the Release build and all 87 API tests. The
repository CI workflow does not declare the whole-repository formatter diagnostic as a release job.

No live or paid model/provider, production email, production payment, supplier communication,
cloud mutation, production data or production resource was used.

### External launch blockers

- No remote CI result or published release artefact exists for this uncommitted working tree.
- Product, Engineering, QA, Commercial, Inventory Operations, Security/Privacy, Legal and
  Operations reviews and owner decisions are not recorded.
- Staging, real-provider and production-environment exercises are not authorised or complete.
- The required thirty genuine accepted cases have not been executed.
- A representative local MinIO object-byte restore is verified. Managed PostgreSQL point-in-time
  recovery, managed object-store recovery across every object family, retention/encryption policy,
  staging execution and measured RPO/RTO remain unproved.
- Privacy-safe local API completion logs and framework request Activity/metric proof are verified.
  Production performance/load evidence, central telemetry export, dashboards, alarms, sampling,
  on-call ownership and incident exercises remain incomplete.
- The model has tenant-wide supplier membership but no separate resource-assignment model for a
  non-admin `supplier_user`; the owner must decide whether narrower assignment is required before
  production.
- A named human owner has not recorded production GO.
