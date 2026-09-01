# Gate 12 Docling transport-security work packet

**Owner direction:** 2026-09-01, continue local production hardening.

**Verified predecessor:** The Gate 12 immutable-build-input evidence records 31/31 architecture
checks, healthy pinned local dependencies, zero source secret findings and passing local API/web
smoke checks. Gate 12 remains in progress.

**Authority:** Local non-production source changes and deterministic verification only. No live
provider call, Docker/cloud mutation, production resource, deployment, commit or push.

## Bounded requirement

Fail API startup when Docling mode is configured outside Development or Test with a base URL whose
scheme is not HTTPS. Development and Test may keep the HTTP endpoint used by local Compose.

This check must run before the Docling adapter can send inventory bytes or its `X-Api-Key` header.
It does not certify the Docling service identity, private network, certificate chain, production
deployment or a live integration.

## Intermediate security finding

The first focused verification exposed a remaining redirect boundary. The .NET HTTP handler follows
redirects by default and clears `Authorization` only; the custom `X-Api-Key` header can therefore be
forwarded with a method-preserving redirect to another HTTPS origin. Modern .NET blocks automatic
HTTPS-to-HTTP downgrade, but that does not protect cross-origin HTTPS. The client must not follow any
Docling redirect because the request contains both a credential and source inventory bytes.

## Normative alignment

Sections 26, 26.1, 27.1 and 27.2 require an Advertified-owned integration boundary, protected
credentials, production TLS and restricted network paths. The environment exception is limited to
the specification's local deterministic profile and the repository's Test environment.

## Required implementation

1. Keep the existing Docling mode, adapter and local Compose configuration unchanged.
2. Centralise the environment check in inventory-extraction registration so non-local Docling
   configuration requires an absolute HTTPS URL with a host and no embedded user information before
   the adapter is registered for use.
3. Preserve the existing ban on deterministic extraction outside Development and Test.
4. Disable automatic redirects on the registered Docling HTTP handler.
5. Add production startup-negative cases, one local HTTP startup-positive case and one loopback-only
   redirect regression. No test may contact Docling or another external dependency.

## Acceptance evidence

1. Production startup with Docling and an HTTP or embedded-user-information URL fails with a
   specific transport-security error.
2. Test startup with the same complete HTTP Docling configuration succeeds without sending a
   request to Docling.
3. A loopback Docling endpoint returning a method-preserving redirect receives the original request,
   but the redirected loopback origin receives neither a request nor the API key or inventory bytes.
4. The existing deterministic adapter mapping/header test still passes.
5. The affected Release build, focused tests, formatter and scoped diff check pass.

## Explicitly out of scope and still blocked

- managed Docling service identity, secret injection and rotation;
- private production networking, certificate issuance or certificate validation evidence;
- guarded sandbox/canary execution, staging or production deployment;
- production readiness approval, commit or push.
