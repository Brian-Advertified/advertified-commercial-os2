# Gate 12 Commercial API readiness evidence

**Evidence date:** 2026-08-31

**Base commit:** `40f6b43e47755f84b8c66e0590c2c05782c39f78`

**Live provider or production resource used:** No

## Implemented

- Replaced process-only Commercial API readiness with a canonical dependency probe. Liveness remains
  an independent process check; readiness now requires a reachable PostgreSQL database and at least
  one governed master-data collection before the instance may report ready for traffic.
- Added safe HTTP 503 responses for an unavailable database and for a reachable but unseeded database.
  Check codes distinguish these conditions without returning a connection string, credential,
  exception, SQL, stack trace, tenant identifier or commercial data.
- Added source-generated warning events for the two readiness failures. The handler deliberately does
  not log caught exception objects or provider details.
- Moved health routing out of `Program.cs` into a cohesive 80-line endpoint module. `Program.cs` is now
  349 lines, the endpoint file is 80 lines and the focused test file is 89 lines.
- Regenerated the retained OpenAPI contract through `tools/generate-openapi.mjs`. The public contract
  now declares `HealthResponse` content for liveness/readiness and an explicit readiness 503 response.

## Verification

| Check | Result |
|---|---|
| Complete Release API/database/security suite | PASS - 71/71 in 2m19s |
| Liveness and dependency-readiness acceptance | PASS - 2/2 in 14s against unreachable and real PostgreSQL 16 targets |
| Health plus retained OpenAPI contract | PASS - 4/4 in 19s |
| Release API build | PASS - zero warnings and zero errors |
| Focused C# formatting | PASS |
| Complete architecture suite | BLOCKED - 21/23; separately owned screen work still contains a 521-line CSS file and inline governed `PENDING` |
| Web/browser regression | NOT RUN - another agent owns and is actively changing the screen tree |

## Acceptance behavior

1. With an unreachable database, `/health/live` returns 200 and only `process`; `/health/ready`
   returns 503 with `process` and `database-unavailable`.
2. A fully migrated but unseeded PostgreSQL 16 database returns 503 with `process`, `database` and
   `master-data-unavailable`. Connectivity alone cannot produce a ready claim.
3. After the governed registry is applied, the same target returns 200 with only `process`, `database`
   and `master-data`.
4. Health endpoints remain unauthenticated deployment probes but expose no tenant/commercial state and
   perform no mutation. They make no claim about optional or live external providers.

## Closed regression

The first retained OpenAPI check failed because the new typed 503 response changed the running
contract while the retained generated JSON was still the prior version. No assertion was weakened.
The repository generator refreshed the document, its diff was limited to health response content and
the `HealthResponse` schema, and the exact contract suite then passed 4/4.

## Remaining boundary

This packet establishes local API dependency readiness only. It does not prove ECS/ALB health-check
configuration, telemetry export, dashboards, alerts, synthetic authentication, SLOs, worker/provider
readiness, performance, staging or production traffic. Those checks plus security/privacy and named
independent operational approval remain required. No push, deployment, live provider call or
production mutation was performed.
