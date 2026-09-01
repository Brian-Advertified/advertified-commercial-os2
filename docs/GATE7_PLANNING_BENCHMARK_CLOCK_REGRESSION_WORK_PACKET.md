# Gate 7 planning benchmark clock regression work packet

**Recorded:** 2026-09-01  
**Authority:** Brian Rabuthu's standing sequential local-delivery direction  
**Boundary:** deterministic local planning acceptance evidence only; no production, cloud,
provider, deployment, commit or push authority

## Bounded regression

`CanonicalPlanningAcceptanceTests.SoloAgencyOperatorTakesApprovedBriefThroughApprovedPlan`
uses inventory and rate fixtures anchored at `2026-08-29`, but its application host currently
resolves `TimeProvider.System`. The product-detail benchmark therefore changes when the wall clock
crosses a fixture rate's `2026-08-31` expiry. On `2026-09-01` the product benchmark reports three
peers instead of its intentionally different four-peer current-date cohort.

The shortlist benchmark must remain three peers because its September 2026 running period excludes
that expiring rate. The interactive product-detail benchmark must remain four peers at the fixture's
comparison date. This proves that both paths apply their own exact comparison window instead of
silently mixing a stale rate into campaign planning or inheriting the machine date in repeatable
evidence.

## Required correction

- Bind the two hosts used by this acceptance journey to the fixture's exact UTC clock through the
  existing dependency-injected `TimeProvider` boundary.
- Do not change production benchmark calculation, rate-period compatibility, cohort thresholds,
  seed dates, or the two expected cohort sizes.
- Preserve all unrelated Planning, Inventory, projection, email and outbox changes in the dirty tree.

## Acceptance evidence

- The isolated canonical planning journey passes in Release with product cohort `4`, shortlist
  cohort `3`, and the existing stale-rate rejection.
- All affected planning/benchmark acceptance tests pass.
- The Release API build and scoped formatter pass.
- The final diff is limited to this packet and deterministic test-host clock wiring; no live or
  production resource is used.

## Verification result

- Reproduction before correction: isolated Release journey failed with expected cohort `4`, actual
  cohort `3`, at `CanonicalPlanningAcceptanceTests.cs:153`.
- Corrected isolated journey: PASS, `1/1`.
- Related marketplace planning-lineage acceptance tests: PASS, `4/4`.
- Scoped `dotnet format --verify-no-changes`: PASS, `0` files required formatting.
- Commercial API Release build: PASS, `0` warnings and `0` errors.
- Complete architecture suite: PASS, `31/31`.

No production calculation was changed. No live/provider/cloud resource, production data, commit or
push was used.

```powershell
dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
  -c Release `
  --filter 'FullyQualifiedName~CanonicalPlanningAcceptanceTests.SoloAgencyOperatorTakesApprovedBriefThroughApprovedPlan' `
  --no-restore
dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
  -c Release --filter 'FullyQualifiedName~MarketplaceAcceptanceTests' --no-build --no-restore
dotnet format api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj `
  --no-restore --verify-no-changes `
  --include api/tests/Advertified.Commercial.Api.Tests/CanonicalPlanningAcceptanceTests.cs `
            api/tests/Advertified.Commercial.Api.Tests/CanonicalPlanningAcceptanceTests.Support.cs
dotnet build api/Advertified.Commercial.Api.csproj -c Release --no-restore
python -m pytest tests/architecture -q
```
