# Advertified Commercial OS

Advertified is a marketing intelligence and campaign operating system. This repository is the clean implementation baseline for the locked stack:

- React 19.2.0, TypeScript, and Vite for the web application
- C# 14/.NET 10 for the canonical Commercial API
- Python 3.12-compatible FastAPI for typed agent proposals
- PostgreSQL 16 with PostGIS and pgvector
- Redis, MinIO, ClamAV, Docling, and MailHog for local non-production infrastructure

Local non-production implementations and release checks now cover the canonical product lifecycle
through Gate 11, including Campaign Delivery through approved measurement. Gate 12 certification
preparation remains in progress. No owner gate decision, remote CI result, staging exercise or
production approval is claimed.

Current local verification of the uncommitted 2026-09-01 working tree is:

| Surface | Result | Qualification |
|---|---|---|
| Commercial API pinned Linux build | PASS | Current API and migrator publish with repository-pinned .NET SDK 10.0.400 |
| Complete current-source API suite | BLOCKED locally | Last complete retained suite passed 128/128 before the latest Inventory Intelligence, Brief-readiness and packaging changes; this Windows host has SDK 10.0.103 while the repo requires 10.0.400 |
| Web | PASS - lint, type-check, unit 6/6 and production build | Host and pinned Linux builds pass; explicit vendor splitting removes the oversized-main-chunk warning without raising the threshold |
| Connected browser journeys | PASS - 4/4 | Real local web/API/PostgreSQL/runtime paths cover keyboard/accessibility shell behavior, Proposal inbox, clear Brief to OOH planning, and Brief through approved PDF/share |
| API-mocked browser matrix | PASS - 32/32 retained | UI/contract regression evidence; not a substitute for staging certification |
| Deterministic agent runtime | PASS - pytest 31/31 | Eleven approved zero-cost handlers including Inventory Intelligence; no live or paid provider call |
| Governed master data | PASS - registry 2.12.0 | Generated C#, TypeScript and Python projections match |
| Durable browser sessions | PASS locally | PostgreSQL-backed opaque sessions survive API restart; logout revocation survives a second restart. Production Cognito/OIDC remains pending |
| Architecture guardrails | PASS - 42/42 | Complete final-tree architecture rerun after current packaging, agent, accessibility and session changes |
| Dependency audits | PASS - no known findings in the checked .NET, Python and web graphs | Local audit evidence only |
| Local dependencies | PASS | The canonical `advertified-dev` stack is healthy; migration/bootstrap/seed jobs completed successfully |
| Complete current-source secret scan | PENDING | Bounded scans pass; the blocking pinned CI scan has not run against an owner-authorised commit |
| Final-image SBOM/vulnerability scan | PENDING | Pinned Syft/Trivy CI steps are implemented but have no remote result for this dirty tree |

The earlier 128/128 API and 32/32 mocked-browser results remain retained evidence, but the full C#
denominator must be rerun after the latest source changes before it can be called current. None of
these local results is staging certification or production approval.

## Start here

Read these in order before changing code:

1. `AGENTS.md`
2. `docs/DEVELOPMENT_ENTRY_GATE.md`
3. `docs/spec/README.md`
4. `docs/IMPLEMENTATION_PLAN.md`
5. the applicable proposed/approved ADRs

## Prerequisites

- Git
- Docker Desktop with Docker Compose v2
- Node.js 22
- .NET 10 SDK
- Python 3.12

AWS credentials are not required for the current baseline. Live and paid AI calls are disabled.

## One-time setup

From `C:\Users\CC KEMPTON\source\advertified-commercial-os2` in PowerShell:

```powershell
Copy-Item infrastructure/env.example infrastructure/.env
docker compose -f infrastructure/docker-compose.yml up -d --build --wait

Set-Location web
npm ci
Set-Location ..

py -3.12 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -r agent-runtime/requirements-dev.txt

dotnet restore api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj
```

Change only the local passwords in `infrastructure/.env`. The file is ignored and must never be committed.

## Run the three applications

Use three PowerShell terminals from the repository root.

Web:

```powershell
npm --prefix web run dev
```

Commercial API:

```powershell
$env:ConnectionStrings__CommercialDatabase = '<connection from your ignored infrastructure/.env>'
$env:InventoryProtection__AccessKey = '<MINIO_ROOT_USER from your ignored infrastructure/.env>'
$env:InventoryProtection__SecretKey = '<MINIO_ROOT_PASSWORD from your ignored infrastructure/.env>'
dotnet run --project api/Advertified.Commercial.Api.csproj --urls http://localhost:5000
```

Remove the connection and inventory-protection credentials from the terminal environment when
the API stops. Never put local passwords in tracked application settings.

Agent runtime:

```powershell
.\.venv\Scripts\Activate.ps1
python -m uvicorn main:app --app-dir agent-runtime --reload --port 8000
```

Local endpoints:

| Surface | URL |
|---|---|
| Public website | http://localhost:5173/ |
| Authenticated web application | http://localhost:5173/sign-in |
| Commercial API description | http://localhost:5000 |
| Commercial API liveness | http://localhost:5000/health/live |
| Commercial API Swagger | http://localhost:5000/swagger |
| Agent runtime description | http://localhost:8000 |
| Agent runtime liveness | http://localhost:8000/health/live |
| MinIO API / console | http://localhost:59000 / http://localhost:59001 |
| ClamAV TCP scanner | localhost:53310 |
| MailHog | http://localhost:58025 |
| PostgreSQL | localhost:55432 |
| Redis | localhost:56379 |

