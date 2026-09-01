# Gate 12 immutable build inputs work packet

**Owner direction:** 2026-09-01, continue local production hardening after the verified email
delivery durability slice.

**Verified predecessor:** Registry 2.11.0 and migration 026 are applied to the named local database;
the isolated Release API suite passes 109/109, web Playwright 32/32, deterministic runtime 28/28,
architecture 23/23, all six Compose dependencies are healthy, and a 977-file tracked/non-ignored
source scan has zero secret findings. Gate 12 remains in progress.

**Authority:** Local non-production source changes, image resolution, builds and deterministic
verification only. No cloud mutation, production resource, live provider, deployment, commit or
push.

## Bounded requirement

Make the existing third-party GitHub Action references and externally sourced local dependency
container references content-addressed. Then make a repository architecture check fail if a
mutable reference is reintroduced in those bounded locations. This does not make every build input
immutable: runner images and the Python `3.12`, Node `22` and .NET `10.0.x` selectors still float.

This packet does not add application images, a release artefact, SBOM generation, signing, remote CI,
staging or production deployment.

## Normative alignment

Sections 17.5, 27.2, 27.5 and 28.3 require pinned supply-chain inputs, automated enforcement,
immutable builds and launch-blocking security evidence. A mutable release tag may remain as a human
annotation, but it is not the executed reference.

## Required implementation

1. Resolve each existing `actions/*` release tag to a full 40-character commit SHA. Execute the SHA
   and retain the release tag in an adjacent comment for controlled updates.
2. Every checkout step disables persisted credentials because all current jobs are read-only.
3. Preserve the currently selected ClamAV, Redis, MailHog and pgvector/PostgreSQL versions while
   adding their verified manifest digest. Preserve the already pinned MinIO and Docling digests.
4. Treat the locally built `advertified/postgres-dev` name as an explicit build-output exception; it
   must retain a local `build` definition and must not be mistaken for an external mutable input.
5. Add a focused architecture rule that inspects workflow `uses:`, Compose `image:` values and
   Dockerfile `FROM` values. It rejects mutable third-party references and malformed digests while
   allowing only documented local build outputs.
6. Do not upgrade action or image versions, add registries, log credentials, or weaken any existing
   security/build check.

## Acceptance evidence

1. Architecture tests cover valid immutable references and representative mutable/malformed
   references, then pass against the repository files.
2. All workflow action references execute full commit SHAs and all checkout steps set
   `persist-credentials: false`.
3. Every external Compose image and Dockerfile base is digest-pinned; the local PostgreSQL service
   remains build-backed.
4. `docker compose config --quiet`, the PostgreSQL image build, extension verification and all six
   local health checks pass using the pinned inputs.
5. The complete architecture suite, diff check, source secret scan and artifact/staging hygiene pass.
6. Exact resolved tags, SHAs and image digests are retained as reproducible evidence. No remote CI,
   live provider, production resource or paid call is used.

## Explicitly out of scope and still blocked

- API, web, runtime and worker production Dockerfiles and immutable application release artefacts;
- SBOM generation, container vulnerability scanning, provenance/attestation and image signing;
- a blocking CI gitleaks job and explicit .NET transitive dependency lock policy;
- production OIDC/Cognito and durable server-side session storage;
- remote CI, cloud/staging deployment, Security/Privacy/Legal approval or production greenlight.
