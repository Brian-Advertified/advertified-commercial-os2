# Gate 12 Commercial API readiness work packet

**Owner direction:** continue local production hardening without editing the separately owned screens.

**Verified predecessor:** local commit `40f6b43`; isolated PostgreSQL backup/restore and authenticated
recovery pass, complete Release Commercial API 71/71, registry 2.9.0, Compose and evidence-schema
checks pass. The repository architecture suite remains 21/23 only because of separately owned screen
files. No push, deployment, production resource or live provider was used.

## Bounded requirement

Replace the Commercial API readiness endpoint's process-only success with a fail-closed canonical
database readiness probe. Liveness must continue to answer independently so the deployment platform
can distinguish a running process from an instance that is safe to receive authenticated commercial
traffic.

## Acceptance evidence

1. `/health/live` returns HTTP 200 with only process liveness and does not connect to PostgreSQL.
2. `/health/ready` returns HTTP 503 and business-safe technical check codes when the configured
   canonical database cannot be reached. The response and logs contain no connection string,
   credential, SQL/provider exception, stack trace or tenant data.
3. Readiness returns HTTP 503 when PostgreSQL is reachable but governed master data is unavailable;
   a bare database connection is not sufficient.
4. Readiness returns HTTP 200 only when the canonical database is reachable and governed master-data
   collections exist. The passing path is exercised against a migrated PostgreSQL 16 container.
5. Health endpoints remain unauthenticated technical deployment boundaries; they expose no commercial
   state, provider readiness or tenant scope and perform no mutation.
6. Focused and complete API tests, formatting, architecture checks, Compose validation and retained
   evidence record the exact outcomes.

## Explicitly out of scope

- application/business metrics, distributed tracing, dashboards, alert routing or synthetic checks;
- checking optional/live AI, email, payment, supplier or external provider availability;
- staging/ECS/ALB health-check configuration, deployment, production traffic or cloud mutation;
- claiming SLO, Gate 12 or production-readiness approval;
- editing or testing the separately owned screen implementation.
