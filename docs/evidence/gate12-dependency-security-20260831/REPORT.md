# Gate 12 Python dependency security evidence

**Evidence date:** 2026-08-31

**Base commit:** `74f862f3c341824368b9a3e715bb53bd883c34b6`

**Live provider or production resource used:** No

## Initial findings

- The pinned runtime graph resolved FastAPI 0.104.1 and Starlette 0.27.0. `pip-audit` 2.10.1
  reported 10 known vulnerabilities across those two packages.
- The development graph additionally reported `PYSEC-2026-1845` for pytest 7.4.3, for 11 findings
  across three packages. The highest reported Starlette fix floor was 1.3.1 and the pytest fix floor
  was 9.0.3.
- The complete Commercial API test graph returned no known vulnerable direct or transitive NuGet
  package from the configured current source.

## Implemented

- Updated the affected runtime chain to FastAPI 0.141.1, Starlette 1.3.1 and Pydantic 2.13.4.
  Starlette is now an explicit direct pin so a clean install cannot resolve the vulnerable 0.27 line.
- Updated pytest to 9.1.1 and pinned `pip-audit` 2.10.1 as a development security tool. Unaffected
  Uvicorn, HTTPX and Ruff pins remain unchanged.
- Added runtime and development graph audits to the agent-runtime CI job before lint and tests. Any
  advisory makes that job fail; there is no ignore/suppression list.
- Added the explicitly ignored `.artifacts` directory to the architecture scanner's generated-path
  exclusion. Clean-install dependencies remain outside Git and are no longer misclassified as
  authored source; tracked and authored source paths retain the same guardrails.
- Renamed the CI install step from “locked” to “pinned” because the repository pins direct Python
  requirements but does not yet retain a hash-locked transitive graph. That remaining supply-chain
  gap is not hidden by this remediation.

Official release references used to select current stable compatibility lines: [FastAPI on PyPI](https://pypi.org/project/fastapi/),
[FastAPI release notes](https://fastapi.tiangolo.com/release-notes/),
[Pydantic on PyPI](https://pypi.org/project/pydantic/) and
[pytest on PyPI](https://pypi.org/project/pytest/).

## Verification

| Check | Result |
|---|---|
| Clean Python dependency installation | PASS - exact direct pins resolve in an empty ignored target |
| Clean deterministic runtime | PASS - Ruff and 28/28 tests against the isolated fixed graph |
| Runtime dependency audit | PASS - no known vulnerabilities found |
| Development dependency audit | PASS - no known vulnerabilities found |
| Complete .NET dependency audit | PASS - no known vulnerable direct or transitive package |
| CI audit gate | IMPLEMENTED - both audit commands precede lint/tests; remote CI execution remains pending |
| Complete architecture suite | BLOCKED - 21/23; separately owned screen work still contains a 521-line CSS file and inline governed `PENDING` |
| Web/browser regression | NOT RUN - another agent owns and is actively changing the screen tree |

## Compatibility and containment

The clean test run imported and printed the selected FastAPI, Starlette, Pydantic and pytest versions
before executing all runtime tests. Provider mode remains disabled by default; deterministic agent
authentication, strict typed schemas, evidence boundaries, agent roster, health and zero-cost
behavior remain covered. No live/paid provider call or canonical-state mutation occurred.

The scan checks the advisory data available at execution time; it is not a permanent security
approval. CI must rerun it for each change. Hash-locked transitive requirements, provenance policy,
SBOM generation, container/image scanning, controlled dependency update cadence and an independent
security review remain required before production.

## Remaining boundary

Remote CI has not executed this commit. Web dependencies are owned by the active screen work and were
not changed or audited in this packet. Container base-image scanning, secret scanning, SBOM/provenance,
staging and named security/privacy approval remain pending. No push, deployment or production
mutation was performed.
