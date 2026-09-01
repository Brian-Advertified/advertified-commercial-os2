# Gate 12 immutable build inputs evidence

**Evidence date:** 2026-09-01

**Base commit:** `53209e7f718b64a25fc5fcf9aa83365301c5a50c`

**Working tree:** Uncommitted review on `master`; this is not a release artefact

**Live provider or production resource used:** No

## Implemented

- Resolved every existing `actions/*` release tag from its official GitHub repository and changed
  all nine workflow references to execute full 40-character commit SHAs. The selected release tags
  remain comments for controlled updates.
- Disabled checkout credential persistence in all five read-only jobs.
- Preserved the selected ClamAV 1.4.3, Redis 7.4.2-alpine, MailHog 1.0.1 and pgvector 0.8.6 /
  PostgreSQL 16 versions while binding them to their registry manifest digests. Existing MinIO and
  Docling digest bindings were retained.
- Retained `advertified/postgres-dev:16-postgis3-pgvector0.8.6` as the sole local build-output
  exception and retained its Compose `build` definition.
- Added one focused architecture test module covering full action SHAs, checkout credential
  handling, image digests, Dockerfile bases, multistage aliases and the local build exception.

## Resolved identifiers

| Input | Human version | Executed immutable identifier |
|---|---|---|
| `actions/checkout` | v4 | `11d5960a326750d5838078e36cf38b85af677262` |
| `actions/setup-python` | v5 | `a26af69be951a213d495a4c3e4e4022e16d87065` |
| `actions/setup-node` | v4 | `49933ea5288caeca8642d1e84afbd3f7d6820020` |
| `actions/setup-dotnet` | v4 | `67a3573c9a986a3f9c594539f4ab511d57bb3ce9` |
| `clamav/clamav` | 1.4.3 | `sha256:75fb5fd95fcbe1d7e6d240c369c1572b686ee2c95949d1042b5148de8eddebb4` |
| `redis` | 7.4.2-alpine | `sha256:02419de7eddf55aa5bcf49efb74e88fa8d931b4d77c07eff8a6b2144472b6952` |
| `mailhog/mailhog` | v1.0.1 | `sha256:8d76a3d4ffa32a3661311944007a415332c4bb855657f4f6c57996405c009bea` |
| `pgvector/pgvector` | 0.8.6-pg16-bookworm | `sha256:ccc6e83d6e35e931dc7c5def2022729d5a6c370318d099181995567ff1fb4d6b` |

## Verification

| Check | Result |
|---|---|
| Official GitHub tag resolution | PASS - four selected action tags resolved successfully |
| Registry manifest inspection | PASS - all four newly bound image/base tags resolved to the retained digests |
| Focused immutable-input tests | PASS - 8/8 |
| Complete architecture suite | PASS - 31/31 |
| Compose configuration | PASS - five external digest pins plus one local build output |
| Pinned PostgreSQL build | PASS - digest-bound base resolved; build completed |
| PostgreSQL version/extensions | PASS - PostgreSQL 16.15, pgcrypto 1.3, PostGIS 3.6.4, vector 0.8.6 |
| Local service health | PASS - all six configured services healthy |
| Tracked/non-ignored source secret scan | PASS - gitleaks 8.30.1 returned zero findings |
| Diff/artifact/staging hygiene | PASS - no diff-check error, staged file or tracked artifact output |
| Local application smoke after dependency restart | PASS - API live/ready and web root returned HTTP 200 |

## Boundary retained

This evidence proves content-addressed inputs for the existing CI actions and local dependency
stack. It does not provide application Dockerfiles, immutable release images, SBOMs, container
vulnerability scanning, image signing or provenance, production OIDC/session persistence, remote
CI, staging, production deployment or independent Security/Privacy/Operations approval. No action
or image version was upgraded, and no commit or push was performed.
