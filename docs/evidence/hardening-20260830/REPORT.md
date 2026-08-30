# Codebase hardening evidence report

**Evidence date:** 2026-08-30

**Repository:** advertified-commercial-os2

**Branch:** master

**Base commit:** `5744e333db4e56f089498663fac8c12aa1069d7f`

**Live provider used:** No

**Production resources used:** No

## Implemented

- Consolidated duplicated Brief, Opportunity, planning, proposal, email and endpoint persistence/composition paths behind cohesive internal owners.
- Replaced per-record inventory extraction/publication and N+1 view composition with bounded, set-based persistence and cursor-paged reads.
- Kept rejected candidate validation evidence intact while excluding rejected rows from the import-level publication blocker count.
- Selected currently effective inventory rates and observed availability facts without allowing scheduled future facts to displace them.
- Moved marketplace filtering into bounded SQL queries and preserved tenant, RLS, permission, concurrency, idempotency, audit and outbox boundaries.
- Removed TRACE from the safe browser-method set, added human-safe rate limits and security headers, and accepted forwarded client addresses only from explicitly configured trusted proxies.
- Consolidated repeated browser formatting and removed unused template assets. Generated `.artifacts/` output is ignored and cannot enter Git.

## Verification

| Check | Exact command | Final result |
|---|---|---|
| Release API build | `cd api; dotnet build Advertified.Commercial.Api.csproj --configuration Release --no-restore` | PASS - 0 warnings, 0 errors |
| Complete API suite | `cd api; dotnet test tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --configuration Release --no-build` | PASS - 46/46 |
| Architecture | `python -m pytest tests/architecture -q` | PASS - 23/23 |
| Agent runtime | `cd agent-runtime; python -m pytest tests -q` | PASS - 18/18 |
| Web static/unit/build | `cd web; npm run lint; npm test -- --run; npm run type-check; npm run build` | PASS - lint/type/build clean and 4/4 unit tests |
| Browser journeys | `cd web; npx playwright test` | PASS - 20/20 desktop and compact journeys |
| Compose definition | `docker compose -f infrastructure/docker-compose.yml config --quiet` | PASS |
| Local dependencies | `docker compose -f infrastructure/docker-compose.yml up -d --build --wait` | PASS - PostgreSQL, Redis, MinIO, ClamAV, Docling and MailHog healthy |
| Diff whitespace | `git diff --check` | PASS |

## Corrected checks

1. The first ordinary API build exposed analyzer error CA1859 in proposal option validation. The concrete collection type now matches actual use and the normal Release build passes without suppressions.
2. The first web build exposed an unused proposal formatter, while lint exposed inventory-page complexity. Shared presentation formatting and smaller workflow components corrected both failures.
3. The first full browser run failed the inventory journey in both viewports because its strict mock response omitted the newly required candidate counts and cursor. The mock contract was corrected; the targeted 2/2 and final full 20/20 runs pass.
4. Generated `.artifacts/` contained 978 local build files. The directory is now ignored. The execution environment refused physical recursive deletion after the exact workspace-local path was verified, but none of those files is tracked, staged or eligible for the commit.

## Production boundary

This hardening packet is locally implemented and repeatably verified; it does not approve production readiness. Remaining Gate 10/11 capabilities, the thirty-case certification pack, staging deployment, recovery rehearsal, production proxy/network values, observability and named independent security/privacy/operations reviews remain required. No push or deployment was performed.
