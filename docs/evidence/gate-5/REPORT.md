# Gate 5 evidence report

**Evidence date:** 2026-08-29

**Repository/branch:** advertified-commercial-os2 / master

**Base commit:** `57c8db5df8762e68f1beb72aaba633e27d812ab1`

**Working tree at evidence capture:** uncommitted review

**Owner direction:** continue sequential local gates without repetitive approval pauses —
Brian Rabuthu, 2026-08-29

## Delivered outcome

Both Gate 5 entry paths now produce one canonical, versioned Brief without inventing missing
facts. A supplied Brief retains the original UTF-8 text and SHA-256 digest. An Opportunity at
`BRIEF_READY` can use the deterministic zero-cost `brief_drafting` proposal only after the
exact StrategyVersion and evidence are approved. The Commercial API remains the sole writer.

One assigned agency operator can create, structure, submit and confirm the Brief, including
self-confirmation in a one-person agency. The browser and default API request do not ask for
an advertiser identity. Advertiser roles do not receive `brief_approve`; advertiser approval
belongs at Proposal / Client Decision. Opportunity-backed confirmation advances the lifecycle
to `PLANNING`.

The implementation includes tenant-scoped CampaignBrief, source, immutable version and
evidence-link records; governed source types and permissions; typed C# and Pydantic contracts;
source/fact/assumption/unknown separation; exact task assignment; idempotency, concurrency,
audit and outbox behavior; retained OpenAPI; and supplied/Opportunity browser routes.

## Verification

| Check | Outcome |
|---|---|
| Commercial API and migration runner Release builds | PASS — .NET 10/C# 14, 0 warnings and 0 errors |
| Complete C# suite | PASS — 33 passed |
| Gate 5 PostgreSQL lifecycle | PASS — supplied and Opportunity paths; one agency operator confirms both |
| Agent runtime | PASS — 16 passed; Ruff passed |
| Architecture guardrails | PASS — 21 passed |
| Web lint/tests/build | PASS — lint, 4 tests and production build passed |
| Browser acceptance | PASS — 10 cases across desktop and compact viewports |
| Web runtime dependency audit | PASS — 0 vulnerabilities |
| Retained OpenAPI v1 | PASS — running and retained contracts match |
| Compose validation and health | PASS — PostgreSQL, Redis, MinIO and MailHog healthy |

The database-backed Gate 5 acceptance proves verbatim source and digest retention, explicit
unknown budget, linked revision history, both entry paths, approved Strategy/evidence
prerequisites, four persisted agent runs, five completed steps, zero incremental AI cost,
agency-operator confirmation and the `BRIEF_READY` → `PLANNING` transition. The complete suite
also proves that an advertiser cannot confirm the Brief. It covers empty migration,
reapply/bootstrap, rollback, 27 forced-RLS tables, tenant isolation,
retained OpenAPI, idempotency, concurrency, audit/outbox correlation and safe errors.

## Corrected checks

1. `dotnet build Advertified.sln` failed with `MSB1009` because this repository uses project
   files rather than a solution. The API and migrator project builds both passed.
2. The first OpenAPI CLI call lacked its generation-only connection setting and failed closed.
   The documented Development generation environment produced the retained contract.
3. The first Ruff run found one import-order finding; formatting corrected it and the final
   Ruff run passed.
4. Early Brief Playwright runs exposed a missing mock ETag, a collapsed source assertion and
   an ambiguous duplicate status locator. Those fixtures/assertions were corrected; the full
   final browser run passed 8/8.
5. The first complete C# run expected the Gate 4 count of 23 protected tables and reported
   actual 27 after the four Gate 5 tables. The exact assertion was updated; the final suite
   passed 33/33. A browser expectation for the revised human-safe assignment wording was also
   aligned before the final run.

## Safety and remaining non-local work

- Live or paid provider used: No.
- External network/tool call by an agent: No.
- Incremental AI cost: 0 minor units.
- Production resource or data used: No.
- Main local database migration applied: No.
- External commercial action or communication: No.
- Commit, push, merge, release or deployment: None.
- Pending: owner gate review, remote CI, and independent Engineering, Security/Privacy and
  Operations review.

The local Gate 5 implementation and its repeatable evidence are complete. The implementing AI
did not approve the gate, security, privacy, legal compliance or production readiness.
