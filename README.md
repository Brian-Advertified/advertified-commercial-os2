# Advertified Commercial OS

Advertified is a marketing intelligence and campaign operating system. This repository is the clean implementation baseline for the locked stack:

- React 19.2.0, TypeScript, and Vite for the web application
- C# 14/.NET 10 for the canonical Commercial API
- Python 3.12-compatible FastAPI for typed agent proposals
- PostgreSQL 16 with PostGIS and pgvector
- Redis, MinIO, and MailHog for local non-production infrastructure

The foundation builds and tests locally. Brian Rabuthu recorded local Gate 2 GO on
2026-08-29. Brian Rabuthu recorded local Gate 3 GO for the implemented authenticated shell;
Gate 4 and later product journeys remain blocked pending exact approved work packets.

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
dotnet run --project api/Advertified.Commercial.Api.csproj --urls http://localhost:5000
```

Remove `ConnectionStrings__CommercialDatabase` from the terminal environment when the API
stops. Never put the local password in tracked application settings.

Agent runtime:

```powershell
.\.venv\Scripts\Activate.ps1
python -m uvicorn main:app --app-dir agent-runtime --reload --port 8000
```

Local endpoints:

| Surface | URL |
|---|---|
| Authenticated web application | http://localhost:5173/sign-in |
| Commercial API description | http://localhost:5000 |
| Commercial API liveness | http://localhost:5000/health/live |
| Commercial API Swagger | http://localhost:5000/swagger |
| Agent runtime description | http://localhost:8000 |
| Agent runtime liveness | http://localhost:8000/health/live |
| MinIO API / console | http://localhost:59000 / http://localhost:59001 |
| MailHog | http://localhost:58025 |
| PostgreSQL | localhost:55432 |
| Redis | localhost:56379 |

The runtime description intentionally reports zero implemented agents and a disabled provider. Do not change those claims until agent contracts and evaluations actually exist.

The Gate 3 web application starts at `/sign-in`, creates only the approved local opaque
browser session and shows real database-backed workspaces. An identity with no active
canonical membership receives the truthful empty-access state; the application never seeds
or fabricates a workspace, task, notification or dashboard count.

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

Brian Rabuthu authorised migration `202608290002_CanonicalCommercialFoundation` against
the `advertified-os2-dev-postgres-1` local database on 2026-08-29; that migration is now
applied. Every future migration requires its own exact target approval.

## Verify before handing off

```powershell
npm --prefix web run lint
npm --prefix web run type-check
npm --prefix web test
npm --prefix web run test:e2e -- --workers=1
npm --prefix web run build

dotnet build api/Advertified.Commercial.Api.csproj --configuration Release
dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --configuration Release

.\.venv\Scripts\Activate.ps1
python -m pytest agent-runtime
python -m ruff check agent-runtime
python -m pytest tests/architecture -q

docker compose -f infrastructure/docker-compose.yml config --quiet
docker compose -f infrastructure/docker-compose.yml ps
```

All four Compose services must be healthy. PostgreSQL health includes a version-16 check and verifies `pgcrypto`, `postgis`, and `vector`.

## Architectural boundary

The Commercial API is the only canonical commercial write boundary. Python may propose typed outputs through authorised API contracts and must not access PostgreSQL directly. React contains no database/provider credentials. AI cannot approve, spend, publish, book, invoice, or communicate externally.

Opportunity discovery and a supplied Brief are separate paths. The canonical delivery lifecycle is:

Brief → Plan → Proposal → Client Decision → Funding → Booking → Readiness → Live → Proof → Measurement → Learning.

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
