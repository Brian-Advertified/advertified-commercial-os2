# Active codebase hardening work packet

**Owner instruction:** 2026-08-30 repository-wide review and correction of duplication, security defects, N+1 database access, legacy code and implementation noise.

**Boundary:** active Advertified Commercial OS 2 source only. Preserve the canonical Brief → Plan → Proposal lifecycle, one `OOH_ONLY`/`FULL_CAMPAIGN` planning path, current tenant/permission rules, deterministic providers and all unrelated committed product work. No production, cloud, paid AI, external send, push or deployment. The owner authorised a local commit on 2026-08-30.

## Verified defects in scope

1. Opportunity-generated Briefs directly duplicate canonical Brief aggregate/source/version SQL.
2. Inventory import detail issues one evidence query per candidate and returns an unbounded candidate set.
3. Inventory extraction and publication issue database commands per row/candidate, which cannot support 10,000+ products efficiently.
4. Email automation list issues run and attachment queries per message.
5. Proposal approved-plan choices issue plan-line and objection queries per plan.
6. Marketplace inventory snapshot joins every historical rate and availability row instead of the current deterministic observation.
7. Marketplace RFQ status filtering occurs after paging, causing incomplete or empty result pages.
8. Marketplace query text/status/channel values are insufficiently bounded or validated, and publish concurrency is not explicitly serialized.
9. Cookie-protected unsafe requests treat TRACE as a safe method; anonymous entry points have no application rate-limit boundary.
10. Rejected/obsolete source trees and generated caches remain inside the working repository and pollute code discovery.
11. Repeated browser formatting and endpoint command-execution helpers create avoidable drift.
12. Rejected candidates retain truthful blocking validation but incorrectly contribute to the import-level publication blocker count.
13. Scheduled future rates and availability observations can displace currently effective facts in inventory and marketplace projections.
14. Anonymous rate-limit partitions use the load balancer address unless forwarded headers are accepted only from explicitly trusted proxies.
15. Analyzer, TypeScript and browser-contract regressions prevent a clean production build and full user-journey run.

## Required outcome

- one internal Brief persistence owner reused by interactive and Opportunity paths;
- set-based/batched inventory persistence and constant-query read composition;
- bounded cursor-paged import review with server counts used for publish readiness;
- constant-query email and approved-plan list composition;
- deterministic currently effective inventory/marketplace facts, SQL-side filters, bounded allow-listed query values and serialized listing publication;
- preserved RLS, permission, idempotency, optimistic-concurrency and audit/outbox boundaries;
- rate-limited anonymous browser/provider entry points and no TRACE CSRF exemption;
- obsolete local source noise removed and generated `.artifacts/` output excluded from Git;
- rejected rows excluded only from the import-level blocker count while their validation evidence remains unchanged;
- explicit trusted-proxy configuration before client-IP rate-limit partitioning;
- shared browser presentation helpers and reuse of the existing command endpoint executor;
- no live provider calls or new duplicate workflow.

## Acceptance evidence

- complete diff inspection;
- architecture and master-data projection checks;
- affected .NET acceptance tests, including multi-observation marketplace and page-filter regressions;
- API Release build and complete API test project;
- agent-runtime tests;
- web lint, unit tests, type/build and affected Playwright journeys;
- Docker Compose validation and current development service health;
- verified diff committed only after the complete local evidence set passes.
