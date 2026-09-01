# Gate 12 Docling transport-security evidence

**Evidence date:** 2026-09-01

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this is not a release artefact

**Live provider, Docker, cloud or production resource used:** No

## Implemented

- API startup now rejects Docling mode outside Development and Test unless the configured base URL
  is absolute HTTPS, has a host and contains no embedded user information.
- The rejection occurs during inventory-extraction registration, before a Docling adapter can be
  resolved to send inventory bytes or the `X-Api-Key` header.
- The registered Docling handler disables automatic redirects so a method-preserving redirect cannot
  forward inventory bytes or the custom API-key header to another origin.
- The existing Development/Test exception preserves the HTTP Docling endpoint used by local
  deterministic infrastructure.
- The existing non-local ban on deterministic extraction remains unchanged.

## Intermediate security finding corrected

The initial HTTPS-only verification did not constrain the default .NET redirect handler. Automatic
redirects clear `Authorization` but not other headers, so a method-preserving redirect to another
HTTPS origin could carry the custom `X-Api-Key` and multipart source. Modern .NET prevents automatic
HTTPS-to-HTTP downgrade, but the cross-origin HTTPS disclosure path remained. The corrected client
does not follow redirects at all.

## Verification

| Check | Result |
|---|---|
| Release Docling test class | PASS - 5/5: production HTTP and embedded-userinfo startup rejected; Test HTTP startup allowed; loopback redirect was not followed; existing adapter contract/header mapping passed |
| Scoped .NET formatting | PASS - API registration and Docling test files required no remaining formatter change |
| Complete architecture suite | PASS - 31/31 |
| Scoped source diff and added-file whitespace checks | PASS - no whitespace error |

The Release test command compiled the affected API and test project graph before executing the
focused class. The redirect test used two loopback-only Kestrel endpoints: the configured endpoint
received the expected source and key, returned HTTP 307, and the redirected endpoint received no
request. The local-positive startup test did not contact its configured Docling URL.

## Reproduction

```powershell
dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj -c Release --filter 'FullyQualifiedName~DoclingInventoryExtractionAdapterTests' --no-restore
dotnet format api/Advertified.Commercial.Api.csproj --no-restore --include api/InventoryExtractionRegistration.cs --verbosity minimal
dotnet format api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --no-restore --include api/tests/Advertified.Commercial.Api.Tests/DoclingInventoryExtractionAdapterTests.cs --verbosity minimal
python -m pytest tests/architecture -q
git diff --check -- api/InventoryExtractionRegistration.cs api/tests/Advertified.Commercial.Api.Tests/DoclingInventoryExtractionAdapterTests.cs
rg -n '[ \t]+$' -- api/InventoryExtractionRegistration.cs api/tests/Advertified.Commercial.Api.Tests/DoclingInventoryExtractionAdapterTests.cs docs/GATE12_DOCLING_TRANSPORT_SECURITY_WORK_PACKET.md docs/evidence/gate12-docling-transport-security-20260901/REPORT.md docs/evidence/gate12-docling-transport-security-20260901/manifest.json
```

## Boundary retained

This evidence proves only the environment-specific configured-URL guard, no-redirect handler policy
and their deterministic startup/loopback behavior. Managed service identity, secret rotation,
private networking, certificate-chain evidence, guarded sandbox/canary execution, staging,
production deployment and named production approval remain unverified or blocked. No commit or push
was performed.
