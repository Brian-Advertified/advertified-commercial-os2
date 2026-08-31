# Gate 12 Python dependency security work packet

**Owner direction:** continue local production hardening without editing the separately owned screens.

**Verified predecessor:** local commit `74f862f`; fail-closed Commercial API dependency readiness,
retained OpenAPI, complete Release API 71/71 and recovery evidence pass. No push, deployment,
production resource or live provider was used.

## Bounded requirement

Remove the known vulnerabilities from the pinned Python agent-runtime and development dependency
graphs, and make recurrence a blocking CI check. Preserve the deterministic zero-cost runtime
contracts and do not use a live AI/provider during validation.

## Initial evidence

- `pip-audit` 2.10.1 against `agent-runtime/requirements.txt` reports 10 advisories in FastAPI
  0.104.1 and its resolved Starlette 0.27.0 dependency.
- The development graph adds one pytest 7.4.3 advisory, for 11 findings across three packages.
- The complete .NET test graph has no known vulnerable direct or transitive package from the current
  NuGet source.

## Acceptance evidence

1. Pin FastAPI 0.141.1, Starlette 1.3.1 and Pydantic 2.13.4 as a compatible fixed runtime set. Pin
   pytest 9.1.1 and `pip-audit` 2.10.1 in the development graph. Other unaffected dependencies remain
   unchanged to keep the remediation bounded.
2. A clean Python 3.12-compatible installation from `requirements-dev.txt` resolves without conflict.
3. Ruff and the complete deterministic runtime tests pass against the clean installed graph. Health,
   authentication, strict typed schemas, exact evidence and zero-cost/provider-disabled behavior
   remain covered.
4. `pip-audit` reports zero known vulnerabilities for both runtime and development requirement files.
5. CI installs the pinned direct requirements, audits both resolved dependency graphs and blocks the
   agent-runtime job when either audit fails.
6. Architecture, Compose, diff and artifact hygiene checks retain exact results; `.artifacts/` remains
   untracked and its disposable third-party tool trees are not misclassified as authored source.

## Explicitly out of scope

- accepting a vulnerability, suppressing an advisory or weakening a runtime/test assertion;
- upgrading unaffected Uvicorn, HTTPX or Ruff pins without a demonstrated requirement;
- container SBOM/image scanning, web dependency changes or deployment infrastructure;
- live/paid AI/provider calls, production data, cloud mutation, push or deployment;
- declaring security review, Gate 12 or production readiness approved;
- editing or testing the separately owned screen implementation.
