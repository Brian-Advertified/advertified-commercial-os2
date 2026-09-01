# Gate 12 reproducible application packaging work packet

**Status:** IN PROGRESS — local non-production implementation only  
**Owner direction:** continue local hardening toward production; no commit, push, registry publication, deployment, cloud mutation, or production access is authorised  
**Date:** 2026-09-01  
**Predecessor evidence:** the corrected final-tree Commercial API regression passed 128/128; the final combined verification/evidence record remains in progress

## Bounded requirement

Add reproducible, least-privilege build definitions for the three application processes that already exist:

1. the C# Commercial API and the separately invoked database migrator;
2. the single Python/FastAPI agent runtime; and
3. the compiled React/Vite public and authenticated web application served as static assets with SPA fallback and same-origin `/api` forwarding.

The packet may add dependency-lock enforcement, container build inputs, container-local health checks, architecture guards, and deterministic local verification evidence. It must not invent a production worker, service identity, AWS topology, secret, live provider, or release artifact.

## Acceptance evidence

- .NET restore succeeds in locked mode for every API, domain, infrastructure, migrator, and test project.
- Web installation uses `npm ci`; Python installation uses the hash-locked runtime requirements.
- Every application base image is content-addressed and each final process runs as a non-root user.
- The API and migrator build from one canonical source graph but produce separate runnable targets.
- The web image serves the SPA and forwards same-origin `/api` requests without baking an environment-specific API host into browser code.
- Local Linux image builds succeed for API, migrator, runtime, and web.
- Container-local liveness smoke checks succeed for API, deterministic agent runtime, and web; the migrator help/startup boundary is exercised without applying production data.
- Architecture tests reject missing locks, floating base images, root final users, or duplicate application/runtime ownership.
- The complete affected builds and test suites remain green, and retained evidence names exact commands and outcomes.

## Explicit exclusions and blockers

- Images built from this dirty, uncommitted tree are verification images, not immutable release artifacts. A clean commit and an owner-authorised commit remain required before release provenance can exist.
- Registry push, SBOM publication, image signing/attestation, production IaC, staging deployment, DNS/TLS, managed secrets, Cognito/OIDC, cross-tenant worker scheduling, and production smoke are later packets with their own approval and evidence.
- No worker image is created until its accepted tenant-bound service-identity and execution contract exists.
- Live or paid AI/provider calls remain disabled.

## Verification record

Local packaging implementation is partially verified under
`docs/evidence/gate12-reproducible-application-packaging-20260901/`.

- Build Compose validates and all four canonical Linux application images build from pinned bases.
- API and migrator publish with the repository-pinned .NET SDK 10.0.400.
- Web `npm ci`/Vite build and non-root Caddy packaging pass. Explicit Vite 8 code splitting removed
  the Linux-only oversized-main-chunk warning without changing the warning threshold.
- The existing `advertified-dev` stack was recreated from the current images and its runtime, API
  and web services are healthy; 3/3 connected critical browser journeys pass through the packaged
  same-origin web/API boundary.
- Final-tree architecture passes 42/42.

The packet remains **IN PROGRESS** because the complete current-source C# test suite still needs a
10.0.400-capable runner or remote CI, and the configured pinned Gitleaks/Syft/Trivy CI checks do not
yet have a remote result for this uncommitted tree. No release provenance or production approval is
claimed.
