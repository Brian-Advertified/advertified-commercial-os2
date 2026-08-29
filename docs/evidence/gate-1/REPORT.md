# Gate 1 evidence report

**Evidence date:** 2026-08-29  
**Repository/branch:** advertified-commercial-os2 / master  
**Base commit:** `0986f62ad0748289fdafe8f36f5f9a3dabaab4d8`  
**Working tree:** uncommitted owner-review diff  
**Decision:** PENDING — only the named owner may record GO

## Authorised outcome

Gate 1 is limited to architecture guardrails from the normative specification. The work establishes mechanically enforced tenant, command, lifecycle, master-data, deterministic-agent, dependency, complexity, ADR, and evidence boundaries.

Product screens, business journeys, live authentication, live AI providers, paid calls, external actions, cloud changes, production resources, and Gates 2–13 remain out of scope.

## Changes

| Path | Created/changed | Capability | Data impact |
|---|---|---|---|
| `Directory.Build.props`, `.editorconfig` | Created | .NET analyzers, warnings-as-errors, complexity ceiling | None |
| `api/src/Advertified.Commercial.Domain/` | Created | Typed tenant, command, consequence, and lifecycle contracts | None |
| `api/src/Advertified.Commercial.Application/` | Created | Deny-by-default authorization and idempotent command dispatch | None |
| `api/src/Advertified.Commercial.Infrastructure/` | Created | PostgreSQL master-data model, bootstrapper, and migration | No main development database migration was run |
| `api/tests/Advertified.Commercial.Api.Tests/` | Changed | Negative tenancy, idempotency, lifecycle, and migration evidence | Throwaway test database only |
| `shared/contracts/master-data.json` | Changed | Single governed master-data source | None |
| `agent-runtime/` | Changed | Closed 11-agent roster, versioned contracts, deterministic fixture provider | No provider call or database access |
| `web/.oxlintrc.json` | Changed | TypeScript complexity and function-size enforcement | None |
| `tests/architecture/` | Changed | Boundary checks and controlled failing fixtures | None |
| `docs/adr/`, `docs/evidence/` | Changed | Human decision ownership and repeatable evidence process | None |

No agent is marked implemented. The closed roster is only a governed contract boundary.

## Verification

| Check | Exact command | Outcome | Retained evidence |
|---|---|---|---|
| React lint | `cd web && npm run lint` | PASS | 0 warnings/errors |
| React type-check | `cd web && npm run type-check` | PASS | Successful |
| React tests | `cd web && npm test` | PASS | 2 passed |
| React build | `cd web && npm run build` | PASS | Successful production build |
| API Release build | `dotnet build api/Advertified.Commercial.Api.csproj --configuration Release --no-restore` | PASS | 0 warnings/errors |
| API/governance tests | `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --configuration Release --no-restore` | PASS | 18 passed |
| Migration isolation | `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~MasterDataMigrationTests` | PASS | Apply/reapply/protection/history/rollback passed |
| Agent runtime tests | `cd agent-runtime && python -m pytest` | PASS | 9 passed |
| Python lint | `cd agent-runtime && python -m ruff check .` | PENDING | Configured in CI; current local rerun unavailable through the bounded runner |
| Architecture | `python -m pytest tests/architecture -q` | PASS | 20 passed, including evidence-schema integrity |
| os2 services | `docker compose -f infrastructure/docker-compose.yml ps` | PASS | PostgreSQL, Redis, MinIO, and MailHog healthy |
| Remote CI | GitHub Advertified CI workflow | PENDING | Commit/push not authorised |
| Owner review | Complete diff and evidence review | PENDING | Owner/reviewers unassigned |

“No tests discovered” was not treated as a pass. The detailed machine-readable record is `docs/evidence/gate-1/manifest.json`.

## Safety and boundaries

- Cross-tenant negative result: generic denial before idempotency lookup or handler execution.
- Permission-denial result: missing, inactive, wrong-actor, wrong-tenant, and missing-permission cases deny identically.
- Migration/rollback result: passed in a self-removing PostgreSQL 16 test container; the main development database was untouched.
- Live or paid provider used: No.
- Incremental AI cost: 0 minor units.
- Production resource used or changed: No.
- Secrets or production data introduced: No.
- Consequential external action performed: No.
- Product screen or Gate 2 work started: No.

## Unresolved blockers

| Blocker | Decision owner | Smallest next action | Required retest |
|---|---|---|---|
| Current Python Ruff result absent | Developer or CI runner | Run the configured Ruff command | Ruff |
| Remote CI absent | Repository owner | Authorise commit/PR workflow when ready | Full CI |
| Final Gate 1 decision awaits CI | Brian Rabuthu | Review remote CI and confirm dated GO | Diff, evidence, and CI review |
| Gate 2 decisions unresolved | Relevant named owners | Approve auth/session/CSRF, tenancy, migration, and deployment ADRs | Gate-specific checks |

Safe local work is limited to correcting Gate 1 failures or evidence gaps. Gate 2 remains blocked.

## Diff and review

- Unrelated user changes preserved: Yes.
- Automated and targeted file review completed: Yes.
- Complete accountable-owner diff review: Conditional close-out approval received; final confirmation follows CI.
- Files staged: No.
- Commit/push/deploy performed: No.
- Accountable owner: Brian Rabuthu.
- Required reviewers: Remote CI; no additional human reviewer designated.
- Owner decision/date: close-out and Git push authorised on 2026-08-29; final Gate 1 GO pending CI.

An AI prepared this report but did not approve the gate, security, privacy, legal compliance, or production readiness.
