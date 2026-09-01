# Gate 12 reproducible application packaging — 2026-09-01

## Result

The existing Commercial API, migrator, Python runtime and React/Vite web application now have production-shaped Linux build definitions with pinned base-image digests, locked dependency installation and non-root final processes. The current local `advertified-dev` stack was recreated from those images rather than creating a new test stack.

## Verified locally

- `docker-compose.build.yml` validates for the four application processes.
- API and migrator publish successfully with the repository-pinned .NET SDK 10.0.400.
- Agent runtime installs its hash-locked runtime graph and builds from Python 3.12.14.
- Web installs with `npm ci`, compiles with Vite 8 and runs under non-root Caddy.
- Caddy provides SPA fallback, same-origin `/api` forwarding, security headers, compressed assets and differentiated cache policy for fingerprinted versus non-fingerprinted assets.
- The Linux-only oversized-main-chunk regression was fixed with explicit Vite 8 `rolldownOptions.output.codeSplitting`. The warning threshold was not raised.
- The current web/API/runtime stack is healthy and 3/3 connected critical browser journeys pass through the packaged web image.
- Final-tree architecture checks pass 42/42.

## Remaining verification

The complete current C# test suite is not yet current because this Windows host has .NET SDK 10.0.103 while `global.json` pins 10.0.400 with roll-forward disabled. This is a local toolchain gap, not an API image compilation failure. A 10.0.400-capable runner or remote CI must execute the full suite.

The CI workflow now contains pinned Gitleaks, Syft and Trivy steps, but no remote CI result exists for this uncommitted tree. Therefore source-secret scanning, final-image SBOMs and HIGH/CRITICAL image vulnerability results remain pending evidence, and this packet remains in progress rather than certified complete.

## Resource discipline

Only the existing canonical image names and the existing `advertified-dev` Compose project were rebuilt/recreated. No new ad-hoc Compose project, live provider, production resource or paid AI call was used.