### Connected local proposal workspace

The established connected development stack is available at
`http://localhost:3017/sign-in`. Reuse that stack; proposal verification must not create a
per-test Compose project or Testcontainers database.

After a local database reset, provision the deterministic workspace prerequisites once:

```powershell
npm --prefix web run seed:local-proposals
```

The command inspects and executes only inside the exact running
`advertified-dev-postgres-1` non-production container. It does not build, pull or create a
Docker image, container or volume. The idempotent seed adds two clearly named `Local Demo`
OOH products and a local client approver so the production-shaped workflow can be exercised:

1. supply a clear Brief with Johannesburg geography, timing and budget;
2. build the audience direction and media mix;
3. confirm eligible source-linked inventory and approve the reconciled media plan;
4. prepare and approve the proposal;
5. create the branded PDF and share it with the local client approver.

Local Demo inventory is verification data, not supplier truth for an external client. Real
proposals must use supplier evidence that has passed the governed import, independent review
and publication workflow. The local email/provider profile is deterministic, so sharing records
access and the recipient decision boundary but does not send an external email.

The runtime defaults to a disabled provider and reports no active agent handlers. Its
Development/Test-only deterministic mode exposes all eleven approved zero-cost handlers from the
closed roster, including Inventory Intelligence. No live provider is configured or permitted.

The public website starts at `/`. The authenticated application starts at `/sign-in`, creates only
the approved local opaque browser session and shows database-backed workspaces and persisted work.
An identity with no active canonical membership receives the truthful empty-access state; the
application never fabricates a workspace, task, notification or dashboard count.

## Database migrations

The API never applies migrations at startup. The dedicated migration runner requires an
explicit `--apply`, an operator-supplied migration connection, and already provisioned
least-privilege `advertified_migrator` and `advertified_app` group roles. Schema migrations
do not create cluster roles:

```powershell
$env:ADVERTIFIED_MIGRATION_CONNECTION_STRING = '<approved migration-only connection>'
dotnet run --project api/src/Advertified.Commercial.DatabaseMigrator/Advertified.Commercial.DatabaseMigrator.csproj -- --apply
Remove-Item Env:ADVERTIFIED_MIGRATION_CONNECTION_STRING
```

Brian Rabuthu authorised the named local developer database under the standing local-only
direction. On 2026-08-31 the dedicated least-privilege runner applied the 14 pending migrations
`202608300013_SupplierMarketplace` through `202608310026_EmailDeliveryDurability`; the immediate
idempotency rerun applied 0, and the runner synchronised 71 governed master-data collections. That
local database now records migrations 001 through 026. This is local development evidence only:
staging and production migrations still require explicit release authority and their own migration,
backup, restore and rollback evidence.

## Verify before handing off

```powershell
npm --prefix web run lint
npm --prefix web run type-check
npm --prefix web test
npm --prefix web run master-data:check
npm --prefix web run openapi:generate
npm --prefix web run test:e2e -- --project=desktop --workers=1
npm --prefix web run test:e2e -- --project=compact --workers=1
npm --prefix web run test:e2e
npm --prefix web run build

dotnet build api/Advertified.Commercial.Api.csproj --configuration Release
dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --configuration Release

.\.venv\Scripts\Activate.ps1
Push-Location agent-runtime
python -m pytest
python -m ruff check .
Pop-Location
python -m pytest tests/architecture -q

docker compose -f infrastructure/docker-compose.yml config --quiet
docker compose -f infrastructure/docker-compose.yml ps
```

All six Compose services—PostgreSQL, Redis, MinIO, ClamAV, Docling and MailHog—must be healthy.
PostgreSQL health includes a version-16 check and verifies `pgcrypto`, `postgis`, and `vector`.
The governed master-data registry is version `2.12.0`, effective from 2026-09-01; generated C#,
TypeScript and Python projections must match it exactly.

## Architectural boundary

The Commercial API is the only canonical commercial write boundary. Python may propose typed outputs through authorised API contracts and must not access PostgreSQL directly. React contains no database/provider credentials. AI cannot approve, spend, publish, book, invoice, or communicate externally.

Opportunity discovery and a supplied Brief are separate paths. The canonical delivery lifecycle is:

Brief → Plan → Proposal → Client Decision → Funding → Booking → Readiness → Live → Proof → Measurement → Learning.

A supplied Brief may name its client directly; client pre-registration is not required. The API owns
the tenant-scoped client record and keeps the client display name visible through Brief and Planning.
Clear Briefs receive an automatic campaign-mode decision, while a human is asked only to resolve
materially unclear details.

## Repository map

| Path | Responsibility |
|---|---|
| `web/` | React application |
| `api/` | C# Commercial API and tests |
| `agent-runtime/` | Provider-disabled Python runtime and tests |
| `shared/` | Versioned schemas and master/reference data |
| `infrastructure/` | Local Docker environment |
| `tests/architecture/` | Executable repository boundaries |
| `docs/spec/` | Complete split normative v1.1 specification |
| `docs/adr/` | Decision records; status matters |

Proprietary. All rights reserved.
