# Gate 10 partial evidence report — supplier marketplace exchange

**Evidence date:** 2026-08-30

**Repository:** advertified-commercial-os2

**Branch:** master

**Scope:** first marketplace listing and RFQ exchange vertical only

**Live provider used:** No

**Production resources used:** No

**Incremental AI cost:** 0

## Implemented

- Supplier-owned listings publish immutable, minimal projections of exact reviewed product,
  rate and availability versions.
- Buyers search published projections without receiving supplier source files, source locators,
  review records, object keys, addresses or coordinates.
- Buyer RFQs retain both counterparty tenants; PostgreSQL row-level security permits only those
  counterparties to read the exchange.
- Buyers create and explicitly send RFQs, addressed suppliers submit one immutable response,
  and buyers accept the exact unexpired response version.
- Draft and archived listings remain hidden from other tenants. Expired responses, wrong
  suppliers and stale versions fail closed.
- Every successful command is permissioned, idempotent, audited and paired with an outbox event.
- The local send transition only exposes the RFQ inside Advertified. No email, booking, payment,
  invoice, supplier-system mutation or other external action occurs.
- React provides role-aware buyer and supplier screens. The screens state that acceptance does
  not create a booking.

## Verification

| Check | Exact command | Final result |
|---|---|---|
| Isolated Release API build | `dotnet build api/Advertified.Commercial.Api.csproj --configuration Release --no-restore --artifacts-path .artifacts/gate10-api -v:minimal` | PASS — 0 warnings, 0 errors |
| Complete API suite | `dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --no-restore -v:minimal` | PASS — 42/42 |
| Marketplace PostgreSQL acceptance | `dotnet test ... --filter FullyQualifiedName~MarketplaceAcceptanceTests` | PASS — publish → discover → send → respond → accept, wrong-supplier denial, expiry, stale version, privacy projection, audit/outbox and immutability |
| Architecture | `python -m pytest tests/architecture -q` | PASS — 23/23 |
| Agent runtime | `cd agent-runtime; python -m pytest tests -q` | PASS — 18/18 |
| Web lint | `cd web; npm run lint` | PASS — 0 warnings, 0 errors |
| Web unit tests | `cd web; npm test` | PASS — 4/4 |
| Web production build | `cd web; npm run build` | PASS |
| Desktop and compact browser journeys | `cd web; npx playwright test` | PASS — 20/20 |
| Retained OpenAPI | generation-only Swagger command plus complete API suite | PASS — retained contract equals the running API |

## Corrected checks

1. The first marketplace migration run found duplicate inventory constraints already owned by
   Gate 7. Gate 10 stopped recreating or removing those constraints; the final migration passed.
2. The first full rollback check found dependency order errors. The down migration now removes
   the cross-policy and circular current-version foreign key before dropping tables; the final
   migration suite passed.
3. Architecture initially found inline governed codes. The final implementation uses generated
   master-data projections and all 23 checks pass.
4. The normal Release output directory remains locked by the already-running local API process
   (PID 6468). That exact command fails with `MSB3027`/`MSB3021`; the isolated Release build above
   proves the current source with zero warnings and errors without stopping the user's process.

## Remaining Gate 10 scope

This report does not close or approve Gate 10. Booking responses, commercial settings and any
remaining normative Gate 10 acceptance cases are not implemented. ADR-0011 remains proposed,
and owner plus independent engineering/security/privacy review remain pending. No shared or
production database migration was run.
