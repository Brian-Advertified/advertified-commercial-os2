# Gate 12 Python hash-lock work packet

**Owner direction:** continue local production hardening without editing the separately owned screens.

**Verified predecessor:** local commit `27068ae`; the fixed Python direct pins install cleanly, Ruff
and all 28 deterministic tests pass, runtime/development `pip-audit` scans have zero known findings,
the .NET dependency graph has zero known findings and CI contains blocking Python audit steps. No
push, deployment, production resource or live provider was used.

## Bounded requirement

Replace unconstrained Python transitive resolution in CI and future runtime builds with reviewed,
hash-locked runtime and development graphs generated deterministically from the existing direct
requirement files.

## Acceptance evidence

1. Pin `pip-tools` 7.6.1 as the lock generator and generate separate runtime and development lock
   files with exact transitive versions and distribution hashes. Generated headers use a stable,
   platform-neutral command label.
2. A previously absent isolated Python 3.12-compatible target installs the development lock with
   `--require-hashes`; no direct un-hashed requirement or dependency-conflict escape is permitted.
3. Ruff and all deterministic runtime tests pass with versions loaded from the locked graph.
4. `pip-audit` reports zero known vulnerabilities against both lock files.
5. CI installs the development lock with hash verification, regenerates both locks and fails if the
   generated output differs from Git, then runs both audits before lint/tests.
6. Architecture, Compose, diff and artifact hygiene checks retain exact outcomes; `.artifacts/`
   remains ignored and untracked.

## Explicitly out of scope

- changing the already verified FastAPI, Starlette, Pydantic, Uvicorn, HTTPX, pytest or Ruff choices;
- suppressing hashes, audit findings or test failures;
- operating-system packages, container/image locks, SBOM generation, signing or provenance policy;
- web dependencies, screen files, deployment infrastructure or cloud resources;
- live/paid AI/provider calls, production data, push or deployment;
- declaring security review, Gate 12 or production readiness approved.
